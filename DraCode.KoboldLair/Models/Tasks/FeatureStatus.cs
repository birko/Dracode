namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// Status of a feature in the development lifecycle
    /// </summary>
    public enum FeatureStatus
    {
        /// <summary>
        /// Feature is in draft state - newly created, can be modified, NOT ready for processing
        /// </summary>
        Draft = 0,

        /// <summary>
        /// Feature is ready for Wyvern to process
        /// </summary>
        Ready = 1,

        /// <summary>
        /// Feature has been assigned to Wyvern for task breakdown
        /// </summary>
        AssignedToWyvern = 2,

        /// <summary>
        /// Feature is being worked on by Kobolds
        /// </summary>
        InProgress = 3,

        /// <summary>
        /// Feature implementation is completed
        /// </summary>
        Completed = 4,

        /// <summary>
        /// Legacy status for backwards compatibility - treated as Draft
        /// </summary>
        [System.Obsolete("Use Draft instead")]
        New = 0
    }
}
