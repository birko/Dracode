namespace DraCode.KoboldLair.Messages;

/// <summary>
/// Periodic heartbeat from active Kobolds for distributed monitoring.
/// Enables DrakeMonitoringService to track remote Kobold health.
/// </summary>
public class KoboldHeartbeatMessage
{
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string KoboldId { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public int Confidence { get; set; } = 100;
    public int IterationsUsed { get; set; }
    public string? CurrentActivity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
