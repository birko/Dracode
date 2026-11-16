namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// Represents the status of a task in the orchestrator.
    /// Stored as its number (the tasks table and the tracker JSON), so the values are pinned. Never renumber.
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>
        /// Task has been received but no agent assigned yet
        /// </summary>
        Unassigned = 0,

        /// <summary>
        /// Agent has been assigned but not started yet
        /// </summary>
        NotInitialized = 1,

        /// <summary>
        /// Agent is currently working on the task
        /// </summary>
        Working = 2,

        /// <summary>
        /// Task has been completed successfully
        /// </summary>
        Done = 3,

        /// <summary>
        /// Task encountered an error and cannot continue
        /// </summary>
        Failed = 4,

        /// <summary>
        /// Task is blocked because one or more dependencies have failed
        /// </summary>
        BlockedByFailure = 5,

        /// <summary>
        /// Task is parked until a person answers its pending decision (or the decision times out)
        /// </summary>
        AwaitingHumanDecision = 6
    }
}
