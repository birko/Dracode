namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// Metadata for a project configuration
    /// </summary>
    public class MetadataConfig
    {
        /// <summary>
        /// Timestamp when configuration was last updated
        /// </summary>
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// Timestamp when the project was created
        /// </summary>
        public DateTime? CreatedAt { get; set; }
    }
}
