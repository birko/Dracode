namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Execution state of a project - controls whether DrakeExecutionService will process it
    /// </summary>
    public enum ProjectExecutionState
    {
        /// <summary>
        /// Normal execution - Drake processes tasks automatically (default)
        /// </summary>
        Running,

        /// <summary>
        /// Temporarily paused - can be resumed at any time (short-term hold)
        /// Use for debugging, high system load, or brief interruptions
        /// </summary>
        Paused,

        /// <summary>
        /// Long-term hold - requires explicit resume action
        /// Use for extended delays or projects awaiting external changes
        /// </summary>
        Suspended,

        /// <summary>
        /// Permanently stopped - terminal state, cannot be resumed
        /// Use when project is abandoned or no longer needed
        /// </summary>
        Cancelled
    }
}
