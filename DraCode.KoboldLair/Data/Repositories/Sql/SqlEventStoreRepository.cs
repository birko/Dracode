using Birko.Data.EventSourcing.Events;
using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using Birko.Configuration;
using DraCode.KoboldLair.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQLite-backed implementation of IAsyncEventStore for domain event persistence.
    /// Stores specification change events as immutable audit trail records.
    /// </summary>
    public class SqlEventStoreRepository : IAsyncEventStore
    {
        private readonly AsyncSqLiteModelRepository<DomainEventEntity> _repository;
        private readonly ILogger<SqlEventStoreRepository>? _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public SqlEventStoreRepository(string dbPath, ILogger<SqlEventStoreRepository>? logger = null)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<DomainEventEntity>();

            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            _repository.SetSettings(new PasswordSettings(dbDir, dbFile));
        }

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            _logger?.LogInformation("SQLite event store repository initialized");
        }

        public async Task AppendAsync(IEvent @event, CancellationToken cancellationToken = default)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                var entity = ToEntity(@event);
                await _repository.CreateAsync(entity);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task AppendRangeAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var @event in events)
                {
                    var entity = ToEntity(@event);
                    await _repository.CreateAsync(entity);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<IEnumerable<IEvent>> ReadAsync(Guid aggregateId, CancellationToken cancellationToken = default)
        {
            var aggId = aggregateId.ToString();
            var entities = await _repository.ReadAsync(
                filter: e => e.AggregateId == aggId, orderBy: null, limit: null, offset: null);
            return entities.OrderBy(e => e.Version).Select(FromEntity).ToList();
        }

        public async Task<IEnumerable<IEvent>> ReadUpToVersionAsync(Guid aggregateId, long maxVersion, CancellationToken cancellationToken = default)
        {
            var aggId = aggregateId.ToString();
            var entities = await _repository.ReadAsync(
                filter: e => e.AggregateId == aggId && e.Version <= maxVersion, orderBy: null, limit: null, offset: null);
            return entities.OrderBy(e => e.Version).Select(FromEntity).ToList();
        }

        public async Task<IEnumerable<IEvent>> ReadFromVersionAsync(Guid aggregateId, long fromVersion, CancellationToken cancellationToken = default)
        {
            var aggId = aggregateId.ToString();
            var entities = await _repository.ReadAsync(
                filter: e => e.AggregateId == aggId && e.Version >= fromVersion, orderBy: null, limit: null, offset: null);
            return entities.OrderBy(e => e.Version).Select(FromEntity).ToList();
        }

        public async Task<long> GetVersionAsync(Guid aggregateId, CancellationToken cancellationToken = default)
        {
            var aggId = aggregateId.ToString();
            var entities = await _repository.ReadAsync(
                filter: e => e.AggregateId == aggId, orderBy: null, limit: null, offset: null);
            return entities.Any() ? entities.Max(e => e.Version) : 0;
        }

        public async Task<IEnumerable<IEvent>> ReadAllFromAsync(DateTime from, CancellationToken cancellationToken = default)
        {
            var entities = await _repository.ReadAsync(
                filter: e => e.OccurredAt >= from, orderBy: null, limit: null, offset: null);
            return entities.OrderBy(e => e.OccurredAt).ThenBy(e => e.Version).Select(FromEntity).ToList();
        }

        private static DomainEventEntity ToEntity(IEvent @event)
        {
            return new DomainEventEntity
            {
                Guid = @event.EventId,
                AggregateId = @event.AggregateId.ToString(),
                Version = @event.Version,
                EventType = @event.EventType,
                EventData = @event.EventData,
                Metadata = @event.Metadata,
                UserId = @event.UserId?.ToString(),
                OccurredAt = @event.OccurredAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static IEvent FromEntity(DomainEventEntity entity)
        {
            return new DomainEvent(
                aggregateId: Guid.Parse(entity.AggregateId),
                version: entity.Version,
                eventType: entity.EventType,
                eventData: entity.EventData,
                userId: string.IsNullOrEmpty(entity.UserId) ? null : Guid.Parse(entity.UserId))
            {
                EventId = entity.Guid ?? Guid.NewGuid(),
                OccurredAt = entity.OccurredAt,
                Metadata = entity.Metadata
            };
        }
    }
}
