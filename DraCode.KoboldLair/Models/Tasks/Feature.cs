namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// Represents a feature within a specification
    /// </summary>
    public class Feature
    {
        /// <summary>
        /// Unique identifier for the feature
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Name of the feature
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Detailed description of the feature
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Current status of the feature
        /// </summary>
        public FeatureStatus Status { get; set; } = FeatureStatus.New;

        /// <summary>
        /// ID of the specification this feature belongs to
        /// </summary>
        public string SpecificationId { get; set; } = "";

        /// <summary>
        /// When the feature was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the feature was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Priority of the feature
        /// </summary>
        public string Priority { get; set; } = "medium";

        /// <summary>
        /// Additional metadata about the feature
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// IDs of tasks created for this feature
        /// </summary>
        public List<string> TaskIds { get; set; } = new();

        /// <summary>
        /// Git branch name for this feature (e.g., "feature/abc123-user-auth")
        /// Null if git is not enabled for the project
        /// </summary>
        public string? GitBranch { get; set; }
    }
}
