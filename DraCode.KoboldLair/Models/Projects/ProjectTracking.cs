namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Tracking data for project processing
    /// </summary>
    public class ProjectTracking
    {
        /// <summary>
        /// Areas that still need tasks assigned (for incremental processing)
        /// </summary>
        public List<string> PendingAreas { get; set; } = new();

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Hash of specification content at last processing
        /// </summary>
        public string? LastProcessedContentHash { get; set; }

        /// <summary>
        /// ID of the specification associated with this project
        /// </summary>
        public string? SpecificationId { get; set; }

        /// <summary>
        /// ID of the Wyvern assigned to this project
        /// </summary>
        public string? WyvernId { get; set; }
    }
}
