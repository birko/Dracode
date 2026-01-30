using DraCode.Agent;
using DraCode.KoboldLair.Server.Agents;
using DraCode.KoboldLair.Server.Models.Agents;
using DraCode.KoboldLair.Server.Services;
using System.Collections.Concurrent;
using KoboldModel = DraCode.KoboldLair.Server.Models.Agents.Kobold;

namespace DraCode.KoboldLair.Server.Factories
{
    /// <summary>
    /// Factory for creating and managing Kobold worker agents.
    /// Maintains a registry of all Kobold instances for tracking and management.
    /// </summary>
    public class KoboldFactory
    {
        private readonly ConcurrentDictionary<Guid, KoboldModel> _kobolds;
        private readonly AgentOptions? _defaultOptions;
        private readonly Dictionary<string, string>? _defaultConfig;
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly Func<string?, int> _getProjectMaxParallelKobolds;

        /// <summary>
        /// Gets the total number of Kobolds managed by this factory
        /// </summary>
        public int TotalKobolds => _kobolds.Count;

        /// <summary>
        /// Creates a new KoboldFactory with optional default settings
        /// </summary>
        public KoboldFactory(
            ProjectConfigurationService projectConfigService,
            Func<string?, int>? getProjectMaxParallelKobolds = null,
            AgentOptions? defaultOptions = null,
            Dictionary<string, string>? defaultConfig = null)
        {
            _kobolds = new ConcurrentDictionary<Guid, KoboldModel>();
            _projectConfigService = projectConfigService;
            _getProjectMaxParallelKobolds = getProjectMaxParallelKobolds ?? ((projectId) => projectConfigService.GetMaxParallelKobolds(projectId ?? string.Empty));
            _defaultOptions = defaultOptions;
            _defaultConfig = defaultConfig;
        }

        /// <summary>
        /// Creates a new Kobold with the specified provider and agent type
        /// </summary>
        /// <param name="provider">LLM provider: "openai", "azureopenai", "claude", "gemini", "ollama", "githubcopilot"</param>
        /// <param name="agentType">Type of agent: "csharp", "cpp", "javascript", "react", "php", "python", "svg", "bitmap", etc.</param>
        /// <param name="options">Optional agent options (overrides default)</param>
        /// <param name="config">Optional provider configuration (overrides default)</param>
        /// <returns>Newly created Kobold instance</returns>
        public KoboldModel CreateKobold(
            string provider,
            string agentType,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null)
        {
            var agent = KoboldLairAgentFactory.Create(
                provider,
                options ?? _defaultOptions,
                config ?? _defaultConfig,
                agentType
            );

            var kobold = new KoboldModel(agent, agentType);
            _kobolds.TryAdd(kobold.Id, kobold);

            return kobold;
        }

        /// <summary>
        /// Gets a Kobold by its ID
        /// </summary>
        public KoboldModel? GetKobold(Guid koboldId)
        {
            _kobolds.TryGetValue(koboldId, out var kobold);
            return kobold;
        }

        /// <summary>
        /// Gets all Kobolds
        /// </summary>
        public IReadOnlyCollection<KoboldModel> GetAllKobolds()
        {
            return _kobolds.Values.ToList();
        }

        /// <summary>
        /// Gets Kobolds by status
        /// </summary>
        public IReadOnlyCollection<KoboldModel> GetKoboldsByStatus(KoboldStatus status)
        {
            return _kobolds.Values
                .Where(k => k.Status == status)
                .ToList();
        }

        /// <summary>
        /// Gets all unassigned Kobolds
        /// </summary>
        public IReadOnlyCollection<KoboldModel> GetUnassignedKobolds()
        {
            return GetKoboldsByStatus(KoboldStatus.Unassigned);
        }

        /// <summary>
        /// Gets all working Kobolds
        /// </summary>
        public IReadOnlyCollection<KoboldModel> GetWorkingKobolds()
        {
            return GetKoboldsByStatus(KoboldStatus.Working);
        }

        /// <summary>
        /// Gets Kobold assigned to a specific task
        /// </summary>
        public KoboldModel? GetKoboldByTaskId(Guid taskId)
        {
            return _kobolds.Values.FirstOrDefault(k => k.TaskId == taskId);
        }

        /// <summary>
        /// Gets Kobolds by agent type
        /// </summary>
        public IReadOnlyCollection<KoboldModel> GetKoboldsByType(string agentType)
        {
            return _kobolds.Values
                .Where(k => k.AgentType.Equals(agentType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Gets the count of active kobolds (assigned or working) for a specific project
        /// </summary>
        public int GetActiveKoboldCountForProject(string? projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                return 0;
            }

            return _kobolds.Values
                .Count(k => k.ProjectId == projectId &&
                           (k.Status == KoboldStatus.Assigned || k.Status == KoboldStatus.Working));
        }

        /// <summary>
        /// Checks if a new kobold can be created for the specified project based on the parallel limit
        /// </summary>
        public bool CanCreateKoboldForProject(string? projectId)
        {
            var currentCount = GetActiveKoboldCountForProject(projectId);
            var maxAllowed = _getProjectMaxParallelKobolds(projectId);

            return currentCount < maxAllowed;
        }

        /// <summary>
        /// Removes a Kobold from the registry
        /// </summary>
        public bool RemoveKobold(Guid koboldId)
        {
            return _kobolds.TryRemove(koboldId, out _);
        }

        /// <summary>
        /// Removes all done Kobolds from the registry
        /// </summary>
        public int CleanupDoneKobolds()
        {
            var doneKobolds = GetKoboldsByStatus(KoboldStatus.Done);
            int removed = 0;

            foreach (var kobold in doneKobolds)
            {
                if (RemoveKobold(kobold.Id))
                {
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>
        /// Gets statistics about Kobolds
        /// </summary>
        public KoboldStatistics GetStatistics()
        {
            var allKobolds = _kobolds.Values.ToList();

            return new KoboldStatistics
            {
                Total = allKobolds.Count,
                Unassigned = allKobolds.Count(k => k.Status == KoboldStatus.Unassigned),
                Assigned = allKobolds.Count(k => k.Status == KoboldStatus.Assigned),
                Working = allKobolds.Count(k => k.Status == KoboldStatus.Working),
                Done = allKobolds.Count(k => k.Status == KoboldStatus.Done),
                ByAgentType = allKobolds
                    .GroupBy(k => k.AgentType)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        /// <summary>
        /// Clears all Kobolds from the registry
        /// </summary>
        public void Clear()
        {
            _kobolds.Clear();
        }
    }
}
