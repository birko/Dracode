namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// Represents a tracked task in the orchestrator
    /// </summary>
    public class TaskRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Task { get; set; } = string.Empty;
        public string AssignedAgent { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public TaskStatus Status { get; set; } = TaskStatus.Unassigned;
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Specification version when this task was created
        /// Used to detect specification drift during execution
        /// </summary>
        public int SpecificationVersion { get; set; } = 1;

        /// <summary>
        /// Content hash of specification when task was created
        /// </summary>
        public string? SpecificationContentHash { get; set; }

        /// <summary>
        /// Task IDs that this task depends on (e.g., ["frontend-1", "frontend-2"])
        /// Stored as structured data to avoid parsing from task description
        /// </summary>
        public List<string> Dependencies { get; set; } = new();
        
        /// <summary>
        /// Git commit SHA created when task was completed (for tracking output files)
        /// </summary>
        public string? CommitSha { get; set; }
        
        /// <summary>
        /// List of files created or modified by this task (extracted from git commit)
        /// </summary>
        public List<string> OutputFiles { get; set; } = new();

        // Retry tracking properties
        
        /// <summary>
        /// Number of retry attempts made for this task
        /// </summary>
        public int RetryCount { get; set; } = 0;
        
        /// <summary>
        /// Timestamp of the last retry attempt
        /// </summary>
        public DateTime? LastRetryAttempt { get; set; }
        
        /// <summary>
        /// Calculated timestamp when next retry should be attempted
        /// </summary>
        public DateTime? NextRetryAt { get; set; }
        
        /// <summary>
        /// Error category for determining retry eligibility (transient vs permanent)
        /// </summary>
        public string? ErrorCategory { get; set; }
        
        /// <summary>
        /// LLM provider used for this task (for circuit breaker tracking)
        /// </summary>
        public string? Provider { get; set; }
    }
}
