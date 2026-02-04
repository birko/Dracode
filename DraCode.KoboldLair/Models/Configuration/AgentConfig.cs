namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// Configuration for a specific agent type within a project
    /// </summary>
    public class AgentConfig
    {
        /// <summary>
        /// Whether this agent type is enabled for the project
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Provider to use for this agent (e.g., "openai", "claude", "zai")
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Model override for this agent (e.g., "gpt-4o", "claude-sonnet-4-20250514")
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Maximum number of this agent type that can run in parallel
        /// </summary>
        public int MaxParallel { get; set; } = 1;

        /// <summary>
        /// Timeout in seconds for this agent type (0 = no timeout)
        /// </summary>
        public int Timeout { get; set; } = 0;
    }
}
