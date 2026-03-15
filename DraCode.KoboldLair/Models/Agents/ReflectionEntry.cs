namespace DraCode.KoboldLair.Models.Agents;

/// <summary>
/// Records a Kobold's self-assessment during plan execution.
/// Created by the ReflectionTool at checkpoint intervals.
/// </summary>
public class ReflectionEntry
{
    public int Iteration { get; set; }
    public int StepIndex { get; set; }        // 1-based
    public int ProgressPercent { get; set; }   // 0-100
    public string? Blockers { get; set; }
    public int ConfidencePercent { get; set; } // 0-100
    public ReflectionDecision Decision { get; set; }
    public EscalationType? EscalationType { get; set; }
    public string? Adjustment { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum ReflectionDecision { Continue, Pivot, Escalate }

public enum EscalationType
{
    TaskInfeasible,
    MissingDependency,
    NeedsSplit,
    WrongApproach,
    WrongAgentType
}
