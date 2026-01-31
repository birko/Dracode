namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// Provider and model settings for a specific Kobold agent type.
    /// Allows configuring different LLM providers for different agent types
    /// (e.g., use Claude for csharp Kobolds, OpenAI for python Kobolds).
    /// </summary>
    public class KoboldAgentTypeProviderSettings
    {
        /// <summary>
        /// The agent type this setting applies to (e.g., "csharp", "python", "react")
        /// </summary>
        public string AgentType { get; set; } = string.Empty;

        /// <summary>
        /// Provider name to use for this agent type (null = use global KoboldProvider)
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Optional model override for this agent type (null = use provider default)
        /// </summary>
        public string? Model { get; set; }
    }
}
