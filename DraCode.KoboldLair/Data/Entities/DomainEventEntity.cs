using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for domain events (event sourcing audit trail).
    /// Each row represents a single immutable domain event.
    /// </summary>
    [Table("domain_events")]
    public class DomainEventEntity : AbstractDatabaseLogModel
    {
        /// <summary>
        /// The aggregate (e.g. specification) this event belongs to
        /// </summary>
        [RequiredField]
        [MaxLengthField(36)]
        public string AggregateId { get; set; } = "";

        /// <summary>
        /// Monotonically increasing version per aggregate
        /// </summary>
        public long Version { get; set; }

        /// <summary>
        /// Event type discriminator (e.g. "SpecificationCreated", "FeatureAdded")
        /// </summary>
        [RequiredField]
        [MaxLengthField(100)]
        public string EventType { get; set; } = "";

        /// <summary>
        /// JSON-serialized event data payload
        /// </summary>
        public string EventData { get; set; } = "{}";

        /// <summary>
        /// Optional JSON metadata (correlation, causation IDs, etc.)
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// User who triggered this event (null for system events)
        /// </summary>
        [MaxLengthField(36)]
        public string? UserId { get; set; }

        /// <summary>
        /// When the event occurred (domain time, not persistence time)
        /// </summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as DomainEventEntity ?? new DomainEventEntity();
            base.CopyTo(target);
            target.AggregateId = AggregateId;
            target.Version = Version;
            target.EventType = EventType;
            target.EventData = EventData;
            target.Metadata = Metadata;
            target.UserId = UserId;
            target.OccurredAt = OccurredAt;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
