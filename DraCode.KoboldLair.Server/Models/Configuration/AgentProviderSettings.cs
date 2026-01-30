namespace DraCode.KoboldLair.Server.Models.Configuration
{
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
}
