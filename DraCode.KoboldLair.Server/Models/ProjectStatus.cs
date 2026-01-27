namespace DraCode.KoboldLair.Server.Models
{
    /// <summary>
    /// Status of a project in the KoboldLair workflow
    /// </summary>
    public enum ProjectStatus
    {
        /// <summary>
        /// Specification just created, no Wyrm assigned yet
        /// </summary>
        New,

        /// <summary>
        /// Wyrm has been assigned to the project
        /// </summary>
        WyrmAssigned,

        /// <summary>
        /// Wyrm has analyzed the specification
        /// </summary>
        Analyzed,

        /// <summary>
        /// Tasks are being processed by Drakes
        /// </summary>
        InProgress,

        /// <summary>
        /// All tasks completed
        /// </summary>
        Completed,

        /// <summary>
        /// Error occurred during processing
        /// </summary>
        Failed
    }
}
