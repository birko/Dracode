namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// Container for per-project configurations.
    /// Default limits come from appsettings.json (KoboldLair.Limits section).
    /// This file only contains project-specific overrides.
    /// </summary>
    public class ProjectConfigurations
    {
        /// <summary>
        /// Collection of project-specific configurations
        /// </summary>
        public List<ProjectConfig> Projects { get; set; } = new();
    }
}
