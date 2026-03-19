using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Services;
using System.Text.Json;

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

        public MetricsCommandHandler(
            ProjectService projectService,
            CostTrackingService? costTracker = null,
            ProviderRateLimiter? rateLimiter = null)
        {
            _projectService = projectService;
            _costTracker = costTracker;
            _rateLimiter = rateLimiter;
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
    }
}
