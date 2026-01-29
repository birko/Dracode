namespace DraCode.KoboldLair.Server.Models
{
    /// <summary>
    /// Status of a project in the KoboldLair workflow
    /// </summary>
    public enum ProjectStatus
    {
        /// <summary>
        /// Specification just created, no Wyvern assigned yet
        /// </summary>
        New,

        /// <summary>
        /// Wyvern has been assigned to the project
        /// </summary>
        WyvernAssigned,

        /// <summary>
        /// Wyvern has analyzed the specification
        /// </summary>
        Analyzed,

        /// <summary>
        /// Specification was modified after analysis - needs reprocessing
        /// </summary>
        SpecificationModified,

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
