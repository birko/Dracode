using System.Text.Json;
using Birko.Data.EventSourcing.Events;
using DraCode.KoboldLair.Events.Specification;
using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services.EventSourcing
{
    /// <summary>
    /// High-level service for recording and querying specification domain events.
    /// Wraps IAsyncEventStore with specification-specific operations.
    /// </summary>
    public class SpecificationEventService
    {
        private readonly IAsyncEventStore _eventStore;
        private readonly ILogger<SpecificationEventService>? _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public SpecificationEventService(IAsyncEventStore eventStore, ILogger<SpecificationEventService>? logger = null)
        {
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _logger = logger;
        }

        /// <summary>
        /// Records a specification creation event.
        /// </summary>
        public async Task RecordSpecificationCreatedAsync(string specId, string name, string content, string projectId)
        {
            var aggregateId = ToGuid(specId);
            var version = await _eventStore.GetVersionAsync(aggregateId) + 1;

            var data = new SpecificationCreatedData
            {
                Name = name,
                Content = content,
                ProjectId = projectId,
                ContentHash = Models.Projects.Specification.ComputeHash(content)
            };

            var @event = new DomainEvent(
                aggregateId: aggregateId,
                version: version,
                eventType: SpecificationEventTypes.Created,
                eventData: JsonSerializer.Serialize(data, JsonOptions));

            await _eventStore.AppendAsync(@event);
            _logger?.LogDebug("Recorded SpecificationCreated event for {SpecId} v{Version}", specId, version);
        }

        /// <summary>
        /// Records a specification content update event.
        /// </summary>
        public async Task RecordSpecificationUpdatedAsync(string specId, string content, string previousHash, int version, string? changeDescription = null)
        {
            var aggregateId = ToGuid(specId);
            var eventVersion = await _eventStore.GetVersionAsync(aggregateId) + 1;

            var data = new SpecificationUpdatedData
            {
                Content = content,
                PreviousContentHash = previousHash,
                NewContentHash = Models.Projects.Specification.ComputeHash(content),
                Version = version,
                ChangeDescription = changeDescription
            };

            var @event = new DomainEvent(
                aggregateId: aggregateId,
                version: eventVersion,
                eventType: SpecificationEventTypes.Updated,
                eventData: JsonSerializer.Serialize(data, JsonOptions));

            await _eventStore.AppendAsync(@event);
            _logger?.LogDebug("Recorded SpecificationUpdated event for {SpecId} v{Version}", specId, eventVersion);
        }

        /// <summary>
        /// Records a specification approval event.
        /// </summary>
        public async Task RecordSpecificationApprovedAsync(string specId, string approvedBy = "Dragon")
        {
            var aggregateId = ToGuid(specId);
            var version = await _eventStore.GetVersionAsync(aggregateId) + 1;

            var data = new SpecificationApprovedData { ApprovedBy = approvedBy };

            var @event = new DomainEvent(
                aggregateId: aggregateId,
                version: version,
                eventType: SpecificationEventTypes.Approved,
                eventData: JsonSerializer.Serialize(data, JsonOptions));

            await _eventStore.AppendAsync(@event);
            _logger?.LogDebug("Recorded SpecificationApproved event for {SpecId}", specId);
        }

        /// <summary>
        /// Records a feature addition event.
        /// </summary>
        public async Task RecordFeatureAddedAsync(string specId, Feature feature)
        {
            var aggregateId = ToGuid(specId);
            var version = await _eventStore.GetVersionAsync(aggregateId) + 1;

            var data = new FeatureAddedData
            {
                FeatureId = feature.Id,
                Name = feature.Name,
                Description = feature.Description,
                Priority = feature.Priority
            };

            var @event = new DomainEvent(
                aggregateId: aggregateId,
                version: version,
                eventType: SpecificationEventTypes.FeatureAdded,
                eventData: JsonSerializer.Serialize(data, JsonOptions));

            await _eventStore.AppendAsync(@event);
            _logger?.LogDebug("Recorded FeatureAdded event for {FeatureName} in {SpecId}", feature.Name, specId);
        }

        /// <summary>
        /// Records a feature modification event.
        /// </summary>
        public async Task RecordFeatureModifiedAsync(string specId, string featureId, string name,
            string? description, string? priority, string? previousDescription, string? previousPriority)
        {
            var aggregateId = ToGuid(specId);
            var version = await _eventStore.GetVersionAsync(aggregateId) + 1;

            var data = new FeatureModifiedData
            {
                FeatureId = featureId,
                Name = name,
                Description = description,
                Priority = priority,
                PreviousDescription = previousDescription,
                PreviousPriority = previousPriority
            };

            var @event = new DomainEvent(
                aggregateId: aggregateId,
                version: version,
                eventType: SpecificationEventTypes.FeatureModified,
                eventData: JsonSerializer.Serialize(data, JsonOptions));

            await _eventStore.AppendAsync(@event);
            _logger?.LogDebug("Recorded FeatureModified event for {FeatureId} in {SpecId}", featureId, specId);
        }

        /// <summary>
        /// Records a feature removal event.
        /// </summary>
        public async Task RecordFeatureRemovedAsync(string specId, string featureId, string name, string? reason = null)
        {
            var aggregateId = ToGuid(specId);
            var version = await _eventStore.GetVersionAsync(aggregateId) + 1;

            var data = new FeatureRemovedData
            {
                FeatureId = featureId,
                Name = name,
                Reason = reason
            };

            var @event = new DomainEvent(
                aggregateId: aggregateId,
                version: version,
                eventType: SpecificationEventTypes.FeatureRemoved,
                eventData: JsonSerializer.Serialize(data, JsonOptions));

            await _eventStore.AppendAsync(@event);
            _logger?.LogDebug("Recorded FeatureRemoved event for {FeatureId} in {SpecId}", featureId, specId);
        }

        /// <summary>
        /// Returns the full event history for a specification, ordered by version.
        /// </summary>
        public async Task<IEnumerable<SpecificationEventRecord>> GetSpecificationHistoryAsync(string specId)
        {
            var aggregateId = ToGuid(specId);
            var events = await _eventStore.ReadAsync(aggregateId);

            return events.Select(e => new SpecificationEventRecord
            {
                EventId = e.EventId,
                Version = e.Version,
                EventType = e.EventType,
                OccurredAt = e.OccurredAt,
                EventData = e.EventData
            });
        }

        /// <summary>
        /// Returns events up to a specific version (for point-in-time replay).
        /// </summary>
        public async Task<IEnumerable<SpecificationEventRecord>> GetHistoryUpToVersionAsync(string specId, long maxVersion)
        {
            var aggregateId = ToGuid(specId);
            var events = await _eventStore.ReadUpToVersionAsync(aggregateId, maxVersion);

            return events.Select(e => new SpecificationEventRecord
            {
                EventId = e.EventId,
                Version = e.Version,
                EventType = e.EventType,
                OccurredAt = e.OccurredAt,
                EventData = e.EventData
            });
        }

        /// <summary>
        /// Converts a string ID to a deterministic GUID.
        /// If the ID is already a valid GUID, parses it directly.
        /// Otherwise, creates a deterministic GUID from the string hash.
        /// </summary>
        private static Guid ToGuid(string id)
        {
            if (Guid.TryParse(id, out var guid))
                return guid;

            // Deterministic GUID from string hash
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id));
            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);
            return new Guid(guidBytes);
        }
    }

    /// <summary>
    /// Simplified event record for display/query purposes.
    /// </summary>
    public class SpecificationEventRecord
    {
        public Guid EventId { get; set; }
        public long Version { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string EventData { get; set; } = "{}";
    }
}
