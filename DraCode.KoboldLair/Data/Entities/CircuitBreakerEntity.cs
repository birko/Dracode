using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for circuit breaker state per provider.
    /// Persists across server restarts to prevent retry storms.
    /// </summary>
    [Table("circuit_breakers")]
    public class CircuitBreakerEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(50)]
        public string Provider { get; set; } = "";

        /// <summary>
        /// 0=Closed, 1=Open, 2=HalfOpen
        /// </summary>
        public int State { get; set; } = 0;

        public int ConsecutiveFailures { get; set; } = 0;

        public DateTime? OpenedAt { get; set; }
        public DateTime? LastFailureAt { get; set; }

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as CircuitBreakerEntity ?? new CircuitBreakerEntity();
            base.CopyTo(target);
            target.Provider = Provider;
            target.State = State;
            target.ConsecutiveFailures = ConsecutiveFailures;
            target.OpenedAt = OpenedAt;
            target.LastFailureAt = LastFailureAt;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
