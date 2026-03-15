using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using static DraCode.KoboldLair.Server.Helpers.LogFormatHelper;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that automatically retries failed tasks with transient errors.
    /// Uses exponential backoff and circuit breaker pattern to handle provider outages.
    /// </summary>
    public class FailureRecoveryService : PeriodicBackgroundService
    {
        private readonly ILogger<FailureRecoveryService> _logger;
        private readonly ProjectService _projectService;
        private readonly DrakeFactory _drakeFactory;
        private readonly ProviderCircuitBreaker _circuitBreaker;
        private readonly int _maxRetryAttempts;
        private readonly TimeSpan[] _retryBackoffSchedule;

        protected override ILogger Logger => _logger;

        public FailureRecoveryService(
            ILogger<FailureRecoveryService> logger,
            ProjectService projectService,
            DrakeFactory drakeFactory,
            ProviderCircuitBreaker circuitBreaker,
            int checkIntervalSeconds = 300,
            int maxRetryAttempts = 5)
            : base(TimeSpan.FromSeconds(checkIntervalSeconds), initialDelay: TimeSpan.FromMinutes(1))
        {
            _logger = logger;
            _projectService = projectService;
            _drakeFactory = drakeFactory;
            _circuitBreaker = circuitBreaker;
            _maxRetryAttempts = maxRetryAttempts;

            // Exponential backoff schedule: 1min, 2min, 5min, 15min, 30min
            _retryBackoffSchedule =
            [
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(15),
                TimeSpan.FromMinutes(30)
            ];
        }

        /// <summary>
        /// Processes all failed tasks across all projects and retries eligible ones
        /// </summary>
        protected override async Task ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            var projects = _projectService.GetAllProjects()
                .Where(p => p.Status == ProjectStatus.InProgress
                            && p.ExecutionState == ProjectExecutionState.Running)
                .ToList();

            if (projects.Count == 0)
            {
                _logger.LogDebug("No in-progress projects to check for failed tasks");
                return;
            }

            _logger.LogDebug("Checking {ProjectCount} projects for failed tasks", projects.Count);

            var totalRetried = 0;
            var totalSkipped = 0;

            foreach (var project in projects)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var result = await ProcessProjectFailedTasksAsync(project, cancellationToken);
                    totalRetried += result.retried;
                    totalSkipped += result.skipped;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing failed tasks for project {ProjectId}", project.Id);
                }
            }

            if (totalRetried > 0 || totalSkipped > 0)
            {
                _logger.LogInformation(
                    "🔄 Recovery cycle complete: {Retried} tasks retried, {Skipped} tasks skipped",
                    totalRetried,
                    totalSkipped);
            }
        }

        /// <summary>
        /// Processes failed tasks for a specific project
        /// </summary>
        private async Task<(int retried, int skipped)> ProcessProjectFailedTasksAsync(
            Project project,
            CancellationToken cancellationToken)
        {
            // Get all Drakes for this project
            var drakes = _drakeFactory.GetDrakesByProject(project.Id);
            if (drakes.Count == 0)
            {
                return (0, 0);
            }

            var retriedCount = 0;
            var skippedCount = 0;

            foreach (var drake in drakes)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var failedTasks = drake.GetAllTasks()
                    .Where(t => t.Status == TaskStatus.Failed)
                    .ToList();

                foreach (var task in failedTasks)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var shouldRetry = ShouldRetryTask(task);
                    
                    if (shouldRetry)
                    {
                        await RetryTaskAsync(drake, task);
                        retriedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
            }

            return (retriedCount, skippedCount);
        }

        /// <summary>
        /// Determines if a task should be retried based on error category, retry count, and timing
        /// </summary>
        private bool ShouldRetryTask(TaskRecord task)
        {
            var now = DateTime.UtcNow;

            // Skip if no error message
            if (string.IsNullOrWhiteSpace(task.ErrorMessage))
            {
                _logger.LogDebug(
                    "Skipping task {TaskId}: No error message",
                    ShortId(task.Id));
                return false;
            }

            // Check if error is transient
            if (ErrorClassifier.IsPermanent(task.ErrorMessage))
            {
                _logger.LogDebug(
                    "Skipping task {TaskId}: Permanent error - {Error}",
                    ShortId(task.Id),
                    Truncate(task.ErrorMessage, 100));
                return false;
            }

            // Check retry count
            if (task.RetryCount >= _maxRetryAttempts)
            {
                _logger.LogDebug(
                    "Skipping task {TaskId}: Max retries ({MaxRetries}) exceeded",
                    ShortId(task.Id),
                    _maxRetryAttempts);
                return false;
            }

            // Check if it's time to retry (based on exponential backoff)
            if (task.NextRetryAt.HasValue && now < task.NextRetryAt.Value)
            {
                var waitTime = task.NextRetryAt.Value - now;
                _logger.LogDebug(
                    "Skipping task {TaskId}: Next retry in {Minutes:F1} minutes",
                    ShortId(task.Id),
                    waitTime.TotalMinutes);
                return false;
            }

            // Check circuit breaker
            if (!string.IsNullOrWhiteSpace(task.Provider) && !_circuitBreaker.CanRetry(task.Provider))
            {
                _logger.LogDebug(
                    "Skipping task {TaskId}: Circuit breaker open for provider {Provider}",
                    ShortId(task.Id),
                    task.Provider);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retries a failed task by resetting it to Unassigned status
        /// </summary>
        private async Task RetryTaskAsync(Drake drake, TaskRecord task)
        {
            var taskPreview = Truncate(task.Task);
            
            _logger.LogInformation(
                "🔄 Retrying task {TaskId} (attempt {Attempt}/{Max})\n" +
                "  Provider: {Provider}\n" +
                "  Task: {Task}\n" +
                "  Last error: {Error}",
                ShortId(task.Id),
                task.RetryCount + 1,
                _maxRetryAttempts,
                task.Provider ?? "unknown",
                taskPreview,
                Truncate(task.ErrorMessage ?? "unknown", 100));

            // Update retry metadata
            task.RetryCount++;
            task.LastRetryAttempt = DateTime.UtcNow;
            
            // Calculate next retry time using exponential backoff
            var backoffIndex = Math.Min(task.RetryCount, _retryBackoffSchedule.Length) - 1;
            task.NextRetryAt = DateTime.UtcNow + _retryBackoffSchedule[backoffIndex];

            // Reset task to Unassigned so Drake will pick it up again
            drake.UpdateTask(task, TaskStatus.Unassigned);

            // Clear error message so it doesn't show as errored while retrying
            task.ErrorMessage = $"Retry {task.RetryCount}/{_maxRetryAttempts} - Previous error: {task.ErrorMessage}";

            // Clear escalation alerts from the associated plan (if any) to prevent stale escalation history
            await drake.ClearEscalationsForTaskAsync(task.Id);

            // Force save task state
            await drake.SaveTasksToFileAsync();
        }
    }
}
