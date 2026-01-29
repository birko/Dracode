namespace DraCode.KoboldLair.Server.Models
{
    /// <summary>
    /// Configuration for an LLM provider
    /// </summary>
    public class ProviderConfig
    {
        /// <summary>
        /// Unique identifier for the provider (e.g., "openai", "claude", "ollama")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the provider
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Provider type (matches the provider name used in agent factory)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Default model to use for this provider
        /// </summary>
        public string DefaultModel { get; set; } = string.Empty;

        /// <summary>
        /// List of agents this provider can be used with
        /// Examples: "dragon", "wyvern", "wyrm", "kobold", "all"
        /// </summary>
        public List<string> CompatibleAgents { get; set; } = new();

        /// <summary>
        /// Whether this provider is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Provider-specific configuration (API keys, endpoints, etc.)
        /// </summary>
        public Dictionary<string, string> Configuration { get; set; } = new();

        /// <summary>
        /// Whether this provider requires an API key
        /// </summary>
        public bool RequiresApiKey { get; set; } = true;

        /// <summary>
        /// Description of the provider
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration for which provider to use for each agent type
    /// </summary>
    public class AgentProviderSettings
    {
        /// <summary>
        /// Provider to use for Dragon (requirements gathering) agent
        /// </summary>
        public string DragonProvider { get; set; } = "openai";

        /// <summary>
        /// Provider to use for Wyvern (task delegation) agent
        /// </summary>
        public string WyvernProvider { get; set; } = "openai";

        /// <summary>
        /// Provider to use for Wyrm (task delegation) agent
        /// </summary>
        public string WyrmProvider { get; set; } = "openai";

        /// <summary>
        /// Provider to use for Kobold (worker) agents
        /// </summary>
        public string KoboldProvider { get; set; } = "openai";

        /// <summary>
        /// Optional: Override model for Dragon
        /// </summary>
        public string? DragonModel { get; set; }

        /// <summary>
        /// Optional: Override model for Wyvern
        /// </summary>
        public string? WyvernModel { get; set; }

        /// <summary>
        /// Optional: Override model for Wyrm
        /// </summary>
        public string? WyrmModel { get; set; }


        /// <summary>
        /// Optional: Override model for Kobolds
        /// </summary>
        public string? KoboldModel { get; set; }
    }

    /// <summary>
    /// Root configuration containing all provider settings
    /// </summary>
    public class KoboldLairProviderConfiguration
    {
        /// <summary>
        /// List of available providers
        /// </summary>
        public List<ProviderConfig> Providers { get; set; } = new();

        /// <summary>
        /// Active agent-to-provider mappings
        /// </summary>
        public AgentProviderSettings AgentProviders { get; set; } = new();
    }
}
