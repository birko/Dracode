namespace DraCode.KoboldLair.Server.Models
{
    /// <summary>
    /// Configuration for a specific project including resource limits, providers, and agent settings
    /// </summary>
    public class ProjectConfig
    {
        /// <summary>
        /// Unique identifier for the project
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// Optional project name for display purposes
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// Maximum number of kobolds that can run in parallel for this project.
        /// Default: 1
        /// </summary>
        public int MaxParallelKobolds { get; set; } = 1;

        /// <summary>
        /// Provider used for the Wyrm agent analyzing this project
        /// </summary>
        public string? WyrmProvider { get; set; }

        /// <summary>
        /// Model override for Wyrm (if any)
        /// </summary>
        public string? WyrmModel { get; set; }

        /// <summary>
        /// Whether Wyrm analysis is enabled for this project
        /// </summary>
        public bool WyrmEnabled { get; set; } = false;

        /// <summary>
        /// Provider used for Drake supervisors in this project
        /// </summary>
        public string? DrakeProvider { get; set; }

        /// <summary>
        /// Model override for Drakes (if any)
        /// </summary>
        public string? DrakeModel { get; set; }

        /// <summary>
        /// Whether Drake supervisors are enabled for this project
        /// </summary>
        public bool DrakeEnabled { get; set; } = false;

        /// <summary>
        /// Provider used for Kobold workers in this project
        /// </summary>
        public string? KoboldProvider { get; set; }

        /// <summary>
        /// Model override for Kobolds (if any)
        /// </summary>
        public string? KoboldModel { get; set; }

        /// <summary>
        /// Whether Kobold workers are enabled for this project
        /// </summary>
        public bool KoboldEnabled { get; set; } = false;

        /// <summary>
        /// Timestamp when configuration was last updated
        /// </summary>
        public DateTime? LastUpdated { get; set; }
    }

    /// <summary>
    /// Root configuration container for all project configurations
    /// </summary>
    public class ProjectConfigurations
    {
        /// <summary>
        /// Default maximum parallel kobolds for projects without specific configuration
        /// </summary>
        public int DefaultMaxParallelKobolds { get; set; } = 1;

        /// <summary>
        /// Collection of project-specific configurations
        /// </summary>
        public List<ProjectConfig> Projects { get; set; } = new();
    }
}
