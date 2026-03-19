using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using System.Text.Json;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Server.Services.CommandHandlers
{
    /// <summary>
    /// Handles WebSocket commands for agent performance metrics and cost data.
    /// </summary>
    public class MetricsCommandHandler
    {
        private readonly CostTrackingService? _costTracker;
        private readonly ProviderRateLimiter? _rateLimiter;
        private readonly ProjectService _projectService;
        private readonly DrakeFactory? _drakeFactory;
        private readonly KoboldPlanService? _planService;

        public MetricsCommandHandler(
            ProjectService projectService,
            CostTrackingService? costTracker = null,
            ProviderRateLimiter? rateLimiter = null,
            DrakeFactory? drakeFactory = null,
            KoboldPlanService? planService = null)
        {
            _projectService = projectService;
            _costTracker = costTracker;
            _rateLimiter = rateLimiter;
            _drakeFactory = drakeFactory;
            _planService = planService;
        }

        /// <summary>
        /// Gets aggregated metrics for the UI dashboard.
        /// </summary>
        public async Task<object> GetMetricsAsync(JsonElement? data)
        {
            var hours = 24;
            if (data.HasValue && data.Value.TryGetProperty("timeRangeHours", out var h))
                hours = h.GetInt32();

            var from = DateTime.UtcNow.AddHours(-hours);
            var to = DateTime.UtcNow;

            // Get provider usage summary
            var providerSummary = _costTracker != null
                ? await _costTracker.GetUsageSummaryAsync(from, to)
                : new List<ProviderUsageSummary>();

            var totalRequests = providerSummary.Sum(s => s.RequestCount);
            var totalTokens = providerSummary.Sum(s => s.TotalTokens);
            var totalCost = providerSummary.Sum(s => s.TotalCostUsd);

            // Get per-project usage
            var projects = _projectService.GetAllProjects();
            var projectMetrics = new List<object>();

            if (_costTracker != null)
            {
                foreach (var project in projects.Take(20)) // Limit to avoid heavy queries
                {
                    var usage = await _costTracker.GetProjectUsageAsync(project.Id, from, to);
                    if (usage != null && usage.RequestCount > 0)
                    {
                        projectMetrics.Add(new
                        {
                            projectId = project.Id,
                            projectName = project.Name,
                            requests = usage.RequestCount,
                            totalTokens = usage.TotalTokens,
                            promptTokens = usage.TotalPromptTokens,
                            completionTokens = usage.TotalCompletionTokens,
                            costUsd = usage.TotalCostUsd
                        });
                    }
                }
            }

            // Get budget status
            object? budgetStatus = null;
            if (_costTracker != null)
            {
                var budget = await _costTracker.CheckBudgetAsync();
                budgetStatus = new
                {
                    isWithinBudget = budget.IsWithinBudget,
                    isWarning = budget.IsWarning,
                    currentSpend = budget.CurrentSpend,
                    budgetLimit = budget.BudgetLimit,
                    budgetType = budget.BudgetType
                };
            }

            // Get rate limit status
            var rateLimitStatus = _rateLimiter?.GetAllStatuses()
                .Select(kv => new
                {
                    provider = kv.Key,
                    requestsThisMinute = kv.Value.RequestsThisMinute,
                    tokensThisMinute = kv.Value.TokensThisMinute,
                    requestsToday = kv.Value.RequestsToday,
                    tokensToday = kv.Value.TokensToday,
                    rpmLimit = kv.Value.RequestsPerMinuteLimit,
                    tpmLimit = kv.Value.TokensPerMinuteLimit,
                    rpdLimit = kv.Value.RequestsPerDayLimit,
                    tpdLimit = kv.Value.TokensPerDayLimit
                }).ToList();

            // Get daily breakdown for the last 7 days
            var dailyBreakdown = new List<object>();
            if (_costTracker != null)
            {
                for (int i = 0; i < 7; i++)
                {
                    var dayStart = DateTime.UtcNow.Date.AddDays(-i);
                    var dayEnd = dayStart.AddDays(1);
                    var daySummary = await _costTracker.GetUsageSummaryAsync(dayStart, dayEnd);
                    dailyBreakdown.Add(new
                    {
                        date = dayStart.ToString("yyyy-MM-dd"),
                        requests = daySummary.Sum(s => s.RequestCount),
                        tokens = daySummary.Sum(s => s.TotalTokens),
                        costUsd = daySummary.Sum(s => s.TotalCostUsd)
                    });
                }
            }

            return new
            {
                timeRangeHours = hours,
                summary = new
                {
                    totalRequests,
                    totalTokens,
                    totalCost,
                    avgTokensPerRequest = totalRequests > 0 ? totalTokens / totalRequests : 0
                },
                byProvider = providerSummary.Select(s => new
                {
                    provider = s.Provider,
                    requests = s.RequestCount,
                    promptTokens = s.TotalPromptTokens,
                    completionTokens = s.TotalCompletionTokens,
                    totalTokens = s.TotalTokens,
                    costUsd = s.TotalCostUsd
                }),
                byProject = projectMetrics,
                dailyBreakdown,
                budget = budgetStatus,
                rateLimits = rateLimitStatus
            };
        }

        /// <summary>
        /// Gets task comparison data for a project — shows all tasks with execution metrics side-by-side.
        /// </summary>
        public async Task<object> GetComparisonAsync(JsonElement? data)
        {
            string? projectId = null;
            if (data.HasValue && data.Value.TryGetProperty("projectId", out var pid))
                projectId = pid.GetString();

            if (string.IsNullOrEmpty(projectId))
                return new { error = "projectId is required" };

            var project = _projectService.GetProject(projectId);
            if (project == null)
                return new { error = $"Project '{projectId}' not found" };

            var tasks = new List<object>();

            // Get tasks from Drake (in-memory) or fall back to files
            if (_drakeFactory != null)
            {
                var drakes = _drakeFactory.GetDrakesForProject(project.Id);
                foreach (var (drake, _) in drakes)
                {
                    foreach (var task in drake.GetAllTasks())
                    {
                        var planMetrics = await LoadPlanMetricsAsync(task, project.Id);
                        tasks.Add(BuildTaskComparison(task, planMetrics));
                    }
                }

                // Fall back to file if no Drakes
                if (drakes.Count == 0)
                {
                    tasks.AddRange(await LoadTasksFromFilesAsync(project));
                }
            }
            else
            {
                tasks.AddRange(await LoadTasksFromFilesAsync(project));
            }

            // Group by status for summary
            var taskList = tasks.ToList();

            return new
            {
                projectId = project.Id,
                projectName = project.Name,
                totalTasks = taskList.Count,
                tasks = taskList
            };
        }

        private async Task<List<object>> LoadTasksFromFilesAsync(DraCode.KoboldLair.Models.Projects.Project project)
        {
            var result = new List<object>();
            foreach (var (area, filePath) in project.Paths.TaskFiles)
            {
                var tracker = new TaskTracker();
                tracker.LoadFromFile(filePath);
                foreach (var task in tracker.GetAllTasks())
                {
                    var planMetrics = await LoadPlanMetricsAsync(task, project.Id);
                    result.Add(BuildTaskComparison(task, planMetrics));
                }
            }
            return result;
        }

        private async Task<PlanMetrics?> LoadPlanMetricsAsync(TaskRecord task, string projectId)
        {
            if (_planService == null) return null;

            try
            {
                var plan = await _planService.LoadPlanAsync(projectId, task.Id);
                if (plan == null) return null;

                var metrics = plan.GetAggregatedMetrics();
                return new PlanMetrics
                {
                    TotalSteps = metrics.TotalSteps,
                    CompletedSteps = metrics.CompletedSteps,
                    FailedSteps = metrics.FailedSteps,
                    SkippedSteps = metrics.SkippedSteps,
                    TotalIterations = metrics.TotalIterations,
                    TotalTokens = metrics.TotalEstimatedTokens,
                    DurationSeconds = metrics.TotalDurationSeconds,
                    SuccessRate = metrics.SuccessRate,
                    AvgIterationsPerStep = metrics.AverageIterationsPerStep,
                    ReflectionCount = plan.Reflections?.Count ?? 0,
                    EscalationCount = plan.Escalations?.Count ?? 0
                };
            }
            catch
            {
                return null;
            }
        }

        private static object BuildTaskComparison(TaskRecord task, PlanMetrics? plan)
        {
            return new
            {
                id = task.Id,
                description = task.Task.Length > 120 ? task.Task[..120] + "..." : task.Task,
                status = task.Status.ToString(),
                priority = task.Priority.ToString(),
                agentType = task.AssignedAgent,
                provider = task.Provider ?? "unknown",
                retryCount = task.RetryCount,
                errorMessage = task.ErrorMessage,
                outputFileCount = task.OutputFiles?.Count ?? 0,
                commitSha = task.CommitSha?[..Math.Min(7, task.CommitSha?.Length ?? 0)],
                commitFailed = task.CommitFailed,
                dependencyCount = task.Dependencies?.Count ?? 0,
                plan = plan != null ? new
                {
                    plan.TotalSteps,
                    plan.CompletedSteps,
                    plan.FailedSteps,
                    plan.SkippedSteps,
                    plan.TotalIterations,
                    plan.TotalTokens,
                    plan.DurationSeconds,
                    plan.SuccessRate,
                    plan.AvgIterationsPerStep,
                    plan.ReflectionCount,
                    plan.EscalationCount
                } : null
            };
        }

        private class PlanMetrics
        {
            public int TotalSteps { get; set; }
            public int CompletedSteps { get; set; }
            public int FailedSteps { get; set; }
            public int SkippedSteps { get; set; }
            public int TotalIterations { get; set; }
            public int TotalTokens { get; set; }
            public double DurationSeconds { get; set; }
            public double SuccessRate { get; set; }
            public double AvgIterationsPerStep { get; set; }
            public int ReflectionCount { get; set; }
            public int EscalationCount { get; set; }
        }
    }
}
