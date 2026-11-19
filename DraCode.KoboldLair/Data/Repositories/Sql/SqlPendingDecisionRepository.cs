using System.Collections.Concurrent;
using System.Text.Json;
using Birko.AI.Orchestration.Models;
using Birko.Data.SQL.Repositories;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQL-backed store of the decisions parked tasks wait on (TASK-019) — the source of truth for which tasks are
    /// parked, so a parked task survives a restart.
    /// </summary>
    public class SqlPendingDecisionRepository
    {
        private readonly AsyncSqLiteModelRepository<PendingDecisionEntity> _repository;
        private readonly ILogger<SqlPendingDecisionRepository>? _logger;
        private readonly SemaphoreSlim _resolveLock;

        // One lock per database file, shared by every instance on it: resolving reads then writes, and an answer
        // racing a timeout must not both win (the repository exposes no conditional update)
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> ResolveLocks = new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public SqlPendingDecisionRepository(string dbPath, ILogger<SqlPendingDecisionRepository>? logger = null)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<PendingDecisionEntity>();

            _repository.SetSettings(SqliteDatabaseSettings.For(dbPath));
            _resolveLock = ResolveLocks.GetOrAdd(Path.GetFullPath(dbPath), _ => new SemaphoreSlim(1, 1));
        }

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            _logger?.LogInformation("SQLite pending-decision repository initialized");
        }

        public async Task CreateAsync(PendingDecision decision)
        {
            var now = DateTime.UtcNow;
            decision.CreatedAt = AsUtc(decision.CreatedAt);
            await _repository.CreateAsync(new PendingDecisionEntity
            {
                DecisionId = decision.Id,
                ProjectId = decision.ProjectId,
                TaskId = decision.TaskId,
                Disposition = (int)decision.Disposition,
                Source = (int)decision.Source,
                QuestionJson = decision.Question,
                EscalationJson = decision.Escalation is null ? null : JsonSerializer.Serialize(decision.Escalation, JsonOptions),
                DecisionCreatedAt = decision.CreatedAt,
                TimeoutSeconds = decision.Timeout is { } t ? (long)t.TotalSeconds : null,
                TimeoutAt = decision.TimeoutAt,
                Resolution = decision.Resolution,
                ResolutionKind = decision.ResolutionKind is { } k ? (int)k : null,
                ResolvedAt = decision.ResolvedAt,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        public async Task<PendingDecision?> GetUnresolvedForTaskAsync(string taskId)
        {
            var entities = await _repository.ReadAsync(
                filter: e => e.TaskId == taskId && e.ResolutionKind == null, orderBy: null, limit: null, offset: null);
            return entities.Select(ToModel).OrderByDescending(d => d.CreatedAt).FirstOrDefault();
        }

        public async Task<PendingDecision?> GetByIdAsync(string decisionId)
        {
            var entity = await _repository.ReadAsync(e => e.DecisionId == decisionId, CancellationToken.None);
            return entity is null ? null : ToModel(entity);
        }

        /// <summary>The task's answered and timed-out decisions, newest first: where an answer is read back (TASK-022).</summary>
        public async Task<List<PendingDecision>> GetResolvedForTaskAsync(string taskId)
        {
            var entities = await _repository.ReadAsync(
                filter: e => e.TaskId == taskId && e.ResolutionKind != null, orderBy: null, limit: null, offset: null);
            return entities.Select(ToModel).OrderByDescending(d => d.ResolvedAt).ToList();
        }

        /// <summary>Every decision of <paramref name="source"/> the task ever had, answered and open alike.</summary>
        public async Task<int> CountForTaskAsync(string taskId, PendingDecisionSource source)
        {
            var sourceInt = (int)source;
            return (int)await _repository.CountAsync(e => e.TaskId == taskId && e.Source == sourceInt);
        }

        public async Task<List<PendingDecision>> GetUnresolvedForProjectAsync(string projectId)
        {
            var entities = await _repository.ReadAsync(
                filter: e => e.ProjectId == projectId && e.ResolutionKind == null, orderBy: null, limit: null, offset: null);
            return entities.Select(ToModel).ToList();
        }

        public async Task<List<PendingDecision>> GetExpiredAsync(DateTime now)
        {
            var entities = await _repository.ReadAsync(
                filter: e => e.ResolutionKind == null && e.TimeoutAt != null && e.TimeoutAt <= now,
                orderBy: null, limit: null, offset: null);
            return entities.Select(ToModel).ToList();
        }

        /// <summary>Resolves a decision once. False when it is unknown or already resolved.</summary>
        public async Task<bool> ResolveAsync(string decisionId, PendingDecisionResolution kind, string? resolution, DateTime at)
        {
            await _resolveLock.WaitAsync();
            try
            {
                var entity = await _repository.ReadAsync(e => e.DecisionId == decisionId, CancellationToken.None);
                if (entity is null || entity.ResolutionKind is not null)
                    return false;

                entity.ResolutionKind = (int)kind;
                entity.Resolution = resolution;
                entity.ResolvedAt = AsUtc(at);
                entity.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(entity);
                return true;
            }
            finally
            {
                _resolveLock.Release();
            }
        }

        /// <summary>
        /// Stores the escalation as its automatic route left it, so the resumed agent hears what was done (TASK-022).
        /// False when the decision is unknown.
        /// </summary>
        public async Task<bool> RecordEscalationOutcomeAsync(string decisionId, EscalationAlert escalation)
        {
            await _resolveLock.WaitAsync();
            try
            {
                var entity = await _repository.ReadAsync(e => e.DecisionId == decisionId, CancellationToken.None);
                if (entity is null)
                    return false;

                entity.EscalationJson = JsonSerializer.Serialize(escalation, JsonOptions);
                entity.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(entity);
                return true;
            }
            finally
            {
                _resolveLock.Release();
            }
        }

        /// <summary>A Local time converts; an Unspecified one is taken as UTC, which is how every caller here passes it.</summary>
        private static DateTime AsUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };

        private PendingDecision ToModel(PendingDecisionEntity e)
        {
            EscalationAlert? escalation = null;
            if (!string.IsNullOrEmpty(e.EscalationJson))
            {
                try { escalation = JsonSerializer.Deserialize<EscalationAlert>(e.EscalationJson, JsonOptions); }
                catch (JsonException ex) { _logger?.LogWarning(ex, "Unreadable escalation on pending decision {Id}", e.DecisionId); }
            }

            return new PendingDecision
            {
                Id = e.DecisionId,
                ProjectId = e.ProjectId,
                TaskId = e.TaskId,
                Disposition = (EscalationDisposition)e.Disposition,
                Source = (PendingDecisionSource)e.Source,
                Question = e.QuestionJson,
                Escalation = escalation,
                CreatedAt = DateTime.SpecifyKind(e.DecisionCreatedAt, DateTimeKind.Utc),
                Timeout = e.TimeoutSeconds is { } s ? TimeSpan.FromSeconds(s) : null,
                Resolution = e.Resolution,
                ResolutionKind = e.ResolutionKind is { } k ? (PendingDecisionResolution)k : null,
                ResolvedAt = e.ResolvedAt is { } r ? DateTime.SpecifyKind(r, DateTimeKind.Utc) : null
            };
        }
    }
}
