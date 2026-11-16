using Birko.AI.Orchestration.Models;

namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// A question a parked task waits on (TASK-019). The database copy is the source of truth for which tasks are
    /// <see cref="TaskStatus.AwaitingHumanDecision"/>: Drake reconciles its tasks to it on every reload.
    /// </summary>
    public class PendingDecision
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = "";
        public string TaskId { get; set; } = "";
        public EscalationDisposition Disposition { get; set; } = EscalationDisposition.HumanDecisionRequired;
        public PendingDecisionSource Source { get; set; }

        /// <summary>The question for the person, as the parking caller shaped it (JSON or text).</summary>
        public string Question { get; set; } = "";

        /// <summary>The escalation that led here, replayed through the auto-resolve path if the decision times out.</summary>
        public EscalationAlert? Escalation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>How long to wait for an answer. Null: wait until answered, never auto-resolve (FEATURE-027 D4).</summary>
        public TimeSpan? Timeout { get; set; }

        public DateTime? TimeoutAt => Timeout is { } t ? CreatedAt + t : null;

        public string? Resolution { get; set; }
        public PendingDecisionResolution? ResolutionKind { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public bool IsResolved => ResolutionKind is not null;
    }

    // Stored as numbers on the pending decision, so the values are pinned. Never renumber.

    /// <summary>How an escalation is to be resolved: automatically (today's path) or by a person.</summary>
    public enum EscalationDisposition { AutoResolve = 0, HumanDecisionRequired = 1 }

    /// <summary>What parked the task.</summary>
    public enum PendingDecisionSource { Escalation = 0, AskHuman = 1, Approval = 2 }

    public enum PendingDecisionResolution { Answered = 0, TimedOut = 1 }
}
