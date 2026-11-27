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

        /// <summary>
        /// The levels the question climbed before it reached the person (TASK-201, FEATURE-078 D24), in order and ending
        /// with <see cref="QuestionLevel.Person"/>; the answer goes back down the same way. Empty for a decision that never
        /// went through the chain (an escalation, an approval, or one stored before the chain).
        /// </summary>
        public List<QuestionLevel> Path { get; set; } = new();

        /// <summary>
        /// The agent level that answered instead of the person (TASK-208, FEATURE-078 D21): Supervisor (Drake), Overseer
        /// (Wyvern) or Dragon. Null for a decision a person answered or that is still open.
        /// </summary>
        public QuestionLevel? AnsweredBy { get; set; }

        /// <summary>Why the agent answered as it did: the line it quoted from its sources.</summary>
        public string? AnswerReason { get; set; }

        /// <summary>The agent's own answer, kept when the person overturns it (<see cref="Resolution"/> is then theirs).</summary>
        public string? AgentAnswer { get; set; }

        /// <summary>When the person kept or overturned the agent's answer; until then it is offered to them.</summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>When the task's plan was revised after an overturn; null until then (the task's Drake does it).</summary>
        public DateTime? RevisedAt { get; set; }

        /// <summary>An agent answered and the person has not yet kept or overturned it: offered as a card and in Needs you.</summary>
        public bool AwaitsReview => AnsweredBy is { } level && level != QuestionLevel.Person
            && ResolutionKind == PendingDecisionResolution.Answered && ReviewedAt is null;

        /// <summary>The person replaced the agent's answer with their own.</summary>
        public bool IsOverturned => AgentAnswer is not null && ReviewedAt is not null && Resolution != AgentAnswer;
    }

    // Stored as numbers on the pending decision, so the values are pinned. Never renumber.

    /// <summary>How an escalation is to be resolved: automatically (today's path) or by a person.</summary>
    public enum EscalationDisposition { AutoResolve = 0, HumanDecisionRequired = 1 }

    /// <summary>What parked the task.</summary>
    public enum PendingDecisionSource { Escalation = 0, AskHuman = 1, Approval = 2 }

    public enum PendingDecisionResolution { Answered = 0, TimedOut = 1 }

    /// <summary>What became of an answer submitted to a pending decision (not stored).</summary>
    public enum DecisionAnswerResult
    {
        Resolved,
        /// <summary>No such decision for this project and area.</summary>
        NotFound,
        /// <summary>Already answered or timed out.</summary>
        AlreadyResolved,
        /// <summary>Not one of the question's options, and free text is not allowed; the decision stays open.</summary>
        InvalidAnswer
    }
}
