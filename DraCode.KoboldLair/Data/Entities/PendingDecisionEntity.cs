using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for <see cref="Models.Tasks.PendingDecision"/>. <c>TimeoutAt</c> is stored absolute so the
    /// timeout sweep is one predicate and survives a restart.
    /// </summary>
    [Table("pending_decisions")]
    public class PendingDecisionEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(36)]
        public string DecisionId { get; set; } = "";

        [RequiredField]
        [MaxLengthField(36)]
        public string ProjectId { get; set; } = "";

        [RequiredField]
        [MaxLengthField(36)]
        public string TaskId { get; set; } = "";

        public int Disposition { get; set; }
        public int Source { get; set; }

        public string QuestionJson { get; set; } = "";
        public string? EscalationJson { get; set; }

        public DateTime DecisionCreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>The timeout in seconds; null waits until answered.</summary>
        public long? TimeoutSeconds { get; set; }

        public DateTime? TimeoutAt { get; set; }

        public string? Resolution { get; set; }

        /// <summary>Null while unresolved; otherwise <see cref="Models.Tasks.PendingDecisionResolution"/>.</summary>
        public int? ResolutionKind { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as PendingDecisionEntity ?? new PendingDecisionEntity();
            base.CopyTo(target);
            target.DecisionId = DecisionId;
            target.ProjectId = ProjectId;
            target.TaskId = TaskId;
            target.Disposition = Disposition;
            target.Source = Source;
            target.QuestionJson = QuestionJson;
            target.EscalationJson = EscalationJson;
            target.DecisionCreatedAt = DecisionCreatedAt;
            target.TimeoutSeconds = TimeoutSeconds;
            target.TimeoutAt = TimeoutAt;
            target.Resolution = Resolution;
            target.ResolutionKind = ResolutionKind;
            target.ResolvedAt = ResolvedAt;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
        }
    }
}
