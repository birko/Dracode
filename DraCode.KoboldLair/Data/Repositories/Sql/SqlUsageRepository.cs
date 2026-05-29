using Birko.AI.Resilience.Stores;
using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using Birko.Configuration;
using DraCode.KoboldLair.Data.Entities;
using Microsoft.Extensions.Logging;
using BirkoUsageRecord = Birko.AI.Resilience.Stores.UsageRecordEntity;
using KoboldLairUsageRecord = DraCode.KoboldLair.Data.Entities.UsageRecordEntity;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQLite repository for LLM usage records.
    /// Stores per-call token usage and cost data for tracking and budgeting.
    /// </summary>
    public class SqlUsageRepository : IUsageRepository
    {
        private readonly AsyncSqLiteModelRepository<KoboldLairUsageRecord> _repository;
        private readonly ILogger _logger;

        public SqlUsageRepository(string dbPath, ILogger logger)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<KoboldLairUsageRecord>();
            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            _repository.SetSettings(new PasswordSettings(dbDir, dbFile));
        }

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            _logger.LogInformation("SqlUsageRepository initialized");
        }

        /// <summary>
        /// Records usage (KoboldLair internal version).
        /// </summary>
        public async Task RecordUsageAsync(KoboldLairUsageRecord record)
        {
            try
            {
                await _repository.CreateAsync(record);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist usage record for {Provider}", record.Provider);
            }
        }

        /// <summary>
        /// Gets total usage aggregated by provider within a time range.
        /// </summary>
        public async Task<List<ProviderUsageSummary>> GetUsageByProviderAsync(DateTime from, DateTime to)
        {
            var records = await _repository.ReadAsync(
                filter: e => e.RecordedAt >= from && e.RecordedAt <= to,
                orderBy: null, limit: null, offset: null);

            return records
                .GroupBy(r => r.Provider)
                .Select(g => new ProviderUsageSummary
                {
                    Provider = g.Key,
                    TotalRequests = g.Count(),
                    TotalTokens = g.Sum(r => r.TotalTokens),
                    TotalCostUsd = g.Sum(r => r.EstimatedCostUsd)
                })
                .OrderByDescending(s => s.TotalCostUsd)
                .ToList();
        }

        /// <summary>
        /// Gets usage for a specific project within a time range.
        /// </summary>
        public async Task<ProjectUsageSummary?> GetUsageByProjectAsync(string projectId, DateTime from, DateTime to)
        {
            var records = await _repository.ReadAsync(
                filter: e => e.ProjectId == projectId && e.RecordedAt >= from && e.RecordedAt <= to,
                orderBy: null, limit: null, offset: null);

            var recordList = records.ToList();
            if (recordList.Count == 0)
                return null;

            return new ProjectUsageSummary
            {
                ProjectId = projectId,
                TotalRequests = recordList.Count,
                TotalTokens = recordList.Sum(r => r.TotalTokens),
                TotalCostUsd = recordList.Sum(r => r.EstimatedCostUsd)
            };
        }

        /// <summary>
        /// Gets total spend for a time range, optionally filtered by provider.
        /// </summary>
        public async Task<double> GetTotalSpendAsync(DateTime from, DateTime to, string? provider = null)
        {
            var records = await _repository.ReadAsync(
                filter: e => e.RecordedAt >= from && e.RecordedAt <= to
                    && (provider == null || e.Provider == provider),
                orderBy: null, limit: null, offset: null);

            return records.Sum(r => r.EstimatedCostUsd);
        }

        /// <summary>
        /// Gets total spend for a specific project (all time).
        /// </summary>
        public async Task<double> GetProjectSpendAsync(string projectId)
        {
            var records = await _repository.ReadAsync(
                filter: e => e.ProjectId == projectId,
                orderBy: null, limit: null, offset: null);

            return records.Sum(r => r.EstimatedCostUsd);
        }

        #region IUsageRepository Implementation

        async Task IUsageRepository.RecordUsageAsync(BirkoUsageRecord entity)
        {
            var koboldLairRecord = new KoboldLairUsageRecord
            {
                Provider = entity.Provider,
                Model = entity.Model,
                PromptTokens = entity.PromptTokens,
                CompletionTokens = entity.CompletionTokens,
                TotalTokens = entity.TotalTokens,
                EstimatedCostUsd = entity.EstimatedCostUsd,
                ProjectId = entity.ProjectId,
                TaskId = entity.TaskId,
                AgentType = entity.AgentType,
                CallerContext = entity.CallerContext,
                RecordedAt = entity.RecordedAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
            await RecordUsageAsync(koboldLairRecord);
        }

        async Task<double> IUsageRepository.GetTotalSpendAsync(DateTime from, DateTime to)
        {
            return await GetTotalSpendAsync(from, to, provider: null);
        }

        #endregion
    }
}
