namespace DraCode.KoboldLair.Models.Configuration
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

        /// <summary>
        /// Path for storing projects. Defaults to current working directory + "./projects"
        /// </summary>
        public string ProjectsPath { get; set; } = "./projects";

        /// <summary>
        /// Configuration for Kobold implementation planning
        /// </summary>
        public PlanningConfiguration Planning { get; set; } = new();
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

        /// <summary>
        /// Interval in seconds between Drake monitoring runs (default: 60)
        /// </summary>
        public int MonitoringIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Timeout in minutes before a working Kobold is considered stuck (default: 30)
        /// </summary>
        public int StuckKoboldTimeoutMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Configuration for Kobold implementation planning.
    /// Plans enable resumability and visibility of task execution.
    /// </summary>
    public class PlanningConfiguration
    {
        /// <summary>
        /// Whether implementation planning is enabled (default: true)
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Provider to use for the planner agent (null uses default Kobold provider)
        /// </summary>
        public string? PlannerProvider { get; set; }

        /// <summary>
        /// Model to use for the planner agent (null uses provider default)
        /// </summary>
        public string? PlannerModel { get; set; }

        /// <summary>
        /// Maximum iterations for plan generation (default: 5)
        /// </summary>
        public int MaxPlanningIterations { get; set; } = 5;

        /// <summary>
        /// Whether to save plan progress after each step (default: true)
        /// </summary>
        public bool SavePlanProgress { get; set; } = true;

        /// <summary>
        /// Whether to resume from saved plans on restart (default: true)
        /// </summary>
        public bool ResumeFromPlan { get; set; } = true;
    }
}
