namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Timestamps tracking project lifecycle
    /// </summary>
    public class ProjectTimestamps
    {
        /// <summary>
        /// When the project was created
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// When the project was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// When Wyvern analysis was completed (if applicable)
        /// </summary>
        public DateTime? AnalyzedAt { get; set; }

        /// <summary>
        /// When the specification was last processed by Wyvern
        /// </summary>
        public DateTime? LastProcessedAt { get; set; }
    }
}
