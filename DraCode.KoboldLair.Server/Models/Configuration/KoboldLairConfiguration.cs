namespace DraCode.KoboldLair.Server.Models.Configuration
{
    /// <summary>
    /// Unified configuration for KoboldLair.
    /// All provider and limit settings are defined here.
    /// </summary>
    public class KoboldLairConfiguration
    {
        /// <summary>
        /// List of available LLM providers
        /// </summary>
        public List<ProviderConfig> Providers { get; set; } = new();

        /// <summary>
        /// Default provider name used for all agents unless overridden
        /// </summary>
        public string DefaultProvider { get; set; } = "openai";

        /// <summary>
        /// Default parallel execution limits for agents
        /// </summary>
        public AgentLimits Limits { get; set; } = new();
    }

    /// <summary>
    /// Default parallel execution limits for agents.
    /// Can be overridden per-project in project-configs.json.
    /// </summary>
    public class AgentLimits
    {
        public int MaxParallelKobolds { get; set; } = 1;
        public int MaxParallelDrakes { get; set; } = 1;
        public int MaxParallelWyrms { get; set; } = 1;
        public int MaxParallelWyverns { get; set; } = 1;
    }
}
