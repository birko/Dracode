namespace DraCode.KoboldLair.Models.Configuration
{
    /// <summary>
    /// Configuration for all agent types in a project
    /// </summary>
    public class AgentsConfig
    {
        /// <summary>
        /// Wyrm agent configuration (specification analysis)
        /// </summary>
        public AgentConfig Wyrm { get; set; } = new();

        /// <summary>
        /// Wyvern agent configuration (project analysis and task breakdown)
        /// </summary>
        public AgentConfig Wyvern { get; set; } = new();

        /// <summary>
        /// Drake agent configuration (task supervision)
        /// </summary>
        public AgentConfig Drake { get; set; } = new();

        /// <summary>
        /// Kobold Planner agent configuration (implementation planning)
        /// </summary>
        public AgentConfig KoboldPlanner { get; set; } = new();

        /// <summary>
        /// Kobold agent configuration (task execution)
        /// </summary>
        public AgentConfig Kobold { get; set; } = new();
    }
}
