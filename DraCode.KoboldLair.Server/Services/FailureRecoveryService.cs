using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that automatically retries failed tasks with transient errors.
    /// Uses exponential backoff and circuit breaker pattern to handle provider outages.
    /// </summary>
    public class FailureRecoveryService : BackgroundService
    {
        private readonly ILogger<FailureRecoveryService> _logger;
        private readonly ProjectService _projectService;
        private readonly DrakeFactory _drakeFactory;
        private readonly ProviderCircuitBreaker _circuitBreaker;
        private readonly TimeSpan _checkInterval;
        private readonly int _maxRetryAttempts;
        private readonly TimeSpan[] _retryBackoffSchedule;
        private bool _isRunning;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new failure recovery service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="projectService">Project service for accessing projects</param>
        /// <param name="drakeFactory">Factory for accessing Drakes</param>
        /// <param name="circuitBreaker">Circuit breaker for provider health tracking</param>
        /// <param name="checkIntervalSeconds">Interval in seconds between checks (default: 300 = 5 minutes)</param>
        /// <param name="maxRetryAttempts">Maximum retry attempts (default: 5)</param>
        public FailureRecoveryService(
            ILogger<FailureRecoveryService> logger,
            ProjectService projectService,
            DrakeFactory drakeFactory,
            ProviderCircuitBreaker circuitBreaker,
            int checkIntervalSeconds = 300,
            int maxRetryAttempts = 5)
        {
            _logger = logger;
            _projectService = projectService;
            _drakeFactory = drakeFactory;
            _circuitBreaker = circuitBreaker;
            _checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
            _maxRetryAttempts = maxRetryAttempts;
            _isRunning = false;

            // Exponential backoff schedule: 1min, 2min, 5min, 15min, 30min
            _retryBackoffSchedule = new[]
            {
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(15),
                TimeSpan.FromMinutes(30)
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "🔄 Failure Recovery Service started. Check interval: {Interval}s, Max retries: {MaxRetries}",
                _checkInterval.TotalSeconds,
                _maxRetryAttempts);

            // Wait 1 minute before first run to let other services stabilize
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
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
                        _logger.LogDebug("⚠️ Previous recovery cycle still running, skipping");
                        await Task.Delay(_checkInterval, stoppingToken);
                        continue;
                    }

                    await ProcessFailedTasksAsync(stoppingToken);

                    lock (_lock)
                    {
                        _isRunning = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Failure Recovery Service stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Failure Recovery Service");
                    lock (_lock)
                    {
                        _isRunning = false;
                    }
                }

                // Wait for next cycle
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Failure Recovery Service stopped");
        }

        /// <summary>
        /// Processes all failed tasks across all projects and retries eligible ones
        /// </summary>
        private async Task ProcessFailedTasksAsync(CancellationToken cancellationToken)
        {
            var projects = _projectService.GetAllProjects()
                .Where(p => p.Status == ProjectStatus.InProgress)
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
                    task.Id[..Math.Min(8, task.Id.Length)]);
                return false;
            }

            // Check if error is transient
            if (ErrorClassifier.IsPermanent(task.ErrorMessage))
            {
                _logger.LogDebug(
                    "Skipping task {TaskId}: Permanent error - {Error}",
                    task.Id[..Math.Min(8, task.Id.Length)],
                    task.ErrorMessage.Length > 100 ? task.ErrorMessage[..100] + "..." : task.ErrorMessage);
                return false;
            }

            // Check retry count
            if (task.RetryCount >= _maxRetryAttempts)
            {
                _logger.LogDebug(
                    "Skipping task {TaskId}: Max retries ({MaxRetries}) exceeded",
                    task.Id[..Math.Min(8, task.Id.Length)],
                    _maxRetryAttempts);
                return false;
            }

            // Check if it's time to retry (based on exponential backoff)
            if (task.NextRetryAt.HasValue && now < task.NextRetryAt.Value)
            {
                var waitTime = task.NextRetryAt.Value - now;
                _logger.LogDebug(
                    "Skipping task {TaskId}: Next retry in {Minutes:F1} minutes",
                    task.Id[..Math.Min(8, task.Id.Length)],
                    waitTime.TotalMinutes);
                return false;
            }

            // Check circuit breaker
            if (!string.IsNullOrWhiteSpace(task.Provider) && !_circuitBreaker.CanRetry(task.Provider))
            {
                _logger.LogDebug(
                    "Skipping task {TaskId}: Circuit breaker open for provider {Provider}",
                    task.Id[..Math.Min(8, task.Id.Length)],
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
            var taskPreview = task.Task.Length > 60 ? task.Task[..60] + "..." : task.Task;
            
            _logger.LogInformation(
                "🔄 Retrying task {TaskId} (attempt {Attempt}/{Max})\n" +
                "  Provider: {Provider}\n" +
                "  Task: {Task}\n" +
                "  Last error: {Error}",
                task.Id[..Math.Min(8, task.Id.Length)],
                task.RetryCount + 1,
                _maxRetryAttempts,
                task.Provider ?? "unknown",
                taskPreview,
                task.ErrorMessage?.Length > 100 ? task.ErrorMessage[..100] + "..." : task.ErrorMessage ?? "unknown");

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

            // Force save task state
            await drake.SaveTasksToFileAsync();
        }
    }
}
