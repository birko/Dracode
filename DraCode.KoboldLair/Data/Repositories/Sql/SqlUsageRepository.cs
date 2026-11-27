using Birko.AI.Resilience.Stores;
using System.Linq.Expressions;
using Birko.Data.SQL.Repositories;
using Birko.Data.SQL.SqLite.Stores;
using Birko.Data.Stores;
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
        private readonly StoreRepository<KoboldLairUsageRecord> _repository;
        /// <summary>The repository's store, for <c>AggregateAsync</c> where it can aggregate (TASK-123, TASK-200).</summary>
        private readonly IAsyncBulkStore<KoboldLairUsageRecord> _store;
        private readonly ILogger _logger;

        private const string Requests = "requests", Tokens = "tokens", Cost = "cost";

        /// <summary>Each total these reports use: the number of calls, their tokens and their cost.</summary>
        private static readonly IReadOnlyList<AggregateField> Totals =
        [
            new(AggregateFunction.Count, nameof(KoboldLairUsageRecord.Guid), Requests),
            new(AggregateFunction.Sum, nameof(KoboldLairUsageRecord.TotalTokens), Tokens),
            new(AggregateFunction.Sum, nameof(KoboldLairUsageRecord.EstimatedCostUsd), Cost)
        ];

        /// <summary>On the SQLite file at <paramref name="dbPath"/> (tests, and the default database).</summary>
        public SqlUsageRepository(string dbPath, ILogger logger)
            : this(DataStores.SqLite(dbPath), logger)
        {
        }

        /// <summary>On the configured backend (TASK-200).</summary>
        public SqlUsageRepository(Birko.Data.Stores.IStoreFactory factory, ILogger logger)
        {
            _repository = new StoreRepository<KoboldLairUsageRecord>(factory);
            _logger = logger;
            _store = _repository.BulkStoreOf;
        }

        /// <summary>Totals computed in the database (SQL <c>GROUP BY</c>), not over every record read into memory (TASK-123).</summary>
        private async Task<IReadOnlyList<AggregateResult>> AggregateAsync(Expression<Func<KoboldLairUsageRecord, bool>> filter,
            IReadOnlyList<string> groupBy, CancellationToken ct)
        {
            var query = new AggregateQuery<KoboldLairUsageRecord> { Filter = filter, GroupByFields = groupBy, Aggregates = Totals };
            if (_store is IAsyncAggregatableStore<KoboldLairUsageRecord> aggregatable)
                return await aggregatable.AggregateAsync(query, ct);
            // A backend whose store cannot aggregate: the same totals over the matching records, in memory
            var records = await _repository.ReadAsync(filter: filter, orderBy: null, limit: null, offset: null, ct: ct);
            return await AggregateHelper.LinqAggregateAsync(records,
                new AggregateQuery<KoboldLairUsageRecord> { GroupByFields = groupBy, Aggregates = Totals }, ct);
        }

        private static int RequestsOf(AggregateResult row) => (int)row.GetValue<long>(Requests);
        private static int TokensOf(AggregateResult row) => (int)row.GetValue<long>(Tokens);
        private static double CostOf(AggregateResult row) => row.GetValue<double>(Cost);

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            EnsureColumns();
            _logger.LogInformation("SqlUsageRepository initialized");
        }

        /// <summary>
        /// Adds columns the entity declares but an older table lacks (TASK-082/087; Birko TASK-510) —
        /// <c>CreateSchemaAsync</c> never alters an existing table. A failure is logged as an error and startup
        /// continues: usage tracking degrades, the server does not stop.
        /// </summary>
        private void EnsureColumns()
        {
            try
            {
                if (_repository.Connector is not { } connector)
                    return; // a non-SQL backend has no fixed columns to repair (TASK-200)
                foreach (var drift in connector.EnsureColumns(typeof(KoboldLairUsageRecord)))
                    _logger.LogInformation("Added missing column {Table}.{Column} ({Type})", drift.Table, drift.Column, drift.Declared);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not add missing usage_records columns — usage records may fail to save");
            }
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
        public async Task<List<ProviderUsageSummary>> GetUsageByProviderAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var rows = await AggregateAsync(e => e.RecordedAt >= from && e.RecordedAt <= to,
                [nameof(KoboldLairUsageRecord.Provider)], ct);
            return rows
                .Select(r => new ProviderUsageSummary
                {
                    Provider = r.GetValue<string>(nameof(KoboldLairUsageRecord.Provider)) ?? "",
                    TotalRequests = RequestsOf(r),
                    TotalTokens = TokensOf(r),
                    TotalCostUsd = CostOf(r)
                })
                .OrderByDescending(s => s.TotalCostUsd)
                .ToList();
        }

        /// <summary>
        /// Gets usage for a specific project within a time range.
        /// </summary>
        public async Task<ProjectUsageSummary?> GetUsageByProjectAsync(string projectId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var rows = await AggregateAsync(e => e.ProjectId == projectId && e.RecordedAt >= from && e.RecordedAt <= to, [], ct);
            var row = rows.FirstOrDefault();
            if (row == null || RequestsOf(row) == 0)
                return null;

            return new ProjectUsageSummary
            {
                ProjectId = projectId,
                TotalRequests = RequestsOf(row),
                TotalTokens = TokensOf(row),
                TotalCostUsd = CostOf(row)
            };
        }

        /// <summary>
        /// Gets total spend for a time range, optionally filtered by provider.
        /// </summary>
        public async Task<double> GetTotalSpendAsync(DateTime from, DateTime to, string? provider = null, CancellationToken ct = default)
        {
            var rows = provider == null
                ? await AggregateAsync(e => e.RecordedAt >= from && e.RecordedAt <= to, [], ct)
                : await AggregateAsync(e => e.RecordedAt >= from && e.RecordedAt <= to && e.Provider == provider, [], ct);
            return rows.Sum(CostOf);
        }

        /// <summary>Each project's spend, all time, as the project budget counts it (TASK-163). Records with no project are left out.</summary>
        public async Task<Dictionary<string, double>> GetSpendByProjectAsync(CancellationToken ct = default)
        {
            var rows = await AggregateAsync(e => e.ProjectId != null && e.ProjectId != "",
                [nameof(KoboldLairUsageRecord.ProjectId)], ct);
            return rows
                .Select(r => (Project: r.GetValue<string>(nameof(KoboldLairUsageRecord.ProjectId)), Cost: CostOf(r)))
                .Where(r => !string.IsNullOrEmpty(r.Project))
                .ToDictionary(r => r.Project!, r => r.Cost);
        }

        /// <summary>A project's usage records, all time, for its stats (TASK-158).</summary>
        public async Task<List<KoboldLairUsageRecord>> GetProjectRecordsAsync(string projectId, CancellationToken ct = default)
        {
            var records = await _repository.ReadAsync(
                filter: e => e.ProjectId == projectId,
                orderBy: null, limit: null, offset: null, ct: ct);
            return records.ToList();
        }

        /// <summary>
        /// Gets total spend for a specific project (all time).
        /// </summary>
        public async Task<double> GetProjectSpendAsync(string projectId, CancellationToken ct = default) =>
            (await AggregateAsync(e => e.ProjectId == projectId, [], ct)).Sum(CostOf);

        #region IUsageRepository Implementation

        async Task IUsageRepository.RecordUsageAsync(BirkoUsageRecord entity, CancellationToken ct)
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

        #endregion
    }
}
