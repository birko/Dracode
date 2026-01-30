namespace DraCode.KoboldLair.Server.Models.Configuration
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
        /// Default maximum parallel drakes for projects without specific configuration
        /// </summary>
        public int DefaultMaxParallelDrakes { get; set; } = 1;

        /// <summary>
        /// Default maximum parallel wyrms for projects without specific configuration
        /// </summary>
        public int DefaultMaxParallelWyrms { get; set; } = 1;

        /// <summary>
        /// Default maximum parallel wyverns for projects without specific configuration
        /// </summary>
        public int DefaultMaxParallelWyverns { get; set; } = 1;

        /// <summary>
        /// Collection of project-specific configurations
        /// </summary>
        public List<ProjectConfig> Projects { get; set; } = new();
    }
}
