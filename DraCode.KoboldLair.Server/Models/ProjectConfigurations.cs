namespace DraCode.KoboldLair.Server.Models
{
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
