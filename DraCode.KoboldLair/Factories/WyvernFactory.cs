using DraCode.Agent;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Factories
{
    /// <summary>
    /// Factory for creating and managing Wyvern instances.
    /// One Wyvern per project - reads specifications and creates organized tasks.
    /// </summary>
    public class WyvernFactory
    {
        private readonly Dictionary<string, Wyvern> _Wyverns;
        private readonly Dictionary<string, string?> _wyvernProjectIds; // Maps wyvern name to project ID
        private readonly object _lock = new object();

        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly KoboldLairConfiguration _koboldLairConfig;
        private readonly GitService? _gitService;
        private readonly AgentOptions _defaultOptions;

        public WyvernFactory(
            ProviderConfigurationService providerConfigService,
            ProjectConfigurationService projectConfigService,
            KoboldLairConfiguration koboldLairConfig,
            AgentOptions? defaultOptions = null,
            GitService? gitService = null)
        {
            _Wyverns = new Dictionary<string, Wyvern>(StringComparer.OrdinalIgnoreCase);
            _wyvernProjectIds = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            _providerConfigService = providerConfigService;
            _projectConfigService = projectConfigService;
            _koboldLairConfig = koboldLairConfig;
            _gitService = gitService;
            _defaultOptions = defaultOptions ?? new AgentOptions { WorkingDirectory = "./workspace", Verbose = false };
        }

        /// <summary>
        /// Creates a new Wyvern for a project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <param name="specificationPath">Path to the specification file</param>
        /// <param name="outputPath">Output path for task files</param>
        /// <param name="wyvernProvider">Optional override for Wyvern provider</param>
        /// <param name="wyvernModel">Optional override for Wyvern model</param>
        /// <param name="wyrmProvider">Optional override for Wyrm provider</param>
        /// <param name="wyrmModel">Optional override for Wyrm model</param>
        /// <param name="projectId">Optional project identifier for resource limiting</param>
        public Wyvern CreateWyvern(
            string projectName,
            string specificationPath,
            string outputPath = "./tasks",
            string? wyvernProvider = null,
            string? wyvernModel = null,
            string? wyrmProvider = null,
            string? wyrmModel = null,
            string? projectId = null)
        {
            lock (_lock)
            {
                // Check if wyvern already exists (also check sanitized name match)
                var existingWyvern = GetWyvernInternal(projectName);
                if (existingWyvern != null)
                {
                    throw new InvalidOperationException($"Wyvern already exists for project: {projectName}");
                }

                // Get Wyvern provider settings
                string effectiveWyvernProvider;
                Dictionary<string, string> wyvernConfig;
                AgentOptions wyvernOptions;

                // Set working directory to workspace subfolder
                var workspacePath = Path.Combine(outputPath, "workspace");
                (effectiveWyvernProvider, wyvernConfig, wyvernOptions) = _providerConfigService.GetProviderSettingsForAgent("wyvern", workspacePath);
                if (wyvernProvider != null)
                {
                    // Use specified provider
                    effectiveWyvernProvider = wyvernProvider;
                    if (wyvernModel != null)
                    {
                        wyvernConfig["model"] = wyvernModel;
                    }
                }

                // Get Wyrm provider settings (separate from Wyvern)
                string effectiveWyrmProvider;
                Dictionary<string, string> wyrmConfig;
                AgentOptions wyrmOptions;

                // Set working directory to workspace subfolder
                (effectiveWyrmProvider, wyrmConfig, wyrmOptions) = _providerConfigService.GetProviderSettingsForAgent("wyrm", workspacePath);
                if (wyrmProvider != null)
                {
                    // Use specified provider override
                    effectiveWyrmProvider = wyrmProvider;
                    if (wyrmModel != null)
                    {
                        wyrmConfig["model"] = wyrmModel;
                    }
                }

                var analyzerAgent = (WyvernAgent)KoboldLairAgentFactory.Create(effectiveWyvernProvider, _koboldLairConfig, wyvernOptions, wyvernConfig, "wyvern");

                var wyvern = new Wyvern(
                    projectName,
                    specificationPath,
                    analyzerAgent,
                    effectiveWyvernProvider,
                    wyvernConfig,
                    wyvernOptions,
                    outputPath,
                    effectiveWyrmProvider,
                    wyrmConfig,
                    wyrmOptions,
                    _gitService
                );

                _Wyverns[projectName] = wyvern;
                _wyvernProjectIds[projectName] = projectId ?? projectName;

                return wyvern;
            }
        }

        /// <summary>
        /// Gets the count of active wyverns for a specific project
        /// </summary>
        public int GetActiveWyvernCountForProject(string? projectId)
        {
            lock (_lock)
            {
                return _wyvernProjectIds.Count(kvp => kvp.Value == projectId);
            }
        }

        /// <summary>
        /// Checks if a new wyvern can be created for the specified project based on the parallel limit
        /// </summary>
        public bool CanCreateWyvernForProject(string? projectId)
        {
            var currentCount = GetActiveWyvernCountForProject(projectId);
            var maxAllowed = _projectConfigService.GetMaxParallelWyverns(projectId ?? string.Empty);
            return currentCount < maxAllowed;
        }

        /// <summary>
        /// Gets an existing Wyvern by project name (also matches sanitized folder name)
        /// </summary>
        public Wyvern? GetWyvern(string projectName)
        {
            lock (_lock)
            {
                return GetWyvernInternal(projectName);
            }
        }

        /// <summary>
        /// Internal lookup without locking (must be called within lock)
        /// </summary>
        private Wyvern? GetWyvernInternal(string projectName)
        {
            // First try direct lookup (case-insensitive due to StringComparer)
            if (_Wyverns.TryGetValue(projectName, out var wyvern))
                return wyvern;

            // Also try matching against sanitized version of stored names
            // This handles the case where folder name (sanitized) is used for lookup
            // but the wyvern was stored with its original name
            var sanitizedSearchName = SanitizeProjectName(projectName);
            return _Wyverns.FirstOrDefault(kvp =>
                SanitizeProjectName(kvp.Key).Equals(sanitizedSearchName, StringComparison.OrdinalIgnoreCase)).Value;
        }

        /// <summary>
        /// Sanitizes project name for comparison (matches ProjectService.SanitizeProjectName)
        /// </summary>
        private static string SanitizeProjectName(string projectName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", projectName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim().Replace(" ", "-").ToLowerInvariant();
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
                // Find the actual key (may be different casing or sanitized form)
                var actualKey = FindActualKey(projectName);
                if (actualKey == null)
                    return false;

                _wyvernProjectIds.Remove(actualKey);
                return _Wyverns.Remove(actualKey);
            }
        }

        /// <summary>
        /// Finds the actual key in the dictionary that matches the given project name
        /// </summary>
        private string? FindActualKey(string projectName)
        {
            // First try direct lookup
            if (_Wyverns.ContainsKey(projectName))
                return projectName;

            // Try to find key with matching sanitized name
            var sanitizedSearchName = SanitizeProjectName(projectName);
            return _Wyverns.Keys.FirstOrDefault(key =>
                SanitizeProjectName(key).Equals(sanitizedSearchName, StringComparison.OrdinalIgnoreCase));
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