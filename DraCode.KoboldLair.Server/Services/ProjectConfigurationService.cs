using System.Text.Json;
using DraCode.KoboldLair.Server.Models;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Service for loading and managing project configurations
    /// </summary>
    public class ProjectConfigurationService
    {
        private readonly ProjectConfigurations _configurations;
        private readonly ILogger<ProjectConfigurationService> _logger;
        private readonly string? _configPath;

        public ProjectConfigurationService(
            IConfiguration configuration,
            ILogger<ProjectConfigurationService> logger)
        {
            _logger = logger;
            _configPath = configuration["ProjectConfigurationsPath"];
            _configurations = LoadConfigurations(_configPath);
        }

        /// <summary>
        /// Gets the configuration for a specific project
        /// </summary>
        public ProjectConfig? GetProjectConfig(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                return null;
            }

            return _configurations.Projects
                .FirstOrDefault(p => p.ProjectId.Equals(projectId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets or creates a configuration for a specific project
        /// </summary>
        public ProjectConfig GetOrCreateProjectConfig(string projectId, string? projectName = null)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
            {
                config = new ProjectConfig
                {
                    ProjectId = projectId,
                    ProjectName = projectName,
                    MaxParallelKobolds = _configurations.DefaultMaxParallelKobolds
                };
                _configurations.Projects.Add(config);
                SaveConfigurations();
            }
            return config;
        }

        /// <summary>
        /// Updates configuration for a specific project
        /// </summary>
        public void UpdateProjectConfig(ProjectConfig config)
        {
            var existing = GetProjectConfig(config.ProjectId);
            if (existing != null)
            {
                _configurations.Projects.Remove(existing);
            }

            config.LastUpdated = DateTime.UtcNow;
            _configurations.Projects.Add(config);
            SaveConfigurations();
        }

        /// <summary>
        /// Gets the maximum parallel kobolds allowed for a specific project
        /// </summary>
        public int GetMaxParallelKobolds(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                return _configurations.DefaultMaxParallelKobolds;
            }

            var config = GetProjectConfig(projectId);
            return config?.MaxParallelKobolds ?? _configurations.DefaultMaxParallelKobolds;
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
            {
                return false; // Disabled by default
            }

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
        /// Sets provider configuration for a project
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
        /// Gets provider for a specific agent type in a project
        /// </summary>
        public string? GetProjectProvider(string projectId, string agentType)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
            {
                return null;
            }

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
        /// Gets model for a specific agent type in a project
        /// </summary>
        public string? GetProjectModel(string projectId, string agentType)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
            {
                return null;
            }

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
            return _configurations.Projects.AsReadOnly();
        }

        /// <summary>
        /// Gets the default configuration
        /// </summary>
        public int GetDefaultConfiguration()
        {
            return _configurations.DefaultMaxParallelKobolds;
        }

        /// <summary>
        /// Deletes a project configuration
        /// </summary>
        public bool DeleteProjectConfig(string projectId)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
            {
                return false;
            }

            _configurations.Projects.Remove(config);
            SaveConfigurations();
            return true;
        }

        /// <summary>
        /// Gets the default maximum parallel kobolds
        /// </summary>
        public int GetDefaultMaxParallelKobolds()
        {
            return _configurations.DefaultMaxParallelKobolds;
        }

        private ProjectConfigurations LoadConfigurations(string? configPath)
        {
            if (string.IsNullOrEmpty(configPath))
            {
                _logger.LogInformation("No ProjectConfigurationsPath specified, using default settings (MaxParallelKobolds=1)");
                return new ProjectConfigurations();
            }

            if (!File.Exists(configPath))
            {
                _logger.LogWarning("ProjectConfigurationsPath specified but file not found: {Path}. Using default settings.", configPath);
                return new ProjectConfigurations();
            }

            try
            {
                var jsonContent = File.ReadAllText(configPath);
                var configurations = JsonSerializer.Deserialize<ProjectConfigurations>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (configurations == null)
                {
                    _logger.LogWarning("Failed to deserialize project configurations from {Path}. Using default settings.", configPath);
                    return new ProjectConfigurations();
                }

                _logger.LogInformation("Loaded project configurations from {Path}. Default: {Default}, Projects: {Count}",
                    configPath, configurations.DefaultMaxParallelKobolds, configurations.Projects.Count);

                return configurations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading project configurations from {Path}. Using default settings.", configPath);
                return new ProjectConfigurations();
            }
        }

        private void SaveConfigurations()
        {
            if (string.IsNullOrEmpty(_configPath))
            {
                _logger.LogWarning("Cannot save project configurations: no path configured");
                return;
            }

            try
            {
                var jsonContent = JsonSerializer.Serialize(_configurations, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(_configPath, jsonContent);
                _logger.LogInformation("Saved project configurations to {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving project configurations to {Path}", _configPath);
            }
        }
    }
}
