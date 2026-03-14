using System.Text.Json;
using System.Threading.Channels;
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
        /// <summary>
        /// Normalizes a path to its canonical form with correct capitalization.
        /// On Windows, this returns the actual path with proper case as stored on the filesystem.
        /// On case-sensitive filesystems, returns the path as-is.
        /// </summary>
        private static string GetCanonicalPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var fullPath = Path.GetFullPath(path);

            // On Windows, get the actual case from the filesystem
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // Get the directory info which provides the actual casing
                    if (Directory.Exists(fullPath))
                    {
                        var dirInfo = new DirectoryInfo(fullPath);
                        return dirInfo.FullName;
                    }
                    else if (File.Exists(fullPath))
                    {
                        var fileInfo = new FileInfo(fullPath);
                        return fileInfo.FullName;
                    }
                }
                catch
                {
                    // If we can't get the actual case, return the normalized full path
                }
            }

            return fullPath;
        }
        private readonly ProviderConfigurationService _providerConfig;
        private readonly ILogger<ProjectConfigurationService> _logger;
        private readonly string _configPath;
        private ProjectConfigurations _configurations;
        private readonly object _lock = new();

        // Debounced save support
        private readonly Channel<bool> _saveChannel;
        private readonly Task _saveTask;
        private readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(2);

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ProjectConfigurationService(
            ProviderConfigurationService providerConfig,
            ILogger<ProjectConfigurationService> logger,
            string configPath = "./project-configs.json")
        {
            _providerConfig = providerConfig;
            _logger = logger;
            _configPath = configPath;
            _configurations = LoadConfigurations();

            // Initialize debounced save channel
            _saveChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite
            });
            _saveTask = ProcessSaveQueueAsync();

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
                    .FirstOrDefault(p => p.Project.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase));
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
                        Project = new ProjectIdentity
                        {
                            Id = projectId,
                            Name = projectName
                        },
                        Agents = new AgentsConfig
                        {
                            Wyrm = new AgentConfig { MaxParallel = limits.MaxParallelWyrms },
                            Wyvern = new AgentConfig { MaxParallel = limits.MaxParallelWyverns },
                            Drake = new AgentConfig { MaxParallel = limits.MaxParallelDrakes },
                            KoboldPlanner = new AgentConfig { MaxParallel = 1 },
                            Kobold = new AgentConfig { MaxParallel = limits.MaxParallelKobolds }
                        },
                        Security = new SecurityConfig(),
                        Metadata = new MetadataConfig
                        {
                            CreatedAt = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow
                        }
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
                    .FirstOrDefault(p => p.Project.Id.Equals(config.Project.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _configurations.Projects.Remove(existing);
                }

                config.Metadata.LastUpdated = DateTime.UtcNow;
                _configurations.Projects.Add(config);
                SaveConfigurations();
            }
        }

        /// <summary>
        /// Gets the maximum parallel agents for a specific type in a project
        /// </summary>
        public int GetMaxParallel(string projectId, string agentType)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
            {
                var limits = _providerConfig.GetDefaultLimits();
                return agentType.ToLowerInvariant() switch
                {
                    "wyrm" => limits.MaxParallelWyrms,
                    "wyvern" => limits.MaxParallelWyverns,
                    "drake" => limits.MaxParallelDrakes,
                    "kobold-planner" or "koboldplanner" or "planner" => 1,
                    "kobold" => limits.MaxParallelKobolds,
                    _ => 1
                };
            }

            return config.GetAgentConfig(agentType).MaxParallel;
        }

        /// <summary>
        /// Gets the maximum parallel kobolds for a project (project override or default)
        /// </summary>
        public int GetMaxParallelKobolds(string projectId) => GetMaxParallel(projectId, "kobold");

        /// <summary>
        /// Gets the maximum parallel drakes for a project (project override or default)
        /// </summary>
        public int GetMaxParallelDrakes(string projectId) => GetMaxParallel(projectId, "drake");

        /// <summary>
        /// Gets the maximum parallel wyrms for a project (project override or default)
        /// </summary>
        public int GetMaxParallelWyrms(string projectId) => GetMaxParallel(projectId, "wyrm");

        /// <summary>
        /// Gets the maximum parallel wyverns for a project (project override or default)
        /// </summary>
        public int GetMaxParallelWyverns(string projectId) => GetMaxParallel(projectId, "wyvern");

        /// <summary>
        /// Gets the maximum parallel kobold planners for a project
        /// </summary>
        public int GetMaxParallelKoboldPlanners(string projectId) => GetMaxParallel(projectId, "kobold-planner");

        /// <summary>
        /// Sets the maximum parallel kobolds for a specific project
        /// </summary>
        public void SetMaxParallelKobolds(string projectId, int maxParallel)
        {
            SetAgentLimit(projectId, "kobold", maxParallel);
        }

        /// <summary>
        /// Sets the maximum parallel limit for a specific agent type in a project
        /// </summary>
        public void SetAgentLimit(string projectId, string agentType, int maxParallel)
        {
            if (maxParallel < 1)
                throw new ArgumentException("Max parallel must be at least 1", nameof(maxParallel));

            var config = GetOrCreateProjectConfig(projectId);
            var agentConfig = config.GetAgentConfig(agentType);
            agentConfig.MaxParallel = maxParallel;

            config.Metadata.LastUpdated = DateTime.UtcNow;
            SaveConfigurations();
        }

        /// <summary>
        /// Sets the timeout for a specific agent type in a project
        /// </summary>
        public void SetAgentTimeout(string projectId, string agentType, int timeoutSeconds)
        {
            if (timeoutSeconds < 0)
                throw new ArgumentException("Timeout must be non-negative", nameof(timeoutSeconds));

            var config = GetOrCreateProjectConfig(projectId);
            var agentConfig = config.GetAgentConfig(agentType);
            agentConfig.Timeout = timeoutSeconds;

            config.Metadata.LastUpdated = DateTime.UtcNow;
            SaveConfigurations();
        }

        /// <summary>
        /// Gets the timeout for a specific agent type in a project
        /// </summary>
        public int GetAgentTimeout(string projectId, string agentType)
        {
            var config = GetProjectConfig(projectId);
            return config?.GetAgentConfig(agentType).Timeout ?? 0;
        }

        /// <summary>
        /// Sets whether an agent is enabled for a project
        /// </summary>
        public void SetAgentEnabled(string projectId, string agentType, bool enabled)
        {
            var config = GetOrCreateProjectConfig(projectId);
            var agentConfig = config.GetAgentConfig(agentType);
            agentConfig.Enabled = enabled;

            config.Metadata.LastUpdated = DateTime.UtcNow;
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

            try
            {
                return config.GetAgentConfig(agentType).Enabled;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Sets provider override for a project
        /// </summary>
        public void SetProjectProvider(string projectId, string agentType, string? provider, string? model = null)
        {
            var config = GetOrCreateProjectConfig(projectId);
            var agentConfig = config.GetAgentConfig(agentType);
            agentConfig.Provider = provider;
            agentConfig.Model = model;

            config.Metadata.LastUpdated = DateTime.UtcNow;
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

            try
            {
                return config.GetAgentConfig(agentType).Provider;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets model override for a specific agent type in a project
        /// </summary>
        public string? GetProjectModel(string projectId, string agentType)
        {
            var config = GetProjectConfig(projectId);
            if (config == null)
                return null;

            try
            {
                return config.GetAgentConfig(agentType).Model;
            }
            catch (ArgumentException)
            {
                return null;
            }
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
                    .FirstOrDefault(p => p.Project.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase));
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
                var json = File.ReadAllTextAsync(_configPath).GetAwaiter().GetResult();
                var configs = JsonSerializer.Deserialize<ProjectConfigurations>(json, ReadOptions);

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

        /// <summary>
        /// Queues a debounced save of the current configurations.
        /// Writes are coalesced - multiple rapid calls result in a single write after the debounce interval.
        /// </summary>
        private void SaveConfigurations()
        {
            _saveChannel.Writer.TryWrite(true);
        }

        /// <summary>
        /// Processes the save queue, debouncing rapid writes
        /// </summary>
        private async Task ProcessSaveQueueAsync()
        {
            try
            {
                while (await _saveChannel.Reader.WaitToReadAsync())
                {
                    // Drain any pending requests
                    while (_saveChannel.Reader.TryRead(out _)) { }

                    // Wait for debounce interval
                    await Task.Delay(_debounceInterval);

                    // Drain again in case more came in during the delay
                    while (_saveChannel.Reader.TryRead(out _)) { }

                    // Perform the actual save
                    await SaveConfigurationsInternalAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Channel was closed, exit gracefully
            }
        }

        /// <summary>
        /// Saves configurations to disk immediately (async)
        /// </summary>
        private async Task SaveConfigurationsInternalAsync()
        {
            try
            {
                string json;
                lock (_lock)
                {
                    json = JsonSerializer.Serialize(_configurations, WriteOptions);
                }
                await File.WriteAllTextAsync(_configPath, json);
                _logger.LogDebug("Saved project configurations to {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving project configurations to {Path}", _configPath);
            }
        }

        /// <summary>
        /// Forces an immediate save of the current configurations (async, bypasses debounce)
        /// </summary>
        public async Task SaveConfigurationsAsync()
        {
            await SaveConfigurationsInternalAsync();
        }
    }
}
