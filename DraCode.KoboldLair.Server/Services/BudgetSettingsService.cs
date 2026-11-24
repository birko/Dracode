using Birko.AI.Resilience.Configuration;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Configuration;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Services;

/// <summary>
/// Settings → Budgets (TASK-163). The live budgets are <c>KoboldLair:CostTracking:Budget</c>'s object, the one
/// <c>CostTrackingService</c> checks before every LLM call and the stats read; this service writes the stored values
/// over it at startup (a stored change wins over appsettings) and on every change, after saving them, so enforcement
/// uses a change at once. Without the SQLite store nothing can be saved, and the budgets are appsettings' alone.
/// </summary>
public sealed class BudgetSettingsService
{
    private readonly BudgetConfiguration _live;
    private readonly SqlBudgetRepository? _store;
    private readonly SqlUsageRepository? _usage;
    private readonly IProjectRepository? _projects;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public sealed record TopProject(string ProjectId, string? ProjectName, double SpentUsd);
    public sealed record Spend(double TodayUsd, double ThisMonthUsd, TopProject? TopProject);
    public sealed record Budgets(double DailyBudgetUsd, double MonthlyBudgetUsd, double ProjectBudgetUsd, double WarningThresholdPercent,
        bool Editable, Spend Spend);
    public sealed record Change(double DailyBudgetUsd, double MonthlyBudgetUsd, double ProjectBudgetUsd, double WarningThresholdPercent);

    public BudgetSettingsService(IOptions<KoboldLairConfiguration> config, SqlBudgetRepository? store, SqlUsageRepository? usage,
        IProjectRepository? projects = null)
    {
        _projects = projects;
        _live = config.Value.CostTracking.Budget;
        _store = store;
        _usage = usage;
        var stored = store?.GetAsync().GetAwaiter().GetResult();
        if (stored != null) Apply(stored.DailyBudgetUsd, stored.MonthlyBudgetUsd, stored.ProjectBudgetUsd, stored.WarningThresholdPercent);
    }

    /// <summary>Whether a change can be saved (the SQLite store is there).</summary>
    public bool Editable => _store != null;

    /// <summary>The budgets in force, with today's and this month's spend (UTC, the windows enforcement uses) and the project that has spent most.</summary>
    public async Task<Budgets> GetAsync(CancellationToken ct = default)
    {
        Spend spend = new(0, 0, null);
        if (_usage != null)
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var top = (await _usage.GetSpendByProjectAsync(ct)).OrderByDescending(p => p.Value).FirstOrDefault();
            spend = new Spend(
                Math.Round(await _usage.GetTotalSpendAsync(today, today.AddDays(1), ct: ct), 6),
                Math.Round(await _usage.GetTotalSpendAsync(monthStart, monthStart.AddMonths(1), ct: ct), 6),
                top.Key is null ? null : new TopProject(top.Key, _projects?.GetById(top.Key)?.Name, Math.Round(top.Value, 6)));
        }
        return new Budgets(_live.DailyBudgetUsd, _live.MonthlyBudgetUsd, _live.ProjectBudgetUsd, _live.WarningThresholdPercent, Editable, spend);
    }

    /// <summary>What is wrong with a change, or null: budgets are 0 (none) or more, the warning threshold 1 to 100 percent.</summary>
    public static string? Validate(Change change)
    {
        if (change.DailyBudgetUsd < 0 || change.MonthlyBudgetUsd < 0 || change.ProjectBudgetUsd < 0)
            return "A budget is 0 (no budget) or more.";
        if (change.WarningThresholdPercent is < 1 or > 100)
            return "The warning threshold is between 1 and 100 percent.";
        return null;
    }

    /// <summary>Saves a valid change, then puts it in force.</summary>
    public async Task UpdateAsync(Change change, string? updatedBy)
    {
        if (_store == null) throw new InvalidOperationException("Budgets cannot be saved: this server keeps no SQLite database.");
        await _lock.WaitAsync();
        try
        {
            await _store.SaveAsync(change.DailyBudgetUsd, change.MonthlyBudgetUsd, change.ProjectBudgetUsd, change.WarningThresholdPercent, updatedBy);
            Apply(change.DailyBudgetUsd, change.MonthlyBudgetUsd, change.ProjectBudgetUsd, change.WarningThresholdPercent);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void Apply(double daily, double monthly, double project, double threshold)
    {
        _live.DailyBudgetUsd = daily;
        _live.MonthlyBudgetUsd = monthly;
        _live.ProjectBudgetUsd = project;
        _live.WarningThresholdPercent = threshold;
    }
}
