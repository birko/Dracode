using DraCode.KoboldLair.Server.Supervisors;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors Drake supervisors and their Kobold workers.
    /// Runs periodically to check Kobold work progress and update task statuses.
    /// </summary>
    public class DrakeMonitoringService : BackgroundService
    {
        private readonly ILogger<DrakeMonitoringService> _logger;
        private readonly DrakeFactory _drakeFactory;
        private readonly TimeSpan _monitoringInterval;
        private bool _isRunning;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new Drake monitoring service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="drakeFactory">Factory managing all Drakes</param>
        /// <param name="monitoringIntervalSeconds">Interval in seconds between monitoring runs (default: 60)</param>
        public DrakeMonitoringService(
            ILogger<DrakeMonitoringService> logger,
            DrakeFactory drakeFactory,
            int monitoringIntervalSeconds = 60)
        {
            _logger = logger;
            _drakeFactory = drakeFactory;
            _monitoringInterval = TimeSpan.FromSeconds(monitoringIntervalSeconds);
            _isRunning = false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🐉 Drake Monitoring Service started. Interval: {Interval}s", _monitoringInterval.TotalSeconds);

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
        /// Monitors all Drakes and their Kobolds
        /// </summary>
        private async Task MonitorDrakesAsync(CancellationToken cancellationToken)
        {
            var drakes = _drakeFactory.GetAllDrakes();

            if (drakes.Count == 0)
            {
                _logger.LogDebug("No Drakes to monitor");
                return;
            }

            _logger.LogInformation("🔍 Monitoring {Count} Drake(s)", drakes.Count);

            foreach (var drake in drakes)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await MonitorSingleDrakeAsync(drake, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error monitoring Drake");
                }
            }

            _logger.LogInformation("✅ Monitoring cycle completed");
        }

        /// <summary>
        /// Monitors a single Drake supervisor
        /// </summary>
        private async Task MonitorSingleDrakeAsync(Drake drake, CancellationToken cancellationToken)
        {
            // Monitor tasks to sync status
            drake.MonitorTasks();

            // Get statistics
            var stats = drake.GetStatistics();

            _logger.LogInformation(
                "📊 Drake Stats - Kobolds: {TotalKobolds} (Working: {Working}, Done: {Done}) | Tasks: {TotalTasks} (Working: {WorkingTasks}, Done: {DoneTasks})",
                stats.TotalKobolds,
                stats.WorkingKobolds,
                stats.DoneKobolds,
                stats.TotalTasks,
                stats.WorkingTasks,
                stats.DoneTasks
            );

            // Check for stuck Kobolds (working for more than 30 minutes)
            var workingKobolds = drake.GetStatistics();
            if (workingKobolds.WorkingKobolds > 0)
            {
                _logger.LogInformation("⚡ {Count} Kobold(s) currently working", workingKobolds.WorkingKobolds);
                
                // TODO: Add logic to detect and handle stuck Kobolds
                // For example, Kobolds that have been working for too long
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

            // Update the task file
            drake.UpdateTasksFile();

            await Task.CompletedTask;
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
