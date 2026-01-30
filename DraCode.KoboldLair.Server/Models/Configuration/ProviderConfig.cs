namespace DraCode.KoboldLair.Server.Models.Configuration
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
}
