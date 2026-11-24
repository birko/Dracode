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
/// homepage's overview (TASK-148, <see cref="OverviewAsync"/>) shares its pieces.
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

    // The homepage's numbers across the caller's projects (TASK-148)
    public sealed record ProjectCounts(int Total, int Active);
    public sealed record OverviewTasks(int Total, Dictionary<string, int> ByStatus, double? SuccessRate, int DoneThisWeek, int FailedThisWeek);
    public sealed record OverviewCost(double ThisMonthUsd, long ThisMonthTokens, int ThisMonthRequests, double? MonthlyBudgetUsd, List<DayCost> ByDay);
    public sealed record ProjectTasks(string ProjectId, string Name, int Total, Dictionary<string, int> ByStatus);
    public sealed record Overview(List<string> ProjectIds, ProjectCounts Projects, OverviewTasks Tasks, OverviewCost Cost, List<ProjectTasks> ByProject);

    /// <summary>Days in <see cref="Overview"/>'s cost by day, today (UTC) included.</summary>
    public const int OverviewDays = 30;

    /// <summary>
    /// The homepage's tiles and charts over <paramref name="projects"/>: how many are active (running and not finished),
    /// tasks by status and per project, the success rate, tasks done and failed in the last 7 days (by the time their
    /// status last changed), spend this calendar month (UTC) against the monthly budget, and cost for each of the last
    /// <see cref="OverviewDays"/> days, days with no usage as zeros. No plans are read, so it stays cheap for many projects.
    /// </summary>
    public async Task<Overview> OverviewAsync(IReadOnlyList<Project> projects, DateTime utcNow, CancellationToken ct = default)
    {
        var taskStore = services.GetService<ITaskRepository>();
        var usage = services.GetService<SqlUsageRepository>();
        var statuses = Enum.GetValues<TaskStatus>();
        Dictionary<string, int> ByStatus(IEnumerable<TaskRecord> tasks) =>
            statuses.ToDictionary(s => s.ToString(), s => tasks.Count(t => t.Status == s));

        var tasks = new List<TaskRecord>();
        var usageRecords = new List<Data.Entities.UsageRecordEntity>();
        var byProject = new List<ProjectTasks>();
        foreach (var project in projects)
        {
            var projectTasks = taskStore != null ? await taskStore.GetByProjectAsync(project.Id) : [];
            tasks.AddRange(projectTasks);
            byProject.Add(new ProjectTasks(project.Id, project.Name, projectTasks.Count, ByStatus(projectTasks)));
            if (usage != null) usageRecords.AddRange(await usage.GetProjectRecordsAsync(project.Id, ct));
        }

        var byStatus = ByStatus(tasks);
        var done = byStatus[nameof(TaskStatus.Done)];
        var failed = byStatus[nameof(TaskStatus.Failed)];
        var weekAgo = utcNow.AddDays(-7);
        bool ThisWeek(TaskRecord t, TaskStatus status) => t.Status == status && t.UpdatedAt is { } at && at.ToUniversalTime() >= weekAgo;
        var overviewTasks = new OverviewTasks(tasks.Count, byStatus,
            done + failed > 0 ? Math.Round(100.0 * done / (done + failed), 1) : null,
            tasks.Count(t => ThisWeek(t, TaskStatus.Done)), tasks.Count(t => ThisWeek(t, TaskStatus.Failed)));

        var today = utcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var thisMonth = usageRecords.Where(r => r.RecordedAt.ToUniversalTime() >= monthStart).ToList();
        var perDay = usageRecords.GroupBy(r => r.RecordedAt.ToUniversalTime().Date).ToDictionary(g => g.Key, g => g.ToList());
        var byDay = Enumerable.Range(0, OverviewDays)
            .Select(i => today.AddDays(i - OverviewDays + 1))
            .Select(day => perDay.TryGetValue(day, out var records)
                ? new DayCost(day.ToString("yyyy-MM-dd"), records.Count, records.Sum(r => (long)r.TotalTokens), Math.Round(records.Sum(r => r.EstimatedCostUsd), 6))
                : new DayCost(day.ToString("yyyy-MM-dd"), 0, 0, 0))
            .ToList();
        var monthlyBudget = config.Value.CostTracking.Budget.MonthlyBudgetUsd;
        var cost = new OverviewCost(Math.Round(thisMonth.Sum(r => r.EstimatedCostUsd), 6), thisMonth.Sum(r => (long)r.TotalTokens), thisMonth.Count,
            monthlyBudget > 0 ? monthlyBudget : null, byDay);

        var active = projects.Count(p => p.ExecutionState == ProjectExecutionState.Running
            && p.Status is not (ProjectStatus.Verified or ProjectStatus.Completed or ProjectStatus.Failed));
        return new Overview(projects.Select(p => p.Id).ToList(), new ProjectCounts(projects.Count, active), overviewTasks, cost, byProject);
    }
}
