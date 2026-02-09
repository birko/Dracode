namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Status of a project in the KoboldLair workflow
    /// </summary>
    public enum ProjectStatus
    {
        /// <summary>
        /// Specification created but not yet confirmed by user.
        /// Dragon is still gathering requirements and refining the specification.
        /// </summary>
        Prototype,

        /// <summary>
        /// Specification confirmed by user, ready for Wyrm assignment
        /// </summary>
        New,

        /// <summary>
        /// Wyrm has been assigned to pre-analyze and recommend agent types
        /// </summary>
        WyrmAssigned,

        /// <summary>
        /// Wyvern has analyzed the specification based on Wyrm recommendations
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
