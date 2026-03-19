namespace DraCode.KoboldLair.Messages;

/// <summary>
/// Message published by Drake to assign a task to a Kobold worker.
/// Consumed by KoboldWorkerService to create and execute Kobolds.
/// </summary>
public class TaskAssignmentMessage
{
    /// <summary>
    /// Project this task belongs to
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Project name for display
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// The task ID to execute
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Task description
    /// </summary>
    public string TaskDescription { get; set; } = string.Empty;

    /// <summary>
    /// Agent type for the Kobold (e.g., "csharp", "typescript", "react")
    /// </summary>
    public string AgentType { get; set; } = string.Empty;

    /// <summary>
    /// LLM provider override (null = use default)
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// LLM model override (null = use default)
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Workspace root for file operations
    /// </summary>
    public string WorkspacePath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the specification file
    /// </summary>
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Allowed external paths for file access
    /// </summary>
    public List<string> AllowedExternalPaths { get; set; } = [];

    /// <summary>
    /// Whether implementation planning is enabled
    /// </summary>
    public bool PlanningEnabled { get; set; } = true;

    /// <summary>
    /// Maximum iterations for Kobold execution
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Feature branch name (null if working on main)
    /// </summary>
    public string? FeatureBranch { get; set; }

    /// <summary>
    /// Git worktree path (null if not using worktrees)
    /// </summary>
    public string? WorktreePath { get; set; }

    /// <summary>
    /// Project constraints from Wyrm and Wyvern analysis
    /// </summary>
    public List<string> Constraints { get; set; } = [];

    /// <summary>
    /// Specification context content
    /// </summary>
    public string? SpecificationContext { get; set; }

    /// <summary>
    /// Dependency context from completed tasks
    /// </summary>
    public string? DependencyContext { get; set; }

    /// <summary>
    /// Correlation ID for request-response tracking
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// When this assignment was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
