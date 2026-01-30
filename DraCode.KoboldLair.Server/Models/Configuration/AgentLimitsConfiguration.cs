namespace DraCode.KoboldLair.Server.Models.Configuration
{
    /// <summary>
    /// Global configuration for agent parallel execution limits.
    /// These values can be overridden per-project in project-configs.json.
    /// </summary>
    public class AgentLimitsConfiguration
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
    }
}
