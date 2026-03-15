using DraCode.Agent;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;
using System.Collections.Concurrent;

namespace DraCode.KoboldLair.Factories
{
    /// <summary>
    /// Factory for tracking and managing Wyrm task executions.
    /// Enforces parallel limits for Wyrm agents per project.
    /// </summary>
    public class WyrmFactory
    {
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ConcurrentDictionary<Guid, string?> _activeWyrms; // Maps wyrm ID to project ID
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new WyrmFactory
        /// </summary>
        /// <param name="projectConfigService">Project configuration service for parallel limits</param>
        /// <param name="providerConfigService">Provider configuration service</param>
        public WyrmFactory(
            ProjectConfigurationService projectConfigService,
            ProviderConfigurationService providerConfigService)
        {
            _projectConfigService = projectConfigService;
            _providerConfigService = providerConfigService;
            _activeWyrms = new ConcurrentDictionary<Guid, string?>();
        }

        /// <summary>
        /// Registers a new Wyrm execution and returns a tracking ID
        /// </summary>
        /// <param name="projectId">Optional project identifier for resource limiting</param>
        /// <returns>Tracking ID for the Wyrm execution</returns>
        public Guid RegisterWyrm(string? projectId = null)
        {
            var wyrmId = Guid.NewGuid();
            _activeWyrms.TryAdd(wyrmId, projectId);
            return wyrmId;
        }

        /// <summary>
        /// Unregisters a Wyrm execution when complete
        /// </summary>
        /// <param name="wyrmId">Tracking ID of the Wyrm execution</param>
        /// <returns>True if successfully unregistered</returns>
        public bool UnregisterWyrm(Guid wyrmId)
        {
            return _activeWyrms.TryRemove(wyrmId, out _);
        }

        /// <summary>
        /// Gets the count of active wyrms for a specific project
        /// </summary>
        public int GetActiveWyrmCountForProject(string? projectId)
        {
            return _activeWyrms.Count(kvp => kvp.Value == projectId);
        }

        /// <summary>
        /// Checks if a new wyrm can be created for the specified project based on the parallel limit
        /// </summary>
        public bool CanCreateWyrmForProject(string? projectId)
        {
            var currentCount = GetActiveWyrmCountForProject(projectId);
            var maxAllowed = _projectConfigService.GetMaxParallelWyrms(projectId ?? string.Empty);
            return currentCount < maxAllowed;
        }

        /// <summary>
        /// Gets the total number of active Wyrms
        /// </summary>
        public int TotalActiveWyrms => _activeWyrms.Count;

        /// <summary>
        /// Gets Wyrm statistics by project
        /// </summary>
        public Dictionary<string, int> GetStatisticsByProject()
        {
            return _activeWyrms.Values
                .GroupBy(projectId => projectId ?? "(default)")
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// Clears all tracked Wyrms (use with caution)
        /// </summary>
        public void Clear()
        {
            _activeWyrms.Clear();
        }

        /// <summary>
        /// Creates a new Wyrm task delegation agent (used by Drake for agent type selection).
        /// </summary>
        /// <param name="project">Project to analyze</param>
        /// <param name="options">Optional agent options override</param>
        /// <returns>Configured Wyrm agent</returns>
        public DraCode.Agent.Agents.Agent CreateWyrm(Project project, AgentOptions? options = null)
        {
            var (providerType, config, agentOptions) = GetProviderSettings(project, options);
            var koboldLairConfig = BuildKoboldLairConfig(providerType);

            var wyrm = KoboldLairAgentFactory.Create(providerType, koboldLairConfig, agentOptions, config, "wyrm");

            RegisterWyrm(project.Id);

            return wyrm;
        }

        /// <summary>
        /// Creates a new Wyrm pre-analysis agent for specification analysis (JSON output).
        /// This is distinct from CreateWyrm which creates the task delegation agent used by Drake.
        /// </summary>
        /// <param name="project">Project to analyze</param>
        /// <param name="options">Optional agent options override</param>
        /// <returns>Configured WyrmPreAnalysisAgent</returns>
        public WyrmPreAnalysisAgent CreateWyrmPreAnalysisAgent(Project project, AgentOptions? options = null)
        {
            var (providerType, config, agentOptions) = GetProviderSettings(project, options);
            var llmProvider = KoboldLairAgentFactory.CreateLlmProvider(providerType, config, "wyrm-preanalysis");

            var agent = new WyrmPreAnalysisAgent(llmProvider, agentOptions);

            RegisterWyrm(project.Id);

            return agent;
        }

        private (string providerType, Dictionary<string, string> config, AgentOptions agentOptions) GetProviderSettings(Project project, AgentOptions? options)
        {
            var (providerType, config, agentOptions) = _providerConfigService.GetProviderSettingsForAgent(
                "wyrm",
                project.Paths.Output
            );

            var finalOptions = options ?? agentOptions;
            return (providerType, config, finalOptions);
        }

        private static KoboldLairConfiguration BuildKoboldLairConfig(string providerType)
        {
            return new KoboldLairConfiguration
            {
                DefaultProvider = providerType,
                Providers = new List<ProviderConfig>
                {
                    new ProviderConfig
                    {
                        Name = providerType,
                        Type = providerType,
                        IsEnabled = true
                    }
                }
            };
        }
    }
}
