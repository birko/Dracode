using Birko.Data.SQL.Repositories;
using DraCode.KoboldLair.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// The budgets an admin set (TASK-163), one row in <c>budget_settings</c> on the shared database. None until the
    /// first change, so appsettings holds until then.
    /// </summary>
    public class SqlBudgetRepository
    {
        private readonly AsyncSqLiteModelRepository<BudgetSettingsEntity> _repository = new();
        private readonly ILogger? _logger;

        public SqlBudgetRepository(string dbPath, ILogger? logger = null)
        {
            _logger = logger;
            _repository.SetSettings(SqliteDatabaseSettings.For(dbPath));
        }

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            _logger?.LogInformation("SQLite budget repository initialized");
        }

        /// <summary>The stored budgets, or null when nobody has changed them.</summary>
        public async Task<BudgetSettingsEntity?> GetAsync() =>
            (await _repository.ReadAsync(CancellationToken.None)).OrderByDescending(e => e.UpdatedAt).FirstOrDefault();

        public async Task SaveAsync(double daily, double monthly, double project, double threshold, string? updatedBy)
        {
            var existing = await GetAsync();
            var row = existing ?? new BudgetSettingsEntity { CreatedAt = DateTime.UtcNow };
            row.DailyBudgetUsd = daily;
            row.MonthlyBudgetUsd = monthly;
            row.ProjectBudgetUsd = project;
            row.WarningThresholdPercent = threshold;
            row.UpdatedBy = updatedBy;
            row.UpdatedAt = DateTime.UtcNow;
            if (existing != null) await _repository.UpdateAsync(row);
            else await _repository.CreateAsync(row);
        }
    }
}
