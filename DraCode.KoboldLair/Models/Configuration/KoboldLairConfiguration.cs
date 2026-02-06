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

        /// <summary>
        /// Configuration for agent iteration limits
        /// </summary>
        public IterationLimits Iterations { get; set; } = new();
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

        /// <summary>
        /// Whether to use enhanced execution with automatic step completion detection and auto-advancement (Phase 2).
        /// When enabled, Kobolds automatically detect completed steps even if agent doesn't explicitly mark them.
        /// (default: true)
        /// </summary>
        public bool UseEnhancedExecution { get; set; } = true;
    }

    /// <summary>
    /// Configuration for agent execution iteration limits.
    /// Controls how many iterations each agent type can perform per work session.
    /// </summary>
    public class IterationLimits
    {
        /// <summary>
        /// Maximum iterations for Kobold task execution (default: 100)
        /// Increased from 30 to allow more complex tasks to complete
        /// </summary>
        public int MaxKoboldIterations { get; set; } = 100;

        /// <summary>
        /// Maximum iterations for Dragon initial requirements gathering (default: 15)
        /// </summary>
        public int MaxDragonInitialIterations { get; set; } = 15;

        /// <summary>
        /// Maximum iterations for Dragon continuation messages (default: 25)
        /// </summary>
        public int MaxDragonContinueIterations { get; set; } = 25;

        /// <summary>
        /// Maximum iterations for Wyrm task delegation (default: 8)
        /// </summary>
        public int MaxWyrmIterations { get; set; } = 8;

        /// <summary>
        /// Maximum iterations for Wyvern analysis (default: 1)
        /// </summary>
        public int MaxWyvernIterations { get; set; } = 1;

        /// <summary>
        /// Maximum iterations for sub-agents (Sage, Seeker, Sentinel, Warden) (default: 15)
        /// </summary>
        public int MaxSubAgentIterations { get; set; } = 15;
    }
}
