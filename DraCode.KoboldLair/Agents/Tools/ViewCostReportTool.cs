using System.Text;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Dragon tool for viewing LLM cost and usage reports.
    /// </summary>
    public class ViewCostReportTool : Tool
    {
        private readonly CostTrackingService _costTracker;
        private readonly ProviderRateLimiter? _rateLimiter;

        public ViewCostReportTool(CostTrackingService costTracker, ProviderRateLimiter? rateLimiter = null)
        {
            _costTracker = costTracker;
            _rateLimiter = rateLimiter;
        }

        public override string Name => "view_cost_report";

        public override string Description =>
            "View LLM usage costs and rate limit status. Actions: 'summary' (today's usage by provider), " +
            "'daily' (last 7 days), 'project' (usage for a specific project), 'budget' (budget status), " +
            "'rate_limits' (current rate limit counters).";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform",
                    @enum = new[] { "summary", "daily", "project", "budget", "rate_limits" }
                },
                project = new
                {
                    type = "string",
                    description = "Project ID or name (for 'project' action)"
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var a) ? a?.ToString() ?? "summary" : "summary";

            return action.ToLowerInvariant() switch
            {
                "summary" => ExecuteSummaryAsync().GetAwaiter().GetResult(),
                "daily" => ExecuteDailyAsync().GetAwaiter().GetResult(),
                "project" => ExecuteProjectAsync(input).GetAwaiter().GetResult(),
                "budget" => ExecuteBudgetAsync().GetAwaiter().GetResult(),
                "rate_limits" => ExecuteRateLimits(),
                _ => $"Unknown action: {action}. Use: summary, daily, project, budget, rate_limits"
            };
        }

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var a) ? a?.ToString() ?? "summary" : "summary";

            return action.ToLowerInvariant() switch
            {
                "summary" => await ExecuteSummaryAsync(),
                "daily" => await ExecuteDailyAsync(),
                "project" => await ExecuteProjectAsync(input),
                "budget" => await ExecuteBudgetAsync(),
                "rate_limits" => ExecuteRateLimits(),
                _ => $"Unknown action: {action}. Use: summary, daily, project, budget, rate_limits"
            };
        }

        private async Task<string> ExecuteSummaryAsync()
        {
            var today = DateTime.UtcNow.Date;
            var summary = await _costTracker.GetUsageSummaryAsync(today, today.AddDays(1));

            if (summary.Count == 0)
                return "No usage recorded today.";

            var sb = new StringBuilder();
            sb.AppendLine("# Today's Usage Summary\n");
            sb.AppendLine("| Provider | Requests | Prompt Tokens | Completion Tokens | Total Tokens | Est. Cost |");
            sb.AppendLine("|----------|----------|---------------|-------------------|--------------|-----------|");

            var totalCost = 0.0;
            var totalTokens = 0;
            foreach (var s in summary)
            {
                sb.AppendLine($"| {s.Provider} | {s.RequestCount} | {s.TotalPromptTokens:N0} | {s.TotalCompletionTokens:N0} | {s.TotalTokens:N0} | ${s.TotalCostUsd:F4} |");
                totalCost += s.TotalCostUsd;
                totalTokens += s.TotalTokens;
            }

            sb.AppendLine($"| **Total** | **{summary.Sum(s => s.RequestCount)}** | | | **{totalTokens:N0}** | **${totalCost:F4}** |");
            return sb.ToString();
        }

        private async Task<string> ExecuteDailyAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Last 7 Days Usage\n");
            sb.AppendLine("| Date | Requests | Tokens | Est. Cost |");
            sb.AppendLine("|------|----------|--------|-----------|");

            var totalCost = 0.0;
            for (int i = 0; i < 7; i++)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var daySummary = await _costTracker.GetUsageSummaryAsync(date, date.AddDays(1));
                var dayRequests = daySummary.Sum(s => s.RequestCount);
                var dayTokens = daySummary.Sum(s => s.TotalTokens);
                var dayCost = daySummary.Sum(s => s.TotalCostUsd);
                totalCost += dayCost;

                if (dayRequests > 0)
                    sb.AppendLine($"| {date:yyyy-MM-dd} | {dayRequests} | {dayTokens:N0} | ${dayCost:F4} |");
                else
                    sb.AppendLine($"| {date:yyyy-MM-dd} | 0 | 0 | $0.0000 |");
            }

            sb.AppendLine($"\n**7-day total**: ${totalCost:F4}");
            return sb.ToString();
        }

        private async Task<string> ExecuteProjectAsync(Dictionary<string, object> input)
        {
            if (!input.TryGetValue("project", out var proj) || string.IsNullOrEmpty(proj?.ToString()))
                return "Error: 'project' is required for the 'project' action.";

            var projectId = proj.ToString()!;
            var usage = await _costTracker.GetProjectUsageAsync(projectId, DateTime.MinValue, DateTime.UtcNow);
            if (usage == null || usage.RequestCount == 0)
                return $"No usage recorded for project '{projectId}'.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Project Usage: {projectId}\n");
            sb.AppendLine($"- **Total Requests**: {usage.RequestCount}");
            sb.AppendLine($"- **Total Tokens**: {usage.TotalTokens:N0} (prompt: {usage.TotalPromptTokens:N0}, completion: {usage.TotalCompletionTokens:N0})");
            sb.AppendLine($"- **Estimated Cost**: ${usage.TotalCostUsd:F4}");

            if (usage.ByProvider.Count > 1)
            {
                sb.AppendLine("\n### By Provider\n");
                foreach (var p in usage.ByProvider)
                {
                    sb.AppendLine($"- **{p.Provider}**: {p.RequestCount} requests, {p.TotalTokens:N0} tokens, ${p.TotalCostUsd:F4}");
                }
            }

            return sb.ToString();
        }

        private async Task<string> ExecuteBudgetAsync()
        {
            var status = await _costTracker.CheckBudgetAsync();
            var sb = new StringBuilder();
            sb.AppendLine("# Budget Status\n");

            if (status.BudgetType == "none" && status.IsWithinBudget)
            {
                sb.AppendLine("No budget limits configured.");
                sb.AppendLine("\nConfigure in `appsettings.json` under `KoboldLair.CostTracking.Budget`:");
                sb.AppendLine("- `DailyBudgetUsd`: Daily spending limit");
                sb.AppendLine("- `MonthlyBudgetUsd`: Monthly spending limit");
                sb.AppendLine("- `ProjectBudgetUsd`: Per-project spending limit");
            }
            else
            {
                var icon = status.IsWithinBudget ? (status.IsWarning ? "Warning" : "OK") : "EXCEEDED";
                sb.AppendLine($"- **Status**: {icon}");
                sb.AppendLine($"- **Scope**: {status.BudgetType}");
                sb.AppendLine($"- **Spent**: ${status.CurrentSpend:F4}");
                sb.AppendLine($"- **Budget**: ${status.BudgetLimit:F4}");
                sb.AppendLine($"- **Remaining**: ${Math.Max(0, status.BudgetLimit - status.CurrentSpend):F4}");
            }

            return sb.ToString();
        }

        private string ExecuteRateLimits()
        {
            if (_rateLimiter == null)
                return "Rate limiting is not enabled.";

            var statuses = _rateLimiter.GetAllStatuses();
            if (statuses.Count == 0)
                return "No rate limit configurations found.";

            var sb = new StringBuilder();
            sb.AppendLine("# Rate Limit Status\n");

            foreach (var (provider, status) in statuses)
            {
                sb.AppendLine($"### {provider}");
                if (status.RequestsPerMinuteLimit > 0)
                    sb.AppendLine($"- RPM: {status.RequestsThisMinute}/{status.RequestsPerMinuteLimit}");
                if (status.TokensPerMinuteLimit > 0)
                    sb.AppendLine($"- TPM: {status.TokensThisMinute:N0}/{status.TokensPerMinuteLimit:N0}");
                if (status.RequestsPerDayLimit > 0)
                    sb.AppendLine($"- RPD: {status.RequestsToday}/{status.RequestsPerDayLimit}");
                if (status.TokensPerDayLimit > 0)
                    sb.AppendLine($"- TPD: {status.TokensToday:N0}/{status.TokensPerDayLimit:N0}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
