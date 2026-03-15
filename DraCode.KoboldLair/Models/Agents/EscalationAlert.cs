namespace DraCode.KoboldLair.Models.Agents;

/// <summary>
/// Represents an escalation from a Kobold to Drake for routing to the appropriate upstream agent.
/// Created when a Kobold's confidence drops below threshold or the ReasoningMonitor detects issues.
/// </summary>
public class EscalationAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = "";
    public string? TaskId { get; set; }
    public Guid KoboldId { get; set; }
    public string AgentType { get; set; } = "";
    public EscalationSource Source { get; set; }
    public EscalationType Type { get; set; }
    public string Summary { get; set; } = "";
    public List<ReflectionEntry> ReflectionHistory { get; set; } = new();
    public EscalationStatus Status { get; set; } = EscalationStatus.Pending;
    public string? Resolution { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public enum EscalationSource { ReflectionTool, ReasoningMonitor }
public enum EscalationStatus { Pending, InProgress, Resolved, Failed }
