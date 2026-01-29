using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.KoboldLair.Server.Services;

namespace DraCode.KoboldLair.Server.Agents.Wyvern
{
    /// <summary>
    /// Factory for creating and managing Wyvern instances.
    /// One Wyvern per project - reads specifications and creates organized tasks.
    /// </summary>
    public class WyvernFactory
    {
        private readonly Dictionary<string, Wyvern> _Wyverns;
        private readonly object _lock = new object();

        private readonly ProviderConfigurationService _providerConfigService;
        private readonly AgentOptions _defaultOptions;

        public WyvernFactory(
            ProviderConfigurationService providerConfigService,
            AgentOptions? defaultOptions = null)
        {
            _Wyverns = new Dictionary<string, Wyvern>(StringComparer.OrdinalIgnoreCase);
            _providerConfigService = providerConfigService;
            _defaultOptions = defaultOptions ?? new AgentOptions { WorkingDirectory = "./workspace", Verbose = false };
        }

        /// <summary>
        /// Creates a new Wyvern for a project
        /// </summary>
        public Wyvern CreateWyvern(
            string projectName,
            string specificationPath,
            string outputPath = "./tasks",
            string? provider = null,
            string? model = null)
        {
            lock (_lock)
            {
                if (_Wyverns.ContainsKey(projectName))
                {
                    throw new InvalidOperationException($"Wyvern already exists for project: {projectName}");
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

                var analyzerAgent = (WyvernAnalyzerAgent)KoboldLairAgentFactory.Create("wyvernanalyzer", options, config);

                var Wyvern = new Wyvern(
                    projectName,
                    specificationPath,
                    analyzerAgent,
                    effectiveProvider,
                    config,
                    options,
                    outputPath
                );

                _Wyverns[projectName] = Wyvern;

                return Wyvern;
            }
        }

        /// <summary>
        /// Gets an existing Wyvern by project name
        /// </summary>
        public Wyvern? GetWyvern(string projectName)
        {
            lock (_lock)
            {
                return _Wyverns.GetValueOrDefault(projectName);
            }
        }

        /// <summary>
        /// Gets all Wyverns
        /// </summary>
        public IEnumerable<Wyvern> GetAllWyverns()
        {
            lock (_lock)
            {
                return _Wyverns.Values.ToList();
            }
        }

        /// <summary>
        /// Removes a Wyvern
        /// </summary>
        public bool RemoveWyvern(string projectName)
        {
            lock (_lock)
            {
                return _Wyverns.Remove(projectName);
            }
        }

        /// <summary>
        /// Total number of Wyverns
        /// </summary>
        public int TotalWyverns
        {
            get
            {
                lock (_lock)
                {
                    return _Wyverns.Count;
                }
            }
        }
    }
}