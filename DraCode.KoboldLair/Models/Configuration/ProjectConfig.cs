namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// Configuration for a specific project including resource limits, providers, and agent settings.
    /// Uses a sectioned structure for better organization.
    /// </summary>
    public class ProjectConfig
    {
        /// <summary>
        /// Project identity information (ID and name)
        /// </summary>
        public ProjectIdentity Project { get; set; } = new();

        /// <summary>
        /// Configuration for all agent types
        /// </summary>
        public AgentsConfig Agents { get; set; } = new();

        /// <summary>
        /// Security settings for the project
        /// </summary>
        public SecurityConfig Security { get; set; } = new();

        /// <summary>
        /// Metadata for tracking and auditing
        /// </summary>
        public MetadataConfig Metadata { get; set; } = new();

        /// <summary>
        /// Gets the agent configuration for a specific agent type
        /// </summary>
        public AgentConfig GetAgentConfig(string agentType)
        {
            return agentType.ToLowerInvariant() switch
            {
                "wyrm" => Agents.Wyrm,
                "wyvern" => Agents.Wyvern,
                "drake" => Agents.Drake,
                "kobold-planner" or "koboldplanner" or "planner" => Agents.KoboldPlanner,
                "kobold" => Agents.Kobold,
                _ => throw new ArgumentException($"Unknown agent type: {agentType}")
            };
        }

        /// <summary>
        /// Sets the agent configuration for a specific agent type
        /// </summary>
        public void SetAgentConfig(string agentType, AgentConfig config)
        {
            switch (agentType.ToLowerInvariant())
            {
                case "wyrm":
                    Agents.Wyrm = config;
                    break;
                case "wyvern":
                    Agents.Wyvern = config;
                    break;
                case "drake":
                    Agents.Drake = config;
                    break;
                case "kobold-planner":
                case "koboldplanner":
                case "planner":
                    Agents.KoboldPlanner = config;
                    break;
                case "kobold":
                    Agents.Kobold = config;
                    break;
                default:
                    throw new ArgumentException($"Unknown agent type: {agentType}");
            }
        }
    }
}
