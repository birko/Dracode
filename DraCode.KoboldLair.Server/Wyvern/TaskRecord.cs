namespace DraCode.KoboldLair.Server.Wyvern
{
    /// <summary>
    /// Represents the status of a task in the orchestrator
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>
        /// Task has been received but no agent assigned yet
        /// </summary>
        Unassigned,
        
        /// <summary>
        /// Agent has been assigned but not started yet
        /// </summary>
        NotInitialized,
        
        /// <summary>
        /// Agent is currently working on the task
        /// </summary>
        Working,
        
        /// <summary>
        /// Task has been completed
        /// </summary>
        Done
    }

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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
