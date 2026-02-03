using System.Text.Json;
using DraCode.KoboldLair.Models.Configuration;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Service for managing per-project configurations.
    /// Default limits come from ProviderConfigurationService (appsettings.json).
    /// Per-project overrides are stored in project-configs.json.
    /// </summary>
    public class ProjectConfigurationService
    {
        private readonly ProviderConfigurationService _providerConfig;
        private readonly ILogger<ProjectConfigurationService> _logger;
        private readonly string _configPath;
        private ProjectConfigurations _configurations;
        private readonly object _lock = new();

        public ProjectConfigurationService(
            ProviderConfigurationService providerConfig,
            ILogger<ProjectConfigurationService> logger,
            string configPath = "./project-configs.json")
        {
            _providerConfig = providerConfig;
            _logger = logger;
            _configPath = configPath;
            _configurations = LoadConfigurations();

            var limits = _providerConfig.GetDefaultLimits();
            _logger.LogInformation(
                "Agent limits - Kobolds: {Kobolds}, Drakes: {Drakes}, Wyrms: {Wyrms}, Wyverns: {Wyverns}",
                limits.MaxParallelKobolds,
                limits.MaxParallelDrakes,
                limits.MaxParallelWyrms,
                limits.MaxParallelWyverns);
        }

        /// <summary>
        /// Gets the configuration for a specific project
        /// </summary>
        public ProjectConfig? GetProjectConfig(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return null;

            lock (_lock)
            {
                return _configurations.Projects
                    .FirstOrDefault(p => p.ProjectId.Equals(projectId, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Gets or creates a configuration for a specific project
        /// </summary>
        public ProjectConfig GetOrCreateProjectConfig(string projectId, string? projectName = null)
        {
            lock (_lock)
            {
                var config = GetProjectConfig(projectId);
                if (config == null)
                {
                    var limits = _providerConfig.GetDefaultLimits();
                    config = new ProjectConfig
                    {
                        ProjectId = projectId,
                        ProjectName = projectName,
                        MaxParallelKobolds = limits.MaxParallelKobolds,
                        MaxParallelDrakes = limits.MaxParallelDrakes,
                        MaxParallelWyrms = limits.MaxParallelWyrms,
                        MaxParallelWyverns = limits.MaxParallelWyverns
                    };
                    _configurations.Projects.Add(config);
                    SaveConfigurations();
                }
                return config;
            }
        }

        /// <summary>
        /// Updates configuration for a specific project
        /// </summary>
        public void UpdateProjectConfig(ProjectConfig config)
        {
            lock (_lock)
            {
                var existing = _configurations.Projects
                    .FirstOrDefault(p => p.ProjectId.Equals(config.ProjectId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _configurations.Projects.Remove(existing);
                }

                config.LastUpdated = DateTime.UtcNow;
                _configurations.Projects.Add(config);
                SaveConfigurations();
            }
        }

        /// <summary>
        /// Gets the maximum parallel kobolds for a project (project override or default)
        /// </summary>
        public int GetMaxParallelKobolds(string projectId)
        {
            var config = GetProjectConfig(projectId);
            return config?.MaxParallelKobolds ?? _providerConfig.GetDefaultLimits().MaxParallelKobolds;
        }

        /// <summary>
        /// Gets the maximum parallel drakes for a project (project override or default)
        /// </summary>
        public int GetMaxParallelDrakes(string projectId)
        {
            var config = GetProjectConfig(projectId);
            return config?.MaxParallelDrakes ?? _providerConfig.GetDefaultLimits().MaxParallelDrakes;
        }

        /// <summary>
        /// Gets the maximum parallel wyrms for a project (project override or default)
        /// </summary>
        public int GetMaxParallelWyrms(string projectId)
        {
            var config = GetProjectConfig(projectId);
            return config?.MaxParallelWyrms ?? _providerConfig.GetDefaultLimits().MaxParallelWyrms;
        }

        /// <summary>
        /// Gets the maximum parallel wyverns for a project (project override or default)
        /// </summary>
        public int GetMaxParallelWyverns(string projectId)
        {
            var config = GetProjectConfig(projectId);
            return config?.MaxParallelWyverns ?? _providerConfig.GetDefaultLimits().MaxParallelWyverns;
        }

        /// <summary>
        /// Sets the maximum parallel kobolds for a specific project
        /// </summary>
        public void SetMaxParallelKobolds(string projectId, int maxParallel)
        {
            var config = GetOrCreateProjectConfig(projectId);
            config.MaxParallelKobolds = maxParallel;
            config.LastUpdated = DateTime.UtcNow;
            SaveConfigurations();
        }

        /// <summary>
        /// Sets the maximum parallel limit for a specific agent type in a project
        /// </summary>
        public void SetAgentLimit(string projectId, string agentType, int maxParallel)
        {
            if (maxParallel < 1)
                throw new ArgumentException("Max parallel must be at least 1", nameof(maxParallel));

            var config = GetOrCreateProjectConfig(projectId);

            switch (agentType.ToLowerInvariant())
            {
                case "wyrm":
                    config.MaxParallelWyrms = maxParallel;
                    break;
                case "wyvern":
                    config.MaxParallelWyverns = maxParallel;
                    break;
                case "drake":
                    config.MaxParallelDrakes = maxParallel;
                    break;
                case "kobold":
                    config.MaxParallelKobolds = maxParallel;
                    break;
                default:
                    throw new ArgumentException($"Unknown agent type: {agentType}");
            }

            config.LastUpdated = DateTime.UtcNow;
            SaveConfigurations();
        }

        /// <summary>
        /// Sets whether an agent is enabled for a project
        /// </summary>
        public void SetAgentEnabled(string projectId, string agentType, bool enabled)
        {
            var config = GetOrCreateProjectConfig(projectId);

            switch (agentType.ToLowerInvariant())
            {
                case "wyrm":
                    config.WyrmEnabled = enabled;
                    break;
                case "wyvern":
                    config.WyvernEnabled = enabled;
                    break;
                case "drake":
                    config.DrakeEnabled = enabled;
                    break;
                case "kobold":
                    config.KoboldEnabled = enabled;
                    break;
                default:
                    throw new ArgumentException($"Unknown agent type: {agentType}");
            }

            config.LastUpdated = DateTime.UtcNow;
            SaveConfigurations();
        }

        /// <summary>
        /// Checks if an agent is enabled for a project
        /// </summary>
        public bool IsAgentEnabled(string projectId, string agentType)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
                return false;

            return agentType.ToLowerInvariant() switch
            {
                "wyrm" => config.WyrmEnabled,
                "wyvern" => config.WyvernEnabled,
                "drake" => config.DrakeEnabled,
                "kobold" => config.KoboldEnabled,
                _ => false
            };
        }

        /// <summary>
        /// Sets provider override for a project
        /// </summary>
        public void SetProjectProvider(string projectId, string agentType, string? provider, string? model = null)
        {
            var config = GetOrCreateProjectConfig(projectId);

            switch (agentType.ToLowerInvariant())
            {
                case "wyrm":
                    config.WyrmProvider = provider;
                    config.WyrmModel = model;
                    break;
                case "wyvern":
                    config.WyvernProvider = provider;
                    config.WyvernModel = model;
                    break;
                case "drake":
                    config.DrakeProvider = provider;
                    config.DrakeModel = model;
                    break;
                case "kobold":
                    config.KoboldProvider = provider;
                    config.KoboldModel = model;
                    break;
                default:
                    throw new ArgumentException($"Unknown agent type: {agentType}");
            }

            config.LastUpdated = DateTime.UtcNow;
            SaveConfigurations();
        }

        /// <summary>
        /// Gets provider override for a specific agent type in a project
        /// </summary>
        public string? GetProjectProvider(string projectId, string agentType)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
                return null;

            return agentType.ToLowerInvariant() switch
            {
                "wyvern" => config.WyvernProvider,
                "wyrm" => config.WyrmProvider,
                "drake" => config.DrakeProvider,
                "kobold" => config.KoboldProvider,
                _ => null
            };
        }

        /// <summary>
        /// Gets model override for a specific agent type in a project
        /// </summary>
        public string? GetProjectModel(string projectId, string agentType)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
                return null;

            return agentType.ToLowerInvariant() switch
            {
                "wyvern" => config.WyvernModel,
                "wyrm" => config.WyrmModel,
                "drake" => config.DrakeModel,
                "kobold" => config.KoboldModel,
                _ => null
            };
        }

        /// <summary>
        /// Gets all project configurations
        /// </summary>
        public IReadOnlyList<ProjectConfig> GetAllProjectConfigs()
        {
            lock (_lock)
            {
                return _configurations.Projects.AsReadOnly();
            }
        }

        /// <summary>
        /// Deletes a project configuration
        /// </summary>
        public bool DeleteProjectConfig(string projectId)
        {
            lock (_lock)
            {
                var config = _configurations.Projects
                    .FirstOrDefault(p => p.ProjectId.Equals(projectId, StringComparison.OrdinalIgnoreCase));
                if (config == null)
                    return false;

                _configurations.Projects.Remove(config);
                SaveConfigurations();
                return true;
            }
        }

        /// <summary>
        /// Gets the default maximum parallel kobolds
        /// </summary>
        public int GetDefaultMaxParallelKobolds() => _providerConfig.GetDefaultLimits().MaxParallelKobolds;

        /// <summary>
        /// Gets the default maximum parallel drakes
        /// </summary>
        public int GetDefaultMaxParallelDrakes() => _providerConfig.GetDefaultLimits().MaxParallelDrakes;

        /// <summary>
        /// Gets the default maximum parallel wyrms
        /// </summary>
        public int GetDefaultMaxParallelWyrms() => _providerConfig.GetDefaultLimits().MaxParallelWyrms;

        /// <summary>
        /// Gets the default maximum parallel wyverns
        /// </summary>
        public int GetDefaultMaxParallelWyverns() => _providerConfig.GetDefaultLimits().MaxParallelWyverns;

        private ProjectConfigurations LoadConfigurations()
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation("No project configurations file found at {Path}, starting fresh", _configPath);
                return new ProjectConfigurations();
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                var configs = JsonSerializer.Deserialize<ProjectConfigurations>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (configs == null)
                {
                    _logger.LogWarning("Failed to parse project configurations, starting fresh");
                    return new ProjectConfigurations();
                }

                _logger.LogInformation("Loaded {Count} project configurations from {Path}",
                    configs.Projects.Count, _configPath);
                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project configurations from {Path}", _configPath);
                return new ProjectConfigurations();
            }
        }

        private void SaveConfigurations()
        {
            try
            {
                var json = JsonSerializer.Serialize(_configurations, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(_configPath, json);
                _logger.LogDebug("Saved project configurations to {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving project configurations to {Path}", _configPath);
            }
        }
    }
}
