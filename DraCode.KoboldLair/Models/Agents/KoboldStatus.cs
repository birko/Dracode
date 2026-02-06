namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Represents the status of a Kobold worker agent.
    /// </summary>
    public enum KoboldStatus
    {
        /// <summary>
        /// Kobold is created but not assigned to any task
        /// </summary>
        Unassigned,

        /// <summary>
        /// Kobold has been assigned a task but hasn't started working yet
        /// </summary>
        Assigned,

        /// <summary>
        /// Kobold is actively working on the assigned task
        /// </summary>
        Working,

        /// <summary>
        /// Kobold has completed the assigned task successfully
        /// </summary>
        Done,

        /// <summary>
        /// Kobold encountered an error and cannot continue
        /// </summary>
        Failed
    }
}
