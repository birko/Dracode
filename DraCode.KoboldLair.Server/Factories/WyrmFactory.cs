using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.KoboldLair.Server.Agents;
using DraCode.KoboldLair.Server.Projects;
using DraCode.KoboldLair.Server.Services;

namespace DraCode.KoboldLair.Server.Factories
{
    /// <summary>
    /// Factory for creating and managing Wyrm instances.
    /// One Wyrm per project - reads specifications and creates organized tasks.
    /// </summary>
    public class WyrmFactory
    {
        private readonly Dictionary<string, Wyrm> _wyrms;
        private readonly object _lock = new object();
        
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly AgentOptions _defaultOptions;

        public WyrmFactory(
            ProviderConfigurationService providerConfigService,
            AgentOptions? defaultOptions = null)
        {
            _wyrms = new Dictionary<string, Wyrm>(StringComparer.OrdinalIgnoreCase);
            _providerConfigService = providerConfigService;
            _defaultOptions = defaultOptions ?? new AgentOptions { WorkingDirectory = "./workspace", Verbose = false };
        }

        /// <summary>
        /// Creates a new Wyrm for a project
        /// </summary>
        public Wyrm CreateWyrm(
            string projectName,
            string specificationPath,
            string outputPath = "./tasks",
            string? provider = null,
            string? model = null)
        {
            lock (_lock)
            {
                if (_wyrms.ContainsKey(projectName))
                {
                    throw new InvalidOperationException($"Wyrm already exists for project: {projectName}");
                }

                // Get provider settings
                string effectiveProvider;
                Dictionary<string, string> config;
                AgentOptions options;

                if (provider != null)
                {
                    // Use specified provider
                    (effectiveProvider, config, options) = _providerConfigService.GetProviderSettingsForAgent("wyvern", outputPath);
                    effectiveProvider = provider;
                    if (model != null)
                    {
                        config["model"] = model;
                    }
                }
                else
                {
                    // Use configured provider
                    (effectiveProvider, config, options) = _providerConfigService.GetProviderSettingsForAgent("wyvern", outputPath);
                }

                var llmProvider = KoboldLairAgentFactory.CreateLlmProvider(effectiveProvider, config);
                var analyzerAgent = new WyrmAnalyzerAgent(llmProvider, options);

                var wyrm = new Wyrm(
                    projectName,
                    specificationPath,
                    analyzerAgent,
                    effectiveProvider,
                    config,
                    options,
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
    }
}