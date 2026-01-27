namespace DraCode.KoboldLair.Server.Models
{
    /// <summary>
    /// Status of a feature in the development lifecycle
    /// </summary>
    public enum FeatureStatus
    {
        /// <summary>
        /// Feature is newly created and can be modified by Dragon
        /// </summary>
        New = 0,

        /// <summary>
        /// Feature has been assigned to Wyrm for task breakdown
        /// </summary>
        AssignedToWyrm = 1,

        /// <summary>
        /// Feature is being worked on by Kobolds
        /// </summary>
        InProgress = 2,

        /// <summary>
        /// Feature implementation is completed
        /// </summary>
        Completed = 3
    }
}
