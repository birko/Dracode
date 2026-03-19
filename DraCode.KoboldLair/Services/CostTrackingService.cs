using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Tracks LLM API usage costs, persists records, and enforces budgets.
    /// </summary>
    public class CostTrackingService
    {
        private readonly SqlUsageRepository? _repository;
        private readonly CostTrackingConfiguration _config;
        private readonly ILogger _logger;
        private readonly Dictionary<string, ProviderPricing> _pricingLookup;

        public CostTrackingService(
            CostTrackingConfiguration config,
            ILogger<CostTrackingService> logger,
            SqlUsageRepository? repository = null)
        {
            _config = config;
            _logger = logger;
            _repository = repository;

            // Build a lookup: "provider:model" -> pricing, with "provider:*" as fallback
            _pricingLookup = new Dictionary<string, ProviderPricing>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in config.Pricing)
            {
                var key = $"{p.Provider}:{p.Model}";
                _pricingLookup[key] = p;
            }
        }

        /// <summary>
        /// Records a usage event with cost calculation and persistence.
        /// </summary>
        public async Task RecordUsageAsync(UsageRecord record)
        {
            if (!_config.Enabled) return;

            var cost = CalculateCost(record.Provider, record.Model, record.PromptTokens, record.CompletionTokens);

            _logger.LogDebug("Usage: {Provider}/{Model} | {PromptTokens}+{CompletionTokens} tokens | ${Cost:F6}",
                record.Provider, record.Model, record.PromptTokens, record.CompletionTokens, cost);

            if (_repository != null)
            {
                var entity = new UsageRecordEntity
                {
                    Provider = record.Provider,
                    Model = record.Model,
                    PromptTokens = record.PromptTokens,
                    CompletionTokens = record.CompletionTokens,
                    TotalTokens = record.PromptTokens + record.CompletionTokens,
                    EstimatedCostUsd = cost,
                    ProjectId = record.ProjectId,
                    TaskId = record.TaskId,
                    AgentType = record.AgentType,
                    CallerContext = record.CallerContext,
                    RecordedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _repository.RecordUsageAsync(entity);
            }
        }

        /// <summary>
        /// Calculates estimated cost for a given usage.
        /// </summary>
        public double CalculateCost(string provider, string model, int promptTokens, int completionTokens)
        {
            // Try exact match first, then wildcard
            var pricing = FindPricing(provider, model);
            if (pricing == null) return 0;

            var inputCost = (promptTokens / 1_000_000.0) * pricing.InputPricePerMillionTokens;
            var outputCost = (completionTokens / 1_000_000.0) * pricing.OutputPricePerMillionTokens;
            return inputCost + outputCost;
        }

        /// <summary>
        /// Checks budget status for optional project scope.
        /// </summary>
        public async Task<BudgetStatus> CheckBudgetAsync(string? projectId = null)
        {
            var budget = _config.Budget;
            if (_repository == null)
                return new BudgetStatus(true, false, 0, 0, "none");

            // Check daily budget
            if (budget.DailyBudgetUsd > 0)
            {
                var today = DateTime.UtcNow.Date;
                var dailySpend = await _repository.GetTotalSpendAsync(today, today.AddDays(1));
                var threshold = budget.DailyBudgetUsd * (budget.WarningThresholdPercent / 100.0);

                if (dailySpend >= budget.DailyBudgetUsd)
                    return new BudgetStatus(false, true, dailySpend, budget.DailyBudgetUsd, "daily");
                if (dailySpend >= threshold)
                    return new BudgetStatus(true, true, dailySpend, budget.DailyBudgetUsd, "daily");
            }

            // Check monthly budget
            if (budget.MonthlyBudgetUsd > 0)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1);
                var monthlySpend = await _repository.GetTotalSpendAsync(monthStart, monthEnd);
                var threshold = budget.MonthlyBudgetUsd * (budget.WarningThresholdPercent / 100.0);

                if (monthlySpend >= budget.MonthlyBudgetUsd)
                    return new BudgetStatus(false, true, monthlySpend, budget.MonthlyBudgetUsd, "monthly");
                if (monthlySpend >= threshold)
                    return new BudgetStatus(true, true, monthlySpend, budget.MonthlyBudgetUsd, "monthly");
            }

            // Check project budget
            if (budget.ProjectBudgetUsd > 0 && !string.IsNullOrEmpty(projectId))
            {
                var projectSpend = await _repository.GetProjectSpendAsync(projectId);
                var threshold = budget.ProjectBudgetUsd * (budget.WarningThresholdPercent / 100.0);

                if (projectSpend >= budget.ProjectBudgetUsd)
                    return new BudgetStatus(false, true, projectSpend, budget.ProjectBudgetUsd, "project");
                if (projectSpend >= threshold)
                    return new BudgetStatus(true, true, projectSpend, budget.ProjectBudgetUsd, "project");
            }

            return new BudgetStatus(true, false, 0, 0, "none");
        }

        /// <summary>
        /// Gets usage summary by provider for a time range.
        /// </summary>
        public async Task<List<ProviderUsageSummary>> GetUsageSummaryAsync(DateTime from, DateTime to)
        {
            if (_repository == null) return new();
            return await _repository.GetUsageByProviderAsync(from, to);
        }

        /// <summary>
        /// Gets usage for a specific project.
        /// </summary>
        public async Task<ProjectUsageSummary?> GetProjectUsageAsync(string projectId, DateTime from, DateTime to)
        {
            if (_repository == null) return null;
            return await _repository.GetUsageByProjectAsync(projectId, from, to);
        }

        private ProviderPricing? FindPricing(string provider, string model)
        {
            // Try exact match: "openai:gpt-4o"
            if (_pricingLookup.TryGetValue($"{provider}:{model}", out var exact))
                return exact;

            // Try wildcard match: "openai:*"
            if (_pricingLookup.TryGetValue($"{provider}:*", out var wildcard))
                return wildcard;

            return null;
        }
    }

    /// <summary>
    /// Record of a single LLM API call for usage tracking.
    /// </summary>
    public record UsageRecord(
        string Provider,
        string Model,
        int PromptTokens,
        int CompletionTokens,
        string? ProjectId = null,
        string? TaskId = null,
        string? AgentType = null,
        string? CallerContext = null);

    /// <summary>
    /// Budget check result.
    /// </summary>
    public record BudgetStatus(
        bool IsWithinBudget,
        bool IsWarning,
        double CurrentSpend,
        double BudgetLimit,
        string BudgetType);
}
