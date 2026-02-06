using DraCode.KoboldLair.Models.Configuration;

namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Represents a project in the KoboldLair system.
    /// Created when Dragon generates a specification, assigned a Wyvern for analysis.
    /// Unified model combining project data and configuration.
    /// </summary>
    public class Project
    {
        /// <summary>
        /// Unique identifier for the project
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Name of the project
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Current status of the project
        /// </summary>
        public ProjectStatus Status { get; set; } = ProjectStatus.Prototype;

        /// <summary>
        /// File paths associated with the project
        /// </summary>
        public ProjectPaths Paths { get; set; } = new();

        /// <summary>
        /// Timestamps tracking project lifecycle
        /// </summary>
        public ProjectTimestamps Timestamps { get; set; } = new();

        /// <summary>
        /// Tracking data for project processing
        /// </summary>
        public ProjectTracking Tracking { get; set; } = new();

        /// <summary>
        /// Agent configuration for this project
        /// </summary>
        public AgentsConfig Agents { get; set; } = new();

        /// <summary>
        /// Security settings for the project
        /// </summary>
        public SecurityConfig Security { get; set; } = new();

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
