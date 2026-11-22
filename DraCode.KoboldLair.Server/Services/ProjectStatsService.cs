using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Options;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Server.Services;

/// <summary>
/// A project's numbers (TASK-158, FEATURE-026 D18) for the Stats tab: its tasks by status and agent type, the success
/// rate, the average time a finished task took, its LLM cost by provider and model and day by day, and its spend against
/// the project budget. Read from the task store, the plans (a task's duration runs from its first step's start, or the
/// plan's creation, to the plan's completion) and the usage records. The shape takes any set of projects, so the
/// cross-project stats (TASK-148) can reuse it.
/// </summary>
public sealed class ProjectStatsService(
    IServiceProvider services,
    KoboldPlanService plans,
    IOptions<KoboldLairConfiguration> config)
{
    public sealed record AgentCount(string AgentType, int Total, int Done, int Failed);
    public sealed record TaskStats(int Total, Dictionary<string, int> ByStatus, List<AgentCount> ByAgent,
        double? SuccessRate, double? AverageDurationSeconds, int TimedTasks);
    public sealed record ModelCost(string Provider, string Model, int Requests, long Tokens, double CostUsd);
    public sealed record DayCost(string Date, int Requests, long Tokens, double CostUsd);
    public sealed record CostStats(double TotalUsd, long TotalTokens, int Requests, List<ModelCost> ByModel, List<DayCost> ByDay);
    public sealed record BudgetStats(double? ProjectBudgetUsd, double SpentUsd, double? PercentUsed, double WarningThresholdPercent);
    public sealed record Stats(List<string> ProjectIds, TaskStats Tasks, CostStats Cost, BudgetStats Budget);

    public async Task<Stats> ForAsync(IReadOnlyList<Project> projects, CancellationToken ct = default)
    {
        var taskStore = services.GetService<ITaskRepository>();
        var usage = services.GetService<SqlUsageRepository>();

        var tasks = new List<TaskRecord>();
        var usageRecords = new List<Data.Entities.UsageRecordEntity>();
        foreach (var project in projects)
        {
            if (taskStore != null) tasks.AddRange(await taskStore.GetByProjectAsync(project.Id));
            if (usage != null) usageRecords.AddRange(await usage.GetProjectRecordsAsync(project.Id, ct));
        }

        // Every status, zero included, so a chart has the same bars for every project
        var byStatus = Enum.GetValues<TaskStatus>().ToDictionary(s => s.ToString(), s => tasks.Count(t => t.Status == s));
        var byAgent = tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.AssignedAgent) ? "unassigned" : t.AssignedAgent)
            .Select(g => new AgentCount(g.Key, g.Count(), g.Count(t => t.Status == TaskStatus.Done), g.Count(t => t.Status == TaskStatus.Failed)))
            .OrderByDescending(a => a.Total).ThenBy(a => a.AgentType)
            .ToList();
        var done = byStatus[nameof(TaskStatus.Done)];
        var failed = byStatus[nameof(TaskStatus.Failed)];
        double? successRate = done + failed > 0 ? Math.Round(100.0 * done / (done + failed), 1) : null;

        var durations = new List<double>();
        foreach (var task in tasks.Where(t => t.Status == TaskStatus.Done && t.ProjectId != null))
        {
            var plan = await plans.LoadPlanAsync(task.ProjectId!, task.Id);
            if (plan?.CompletedAt is not { } completed) continue;
            var started = plan.Steps.Where(s => s.StartedAt.HasValue).Select(s => s.StartedAt!.Value).DefaultIfEmpty(plan.CreatedAt).Min();
            var seconds = (completed - started).TotalSeconds;
            if (seconds > 0) durations.Add(seconds);
        }

        var spent = usageRecords.Sum(r => r.EstimatedCostUsd);
        var cost = new CostStats(
            Math.Round(spent, 6),
            usageRecords.Sum(r => (long)r.TotalTokens),
            usageRecords.Count,
            usageRecords.GroupBy(r => (r.Provider, r.Model))
                .Select(g => new ModelCost(g.Key.Provider, g.Key.Model, g.Count(), g.Sum(r => (long)r.TotalTokens), Math.Round(g.Sum(r => r.EstimatedCostUsd), 6)))
                .OrderByDescending(m => m.CostUsd).ThenByDescending(m => m.Tokens)
                .ToList(),
            usageRecords.GroupBy(r => r.RecordedAt.ToUniversalTime().Date)
                .OrderBy(g => g.Key)
                .Select(g => new DayCost(g.Key.ToString("yyyy-MM-dd"), g.Count(), g.Sum(r => (long)r.TotalTokens), Math.Round(g.Sum(r => r.EstimatedCostUsd), 6)))
                .ToList());

        var budgetConfig = config.Value.CostTracking.Budget;
        double? budget = budgetConfig.ProjectBudgetUsd > 0 ? budgetConfig.ProjectBudgetUsd * projects.Count : null;
        var budgetStats = new BudgetStats(budget, Math.Round(spent, 6), budget is { } b ? Math.Round(100 * spent / b, 1) : null,
            budgetConfig.WarningThresholdPercent);

        return new Stats(projects.Select(p => p.Id).ToList(),
            new TaskStats(tasks.Count, byStatus, byAgent, successRate, durations.Count > 0 ? Math.Round(durations.Average(), 1) : null, durations.Count),
            cost, budgetStats);
    }
}
