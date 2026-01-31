namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// User runtime settings that persist across restarts.
    /// Saved to user-settings.json, separate from provider definitions.
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// Per-agent-type provider/model settings for Kobolds.
        /// Allows different LLM providers for different agent types
        /// (e.g., use Claude for csharp Kobolds, OpenAI for python Kobolds).
        /// </summary>
        public List<KoboldAgentTypeProviderSettings> KoboldAgentTypeSettings { get; set; } = new();

        /// <summary>
        /// Provider name to use for Dragon agent (null = use default)
        /// </summary>
        public string? DragonProvider { get; set; }

        /// <summary>
        /// Provider name to use for Wyrm agent (null = use default)
        /// </summary>
        public string? WyrmProvider { get; set; }

        /// <summary>
        /// Provider name to use for Wyvern agent (null = use default)
        /// </summary>
        public string? WyvernProvider { get; set; }

        /// <summary>
        /// Provider name to use for Kobold agents (null = use default)
        /// </summary>
        public string? KoboldProvider { get; set; }

        /// <summary>
        /// Optional model override for Dragon (null = use provider default)
        /// </summary>
        public string? DragonModel { get; set; }

        /// <summary>
        /// Optional model override for Wyrm (null = use provider default)
        /// </summary>
        public string? WyrmModel { get; set; }

        /// <summary>
        /// Optional model override for Wyvern (null = use provider default)
        /// </summary>
        public string? WyvernModel { get; set; }

        /// <summary>
        /// Optional model override for Kobold (null = use provider default)
        /// </summary>
        public string? KoboldModel { get; set; }
    }
}
