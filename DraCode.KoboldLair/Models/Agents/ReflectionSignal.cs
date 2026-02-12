namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Represents a structured checkpoint from a Kobold's self-reflection during task execution.
    /// Used by the ReflectionTool to capture progress, confidence, and blockers.
    /// </summary>
    public class ReflectionSignal
    {
        /// <summary>
        /// Unique identifier for this reflection checkpoint
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// When this reflection was recorded
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Current step index (0-based) the Kobold is working on
        /// </summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// Current iteration number within the execution loop
        /// </summary>
        public int Iteration { get; set; }

        /// <summary>
        /// Self-reported progress percentage toward current step completion (0-100)
        /// </summary>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// Files that have been successfully created or modified so far
        /// </summary>
        public List<string> FilesDone { get; set; } = new();

        /// <summary>
        /// Current blockers or obstacles the Kobold is facing
        /// </summary>
        public List<string> Blockers { get; set; } = new();

        /// <summary>
        /// Confidence level (0-100) that current approach will succeed
        /// </summary>
        public int Confidence { get; set; }

        /// <summary>
        /// Kobold's decision on how to proceed
        /// </summary>
        public ReflectionDecision Decision { get; set; } = ReflectionDecision.Continue;

        /// <summary>
        /// Optional additional notes from the Kobold
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Whether a Drake intervention signal was sent based on this reflection
        /// </summary>
        public bool DrakeSignalSent { get; set; }

        /// <summary>
        /// The intervention reason if DrakeSignalSent is true
        /// </summary>
        public InterventionReason? InterventionReasonSent { get; set; }
    }

    /// <summary>
    /// Decision made by a Kobold during self-reflection
    /// </summary>
    public enum ReflectionDecision
    {
        /// <summary>
        /// Continue with the current approach - things are going well
        /// </summary>
        Continue,

        /// <summary>
        /// Pivot to a different approach - current method isn't working
        /// </summary>
        Pivot,

        /// <summary>
        /// Escalate to Drake supervisor - need help or intervention
        /// </summary>
        Escalate
    }

    /// <summary>
    /// Signal sent to Drake when intervention may be needed.
    /// Drake checks these signals to decide if it should intervene.
    /// </summary>
    public class DrakeInterventionSignal
    {
        /// <summary>
        /// Unique identifier for this signal
        /// </summary>
        public string SignalId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// ID of the Kobold that generated this signal
        /// </summary>
        public string KoboldId { get; set; } = string.Empty;

        /// <summary>
        /// Task ID the Kobold is working on
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Project ID the task belongs to
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// Reason for the intervention signal
        /// </summary>
        public InterventionReason Reason { get; set; }

        /// <summary>
        /// Current confidence level when signal was generated
        /// </summary>
        public int Confidence { get; set; }

        /// <summary>
        /// Current blockers when signal was generated
        /// </summary>
        public List<string> Blockers { get; set; } = new();

        /// <summary>
        /// The reflection that triggered this signal
        /// </summary>
        public ReflectionSignal? SourceReflection { get; set; }

        /// <summary>
        /// When this signal was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this signal has been acknowledged by Drake
        /// </summary>
        public bool Acknowledged { get; set; }

        /// <summary>
        /// When the signal was acknowledged
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }
    }

    /// <summary>
    /// Reasons why a Drake intervention signal might be generated
    /// </summary>
    public enum InterventionReason
    {
        /// <summary>
        /// Kobold explicitly requested escalation via decision = "escalate"
        /// </summary>
        AgentEscalated,

        /// <summary>
        /// Confidence dropped below threshold (e.g., less than 30%)
        /// </summary>
        LowConfidence,

        /// <summary>
        /// Confidence declined significantly over multiple checkpoints (e.g., 20%+ drop over 3 checkpoints)
        /// </summary>
        DecliningConfidence,

        /// <summary>
        /// Multiple blockers reported (e.g., 3+ blockers)
        /// </summary>
        MultipleBlockers,

        /// <summary>
        /// Same file being modified repeatedly (possible stuck loop)
        /// </summary>
        RepeatedFileModifications,

        /// <summary>
        /// No progress over multiple checkpoints
        /// </summary>
        StalledProgress
    }

    /// <summary>
    /// Configuration for intervention thresholds
    /// </summary>
    public static class InterventionThresholds
    {
        /// <summary>
        /// Confidence below this triggers LowConfidence intervention (default: 30%)
        /// </summary>
        public const int LowConfidenceThreshold = 30;

        /// <summary>
        /// Number of checkpoints to consider for declining confidence trend (default: 3)
        /// </summary>
        public const int DecliningConfidenceCheckpoints = 3;

        /// <summary>
        /// Minimum confidence drop percentage over DecliningConfidenceCheckpoints to trigger intervention (default: 20%)
        /// </summary>
        public const int DecliningConfidenceDropThreshold = 20;

        /// <summary>
        /// Number of blockers that triggers MultipleBlockers intervention (default: 3)
        /// </summary>
        public const int MultipleBlockersThreshold = 3;

        /// <summary>
        /// Number of times the same file can be modified before RepeatedFileModifications triggers (default: 5)
        /// </summary>
        public const int RepeatedFileModificationThreshold = 5;

        /// <summary>
        /// Number of checkpoints with 0% progress before StalledProgress triggers (default: 3)
        /// </summary>
        public const int StalledProgressCheckpoints = 3;
    }
}
