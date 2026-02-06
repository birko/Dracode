namespace DraCode.KoboldLair.Models.Tasks
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
        /// Task has been completed successfully
        /// </summary>
        Done,

        /// <summary>
        /// Task encountered an error and cannot continue
        /// </summary>
        Failed,

        /// <summary>
        /// Task is blocked because one or more dependencies have failed
        /// </summary>
        BlockedByFailure
    }
}
