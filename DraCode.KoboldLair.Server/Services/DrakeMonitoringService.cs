using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Orchestrators;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors Drake supervisors and their Kobold workers.
    /// Runs periodically to check Kobold work progress and update task statuses.
    /// Detects and handles stuck Kobolds that exceed the configured timeout.
    /// </summary>
    public class DrakeMonitoringService : BackgroundService
    {
        private readonly ILogger<DrakeMonitoringService> _logger;
        private readonly DrakeFactory _drakeFactory;
        private readonly TimeSpan _monitoringInterval;
        private readonly TimeSpan _stuckKoboldTimeout;
        private bool _isRunning;
        private readonly object _lock = new object();

        // Throttle concurrent Drake monitoring to avoid overwhelming I/O
        private readonly SemaphoreSlim _drakeThrottle;
        private const int MaxConcurrentDrakes = 5;

        /// <summary>
        /// Default timeout for stuck Kobold detection (30 minutes)
        /// </summary>
        public static readonly TimeSpan DefaultStuckTimeout = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Creates a new Drake monitoring service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="drakeFactory">Factory managing all Drakes</param>
        /// <param name="monitoringIntervalSeconds">Interval in seconds between monitoring runs (default: 60)</param>
        /// <param name="stuckKoboldTimeoutMinutes">Timeout in minutes before a Kobold is considered stuck (default: 30)</param>
        public DrakeMonitoringService(
            ILogger<DrakeMonitoringService> logger,
            DrakeFactory drakeFactory,
            int monitoringIntervalSeconds = 60,
            int stuckKoboldTimeoutMinutes = 30)
        {
            _logger = logger;
            _drakeFactory = drakeFactory;
            _monitoringInterval = TimeSpan.FromSeconds(monitoringIntervalSeconds);
            _stuckKoboldTimeout = TimeSpan.FromMinutes(stuckKoboldTimeoutMinutes);
            _isRunning = false;
            _drakeThrottle = new SemaphoreSlim(MaxConcurrentDrakes, MaxConcurrentDrakes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "🐉 Drake Monitoring Service started. Interval: {Interval}s, Stuck timeout: {Timeout} min",
                _monitoringInterval.TotalSeconds,
                _stuckKoboldTimeout.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for the interval before next run
                    await Task.Delay(_monitoringInterval, stoppingToken);

                    // Check if previous job is still running
                    bool canRun;
                    lock (_lock)
                    {
                        canRun = !_isRunning;
                        if (canRun)
                        {
                            _isRunning = true;
                        }
                    }

                    if (!canRun)
                    {
                        _logger.LogWarning("⚠️ Previous monitoring job still running, skipping this cycle");
                        continue;
                    }

                    // Execute monitoring
                    await MonitorDrakesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in Drake monitoring cycle");
                }
                finally
                {
                    // Mark job as complete
                    lock (_lock)
                    {
                        _isRunning = false;
                    }
                }
            }

            _logger.LogInformation("🛑 Drake Monitoring Service stopped");
        }

        /// <summary>
        /// Monitors all Drakes and their Kobolds in parallel
        /// </summary>
        private async Task MonitorDrakesAsync(CancellationToken cancellationToken)
        {
            var drakes = _drakeFactory.GetAllDrakes();

            if (drakes.Count == 0)
            {
                _logger.LogDebug("No Drakes to monitor");
                return;
            }

            _logger.LogDebug("🔍 Monitoring {Count} Drake(s) (max {Max} concurrent)", drakes.Count, MaxConcurrentDrakes);

            // Monitor Drakes with throttled parallelism
            var monitoringTasks = drakes.Select(async drake =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await _drakeThrottle.WaitAsync(cancellationToken);
                try
                {
                    await MonitorSingleDrakeAsync(drake, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error monitoring Drake");
                }
                finally
                {
                    _drakeThrottle.Release();
                }
            });

            await Task.WhenAll(monitoringTasks);

            _logger.LogInformation("✅ Monitoring cycle completed");
        }

        /// <summary>
        /// Monitors a single Drake supervisor
        /// </summary>
        private async Task MonitorSingleDrakeAsync(Drake drake, CancellationToken cancellationToken)
        {
            // Monitor tasks to sync status (async to avoid blocking on git operations)
            await drake.MonitorTasksAsync();

            // Get statistics
            var stats = drake.GetStatistics();

            _logger.LogDebug(
                "📊 Drake Stats - Kobolds: {TotalKobolds} (Working: {Working}, Done: {Done}) | Tasks: {TotalTasks} (Working: {WorkingTasks}, Done: {DoneTasks})",
                stats.TotalKobolds,
                stats.WorkingKobolds,
                stats.DoneKobolds,
                stats.TotalTasks,
                stats.WorkingTasks,
                stats.DoneTasks
            );

            // Check for stuck Kobolds (working longer than timeout threshold)
            if (stats.WorkingKobolds > 0)
            {
                _logger.LogDebug("⚡ {Count} Kobold(s) currently working", stats.WorkingKobolds);

                // Detect and handle stuck Kobolds (async to avoid blocking on git operations)
                var stuckKobolds = await drake.HandleStuckKoboldsAsync(_stuckKoboldTimeout);

                if (stuckKobolds.Count > 0)
                {
                    _logger.LogWarning(
                        "🚨 Handled {Count} stuck Kobold(s) (timeout: {Timeout} minutes)",
                        stuckKobolds.Count,
                        _stuckKoboldTimeout.TotalMinutes);

                    foreach (var (koboldId, taskId, duration) in stuckKobolds)
                    {
                        _logger.LogWarning(
                            "   - Kobold {KoboldId}: worked {Duration:F1} min on task {TaskId}",
                            koboldId.ToString()[..8],
                            duration.TotalMinutes,
                            taskId?[..8] ?? "unknown");
                    }
                }
            }

            // Cleanup completed Kobolds
            if (stats.DoneKobolds > 0)
            {
                var unsummoned = drake.UnsummonCompletedKobolds();
                if (unsummoned > 0)
                {
                    _logger.LogInformation("🗑️ Unsummoned {Count} completed Kobold(s)", unsummoned);
                }
            }

            // Update the task file (async to avoid blocking)
            await drake.UpdateTasksFileAsync();
        }

        /// <summary>
        /// Gets the current status of the monitoring service
        /// </summary>
        public bool IsCurrentlyRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
        }
    }
}
