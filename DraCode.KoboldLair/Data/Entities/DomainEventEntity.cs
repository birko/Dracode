using Birko.Data.Models;
using Birko.Data.SQL.Attributes;
using Birko.Data.ViewModels;

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

        public override void LoadFrom(ModelViewModel data)
        {
            base.LoadFrom(data);
            if (data is DomainEventViewModel vm)
            {
                AggregateId = vm.AggregateId;
                Version = vm.Version;
                EventType = vm.EventType;
                EventData = vm.EventData;
                Metadata = vm.Metadata;
                UserId = vm.UserId;
                OccurredAt = vm.OccurredAt;
            }
        }
    }

    public class DomainEventViewModel : LogViewModel
    {
        public string AggregateId { get; set; } = "";
        public long Version { get; set; }
        public string EventType { get; set; } = "";
        public string EventData { get; set; } = "{}";
        public string? Metadata { get; set; }
        public string? UserId { get; set; }
        public DateTime OccurredAt { get; set; }

        public void LoadFrom(DomainEventEntity data)
        {
            base.LoadFrom((AbstractModel)data);
            if (data != null)
            {
                AggregateId = data.AggregateId;
                Version = data.Version;
                EventType = data.EventType;
                EventData = data.EventData;
                Metadata = data.Metadata;
                UserId = data.UserId;
                OccurredAt = data.OccurredAt;
            }
        }
    }
}
