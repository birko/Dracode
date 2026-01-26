namespace DraCode.Agent
{
    /// <summary>
    /// Configuration options for agent behavior
    /// </summary>
    public class AgentOptions
    {
        /// <summary>
        /// Enable/disable interactive mode (user prompts). Default: true
        /// </summary>
        public bool Interactive { get; set; } = true;

        /// <summary>
        /// Maximum number of iterations for agent execution. Default: 10
        /// </summary>
        public int MaxIterations { get; set; } = 10;

        /// <summary>
        /// Enable verbose logging output. Default: true
        /// </summary>
        public bool Verbose { get; set; } = true;

        /// <summary>
        /// Working directory for agent operations
        /// </summary>
        public string WorkingDirectory { get; set; } = "./";

        /// <summary>
        /// Timeout for interactive prompts in seconds. Default: 300 (5 minutes)
        /// </summary>
        public int PromptTimeout { get; set; } = 300;

        /// <summary>
        /// Default response for prompts in non-interactive mode. 
        /// If null or empty, prompts will return an error in non-interactive mode.
        /// </summary>
        public string? DefaultPromptResponse { get; set; }

        /// <summary>
        /// Model thinking/reasoning depth level. Higher values encourage deeper reasoning.
        /// Range: 0-10, Default: 5
        /// 0-3: Quick/shallow reasoning
        /// 4-6: Balanced reasoning (default)
        /// 7-10: Deep/thorough reasoning
        /// </summary>
        public int ModelDepth { get; set; } = 5;

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        public AgentOptions Clone()
        {
            return new AgentOptions
            {
                Interactive = Interactive,
                MaxIterations = MaxIterations,
                Verbose = Verbose,
                WorkingDirectory = WorkingDirectory,
                PromptTimeout = PromptTimeout,
                DefaultPromptResponse = DefaultPromptResponse,
                ModelDepth = ModelDepth
            };
        }

        /// <summary>
        /// Merges another options object into this one, overwriting values
        /// </summary>
        public void Merge(AgentOptions other)
        {
            if (other == null) return;

            Interactive = other.Interactive;
            MaxIterations = other.MaxIterations;
            Verbose = other.Verbose;
            if (!string.IsNullOrEmpty(other.WorkingDirectory))
                WorkingDirectory = other.WorkingDirectory;
            PromptTimeout = other.PromptTimeout;
            if (other.DefaultPromptResponse != null)
                DefaultPromptResponse = other.DefaultPromptResponse;
            ModelDepth = other.ModelDepth;
        }

        /// <summary>
        /// Creates options from a dictionary (used for parsing config)
        /// </summary>
        public static AgentOptions FromDictionary(Dictionary<string, string> config)
        {
            var options = new AgentOptions();

            if (config.TryGetValue("interactive", out var interactive))
                options.Interactive = bool.Parse(interactive);

            if (config.TryGetValue("maxIterations", out var maxIterations))
                options.MaxIterations = int.Parse(maxIterations);

            if (config.TryGetValue("verbose", out var verbose))
                options.Verbose = bool.Parse(verbose);

            if (config.TryGetValue("workingDirectory", out var workingDirectory))
                options.WorkingDirectory = workingDirectory;

            if (config.TryGetValue("promptTimeout", out var promptTimeout))
                options.PromptTimeout = int.Parse(promptTimeout);

            if (config.TryGetValue("defaultPromptResponse", out var defaultPromptResponse))
                options.DefaultPromptResponse = defaultPromptResponse;

            if (config.TryGetValue("modelDepth", out var modelDepth))
                options.ModelDepth = int.Parse(modelDepth);

            return options;
        }

        /// <summary>
        /// Converts options to a dictionary
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>
            {
                ["interactive"] = Interactive.ToString(),
                ["maxIterations"] = MaxIterations.ToString(),
                ["verbose"] = Verbose.ToString(),
                ["workingDirectory"] = WorkingDirectory,
                ["promptTimeout"] = PromptTimeout.ToString(),
                ["modelDepth"] = ModelDepth.ToString()
            };

            if (DefaultPromptResponse != null)
                dict["defaultPromptResponse"] = DefaultPromptResponse;

            return dict;
        }
    }
}
