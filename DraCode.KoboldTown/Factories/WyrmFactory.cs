using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.KoboldTown.Agents;
using DraCode.KoboldTown.Projects;

namespace DraCode.KoboldTown.Factories
{
    /// <summary>
    /// Factory for creating and managing Wyrm instances.
    /// One Wyrm per project - reads specifications and creates organized tasks.
    /// </summary>
    public class WyrmFactory
    {
        private readonly Dictionary<string, Wyrm> _wyrms;
        private readonly object _lock = new object();
        
        private readonly string _defaultProvider;
        private readonly Dictionary<string, string> _defaultConfig;
        private readonly AgentOptions _defaultOptions;

        public WyrmFactory(
            string defaultProvider = "openai",
            Dictionary<string, string>? defaultConfig = null,
            AgentOptions? defaultOptions = null)
        {
            _wyrms = new Dictionary<string, Wyrm>(StringComparer.OrdinalIgnoreCase);
            _defaultProvider = defaultProvider;
            _defaultConfig = defaultConfig ?? new Dictionary<string, string>();
            _defaultOptions = defaultOptions ?? new AgentOptions { WorkingDirectory = "./workspace", Verbose = false };
        }

        /// <summary>
        /// Creates a new Wyrm for a project
        /// </summary>
        public Wyrm CreateWyrm(
            string projectName,
            string specificationPath,
            string outputPath = "./tasks",
            string? provider = null)
        {
            lock (_lock)
            {
                if (_wyrms.ContainsKey(projectName))
                {
                    throw new InvalidOperationException($"Wyrm already exists for project: {projectName}");
                }

                var effectiveProvider = provider ?? _defaultProvider;
                var llmProvider = CreateLlmProvider(effectiveProvider);

                var analyzerAgent = new WyrmAnalyzerAgent(llmProvider, _defaultOptions);

                var wyrm = new Wyrm(
                    projectName,
                    specificationPath,
                    analyzerAgent,
                    effectiveProvider,
                    _defaultConfig,
                    _defaultOptions,
                    outputPath
                );

                _wyrms[projectName] = wyrm;

                return wyrm;
            }
        }

        /// <summary>
        /// Gets an existing Wyrm by project name
        /// </summary>
        public Wyrm? GetWyrm(string projectName)
        {
            lock (_lock)
            {
                return _wyrms.GetValueOrDefault(projectName);
            }
        }

        /// <summary>
        /// Gets all Wyrms
        /// </summary>
        public IEnumerable<Wyrm> GetAllWyrms()
        {
            lock (_lock)
            {
                return _wyrms.Values.ToList();
            }
        }

        /// <summary>
        /// Removes a Wyrm
        /// </summary>
        public bool RemoveWyrm(string projectName)
        {
            lock (_lock)
            {
                return _wyrms.Remove(projectName);
            }
        }

        /// <summary>
        /// Total number of Wyrms
        /// </summary>
        public int TotalWyrms
        {
            get
            {
                lock (_lock)
                {
                    return _wyrms.Count;
                }
            }
        }

        private ILlmProvider CreateLlmProvider(string provider)
        {
            return provider.ToLowerInvariant() switch
            {
                "openai" => new OpenAiProvider(
                    _defaultConfig.GetValueOrDefault("apiKey", Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? ""),
                    _defaultConfig.GetValueOrDefault("model", "gpt-4o")
                ),
                "claude" or "anthropic" => new ClaudeProvider(
                    _defaultConfig.GetValueOrDefault("apiKey", Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? ""),
                    _defaultConfig.GetValueOrDefault("model", "claude-sonnet-4.5")
                ),
                "azure" or "azureopenai" => new AzureOpenAiProvider(
                    _defaultConfig.GetValueOrDefault("endpoint", Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? ""),
                    _defaultConfig.GetValueOrDefault("apiKey", Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? ""),
                    _defaultConfig.GetValueOrDefault("deployment", "gpt-4")
                ),
                _ => throw new ArgumentException($"Unknown provider: {provider}")
            };
        }
    }
}