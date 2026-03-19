namespace DraCode.KoboldLair.Messages;

/// <summary>
/// Message published when a Kobold completes (or fails) a task.
/// Consumed by Drake to update task status and trigger post-completion logic.
/// </summary>
public class TaskCompletionMessage
{
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string KoboldId { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the task completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the task failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of iterations the Kobold used
    /// </summary>
    public int IterationsUsed { get; set; }

    /// <summary>
    /// Total execution duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Files created during execution
    /// </summary>
    public List<string> OutputFiles { get; set; } = [];

    /// <summary>
    /// Worktree path used (for cleanup)
    /// </summary>
    public string? WorktreePath { get; set; }

    /// <summary>
    /// Feature branch name (for git commit)
    /// </summary>
    public string? FeatureBranch { get; set; }

    /// <summary>
    /// Correlation ID matching the assignment
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
