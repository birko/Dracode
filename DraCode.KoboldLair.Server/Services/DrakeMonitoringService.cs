using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Orchestrators;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors Drake supervisors and their Kobold workers.
    /// Runs periodically to check Kobold work progress and update task statuses.
    /// Detects and handles stuck Kobolds that exceed the configured timeout.
    /// </summary>
    public class DrakeMonitoringService : PeriodicBackgroundService
    {
        private readonly ILogger<DrakeMonitoringService> _logger;
        private readonly DrakeFactory _drakeFactory;
        private readonly TimeSpan _stuckKoboldTimeout;

        // Throttle concurrent Drake monitoring to avoid overwhelming I/O
        private readonly SemaphoreSlim _drakeThrottle;
        private const int MaxConcurrentDrakes = 5;

        protected override ILogger Logger => _logger;

        /// <summary>
        /// Default timeout for stuck Kobold detection (30 minutes)
        /// </summary>
        public static readonly TimeSpan DefaultStuckTimeout = TimeSpan.FromMinutes(30);

        public DrakeMonitoringService(
            ILogger<DrakeMonitoringService> logger,
            DrakeFactory drakeFactory,
            int monitoringIntervalSeconds = 60,
            int stuckKoboldTimeoutMinutes = 30)
            : base(TimeSpan.FromSeconds(monitoringIntervalSeconds))
        {
            _logger = logger;
            _drakeFactory = drakeFactory;
            _stuckKoboldTimeout = TimeSpan.FromMinutes(stuckKoboldTimeoutMinutes);
            _drakeThrottle = new SemaphoreSlim(MaxConcurrentDrakes, MaxConcurrentDrakes);
        }

        protected override async Task ExecuteCycleAsync(CancellationToken stoppingToken)
        {
            var drakes = _drakeFactory.GetAllDrakes();

            if (drakes.Count == 0)
            {
                _logger.LogDebug("No Drakes to monitor");
                return;
            }

            _logger.LogDebug("Monitoring {Count} Drake(s)", drakes.Count);

            var monitoringTasks = drakes.Select(async drake =>
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                await _drakeThrottle.WaitAsync(stoppingToken);
                try
                {
                    await MonitorSingleDrakeAsync(drake, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring Drake");
                }
                finally
                {
                    _drakeThrottle.Release();
                }
            });

            await Task.WhenAll(monitoringTasks);
        }

        private async Task MonitorSingleDrakeAsync(Drake drake, CancellationToken cancellationToken)
        {
            var projectInfo = drake.ProjectId ?? "unknown project";

            await drake.MonitorTasksAsync();

            var stats = drake.GetStatistics();

            _logger.LogDebug(
                "Drake stats for {ProjectId} | Kobolds: {TotalKobolds} (Working: {Working}, Done: {Done}) | Tasks: {TotalTasks} (Working: {WorkingTasks}, Done: {DoneTasks})",
                projectInfo,
                stats.TotalKobolds,
                stats.WorkingKobolds,
                stats.DoneKobolds,
                stats.TotalTasks,
                stats.WorkingTasks,
                stats.DoneTasks
            );

            // Check for stuck Kobolds
            if (stats.WorkingKobolds > 0)
            {
                var stuckKobolds = await drake.HandleStuckKoboldsAsync(_stuckKoboldTimeout);

                if (stuckKobolds.Count > 0)
                {
                    _logger.LogWarning(
                        "Project {ProjectId}: Handled {Count} stuck Kobold(s) (timeout: {Timeout} min)",
                        projectInfo,
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
                    _logger.LogInformation(
                        "Project {ProjectId}: Unsummoned {Count} completed Kobold(s)",
                        projectInfo, unsummoned);
                }
            }

            try
            {
                await drake.UpdateTasksFileAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to save task state for Drake in project {ProjectId}. " +
                    "Task state may be inconsistent - will retry next cycle.",
                    projectInfo);
            }
        }
    }
}
