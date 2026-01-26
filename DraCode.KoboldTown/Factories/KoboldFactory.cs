using DraCode.Agent;
using DraCode.KoboldTown.Agents;
using DraCode.KoboldTown.Models;
using System.Collections.Concurrent;

namespace DraCode.KoboldTown.Factories
{
    /// <summary>
    /// Factory for creating and managing Kobold worker agents.
    /// Maintains a registry of all Kobold instances for tracking and management.
    /// </summary>
    public class KoboldFactory
    {
        private readonly ConcurrentDictionary<Guid, Kobold> _kobolds;
        private readonly AgentOptions? _defaultOptions;
        private readonly Dictionary<string, string>? _defaultConfig;

        /// <summary>
        /// Gets the total number of Kobolds managed by this factory
        /// </summary>
        public int TotalKobolds => _kobolds.Count;

        /// <summary>
        /// Creates a new KoboldFactory with optional default settings
        /// </summary>
        public KoboldFactory(
            AgentOptions? defaultOptions = null,
            Dictionary<string, string>? defaultConfig = null)
        {
            _kobolds = new ConcurrentDictionary<Guid, Kobold>();
            _defaultOptions = defaultOptions;
            _defaultConfig = defaultConfig;
        }

        /// <summary>
        /// Creates a new Kobold with the specified provider and agent type
        /// </summary>
        /// <param name="provider">LLM provider: "openai", "azureopenai", "claude", "gemini", "ollama", "githubcopilot"</param>
        /// <param name="agentType">Type of agent: "csharp", "cpp", "javascript", "react", etc.</param>
        /// <param name="options">Optional agent options (overrides default)</param>
        /// <param name="config">Optional provider configuration (overrides default)</param>
        /// <returns>Newly created Kobold instance</returns>
        public Kobold CreateKobold(
            string provider,
            string agentType,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null)
        {
            var agent = KoboldTownAgentFactory.Create(
                provider,
                options ?? _defaultOptions,
                config ?? _defaultConfig,
                agentType
            );

            var kobold = new Kobold(agent, agentType);
            _kobolds.TryAdd(kobold.Id, kobold);

            return kobold;
        }

        /// <summary>
        /// Gets a Kobold by its ID
        /// </summary>
        public Kobold? GetKobold(Guid koboldId)
        {
            _kobolds.TryGetValue(koboldId, out var kobold);
            return kobold;
        }

        /// <summary>
        /// Gets all Kobolds
        /// </summary>
        public IReadOnlyCollection<Kobold> GetAllKobolds()
        {
            return _kobolds.Values.ToList();
        }

        /// <summary>
        /// Gets Kobolds by status
        /// </summary>
        public IReadOnlyCollection<Kobold> GetKoboldsByStatus(KoboldStatus status)
        {
            return _kobolds.Values
                .Where(k => k.Status == status)
                .ToList();
        }

        /// <summary>
        /// Gets all unassigned Kobolds
        /// </summary>
        public IReadOnlyCollection<Kobold> GetUnassignedKobolds()
        {
            return GetKoboldsByStatus(KoboldStatus.Unassigned);
        }

        /// <summary>
        /// Gets all working Kobolds
        /// </summary>
        public IReadOnlyCollection<Kobold> GetWorkingKobolds()
        {
            return GetKoboldsByStatus(KoboldStatus.Working);
        }

        /// <summary>
        /// Gets Kobold assigned to a specific task
        /// </summary>
        public Kobold? GetKoboldByTaskId(Guid taskId)
        {
            return _kobolds.Values.FirstOrDefault(k => k.TaskId == taskId);
        }

        /// <summary>
        /// Gets Kobolds by agent type
        /// </summary>
        public IReadOnlyCollection<Kobold> GetKoboldsByType(string agentType)
        {
            return _kobolds.Values
                .Where(k => k.AgentType.Equals(agentType, StringComparison.OrdinalIgnoreCase))
                .ToList();
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

    /// <summary>
    /// Statistics about Kobold instances
    /// </summary>
    public class KoboldStatistics
    {
        public int Total { get; init; }
        public int Unassigned { get; init; }
        public int Assigned { get; init; }
        public int Working { get; init; }
        public int Done { get; init; }
        public Dictionary<string, int> ByAgentType { get; init; } = new();

        public override string ToString()
        {
            return $"Total: {Total}, Unassigned: {Unassigned}, Assigned: {Assigned}, Working: {Working}, Done: {Done}";
        }
    }
}
