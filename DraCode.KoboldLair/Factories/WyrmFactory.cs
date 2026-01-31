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
        private readonly ConcurrentDictionary<Guid, string?> _activeWyrms; // Maps wyrm ID to project ID
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new WyrmFactory
        /// </summary>
        /// <param name="projectConfigService">Project configuration service for parallel limits</param>
        public WyrmFactory(ProjectConfigurationService projectConfigService)
        {
            _projectConfigService = projectConfigService;
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
    }
}
