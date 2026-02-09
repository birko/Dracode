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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Git commit SHA created when task was completed (for tracking output files)
        /// </summary>
        public string? CommitSha { get; set; }
        
        /// <summary>
        /// List of files created or modified by this task (extracted from git commit)
        /// </summary>
        public List<string> OutputFiles { get; set; } = new();
    }
}
