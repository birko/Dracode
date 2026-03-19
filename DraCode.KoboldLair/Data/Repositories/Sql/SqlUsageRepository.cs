using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using Birko.Configuration;
using DraCode.KoboldLair.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQLite repository for LLM usage records.
    /// Stores per-call token usage and cost data for tracking and budgeting.
    /// </summary>
    public class SqlUsageRepository
    {
        private readonly AsyncSqLiteModelRepository<UsageRecordEntity> _repository;
        private readonly ILogger _logger;

        public SqlUsageRepository(string dbPath, ILogger logger)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<UsageRecordEntity>();
            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            _repository.SetSettings(new PasswordSettings(dbDir, dbFile));
        }

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            _logger.LogInformation("SqlUsageRepository initialized");
        }

        public async Task RecordUsageAsync(UsageRecordEntity record)
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
                    RequestCount = g.Count(),
                    TotalPromptTokens = g.Sum(r => r.PromptTokens),
                    TotalCompletionTokens = g.Sum(r => r.CompletionTokens),
                    TotalTokens = g.Sum(r => r.TotalTokens),
                    TotalCostUsd = g.Sum(r => r.EstimatedCostUsd)
                })
                .OrderByDescending(s => s.TotalCostUsd)
                .ToList();
        }

        /// <summary>
        /// Gets usage for a specific project within a time range.
        /// </summary>
        public async Task<ProjectUsageSummary> GetUsageByProjectAsync(string projectId, DateTime from, DateTime to)
        {
            var records = await _repository.ReadAsync(
                filter: e => e.ProjectId == projectId && e.RecordedAt >= from && e.RecordedAt <= to,
                orderBy: null, limit: null, offset: null);

            var recordList = records.ToList();
            return new ProjectUsageSummary
            {
                ProjectId = projectId,
                RequestCount = recordList.Count,
                TotalPromptTokens = recordList.Sum(r => r.PromptTokens),
                TotalCompletionTokens = recordList.Sum(r => r.CompletionTokens),
                TotalTokens = recordList.Sum(r => r.TotalTokens),
                TotalCostUsd = recordList.Sum(r => r.EstimatedCostUsd),
                ByProvider = recordList
                    .GroupBy(r => r.Provider)
                    .Select(g => new ProviderUsageSummary
                    {
                        Provider = g.Key,
                        RequestCount = g.Count(),
                        TotalPromptTokens = g.Sum(r => r.PromptTokens),
                        TotalCompletionTokens = g.Sum(r => r.CompletionTokens),
                        TotalTokens = g.Sum(r => r.TotalTokens),
                        TotalCostUsd = g.Sum(r => r.EstimatedCostUsd)
                    })
                    .ToList()
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
    }

    public class ProviderUsageSummary
    {
        public string Provider { get; set; } = "";
        public int RequestCount { get; set; }
        public int TotalPromptTokens { get; set; }
        public int TotalCompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public double TotalCostUsd { get; set; }
    }

    public class ProjectUsageSummary
    {
        public string ProjectId { get; set; } = "";
        public int RequestCount { get; set; }
        public int TotalPromptTokens { get; set; }
        public int TotalCompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public double TotalCostUsd { get; set; }
        public List<ProviderUsageSummary> ByProvider { get; set; } = new();
    }
}
