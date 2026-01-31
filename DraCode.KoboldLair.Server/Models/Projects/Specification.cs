using DraCode.KoboldLair.Server.Models.Tasks;

namespace DraCode.KoboldLair.Server.Models.Projects
{
    /// <summary>
    /// Represents a project specification with associated features
    /// </summary>
    public class Specification
    {
        /// <summary>
        /// Unique identifier for the specification
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Name of the specification
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Full markdown content of the specification
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Path to the specification file on disk
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// Path to the project folder containing this specification and related files
        /// </summary>
        public string ProjectFolder { get; set; } = "";

        /// <summary>
        /// ID of the project this specification belongs to
        /// </summary>
        public string ProjectId { get; set; } = "";

        /// <summary>
        /// Features defined in this specification
        /// </summary>
        public List<Feature> Features { get; set; } = new();

        /// <summary>
        /// When the specification was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the specification was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Version number of the specification
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
