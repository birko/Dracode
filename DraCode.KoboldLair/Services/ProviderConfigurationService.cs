using System.Text.Json;
using DraCode.Agent;
using DraCode.KoboldLair.Models.Configuration;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Service for managing provider configuration.
    ///
    /// Configuration sources (in order of precedence):
    /// 1. appsettings.json - Provider definitions and default provider
    /// 2. user-settings.json - Runtime agent-to-provider selections (persisted)
    /// 3. Project config - Per-project overrides (handled by ProjectConfigurationService)
    /// </summary>
    public class ProviderConfigurationService
    {
        private readonly ILogger<ProviderConfigurationService> _logger;
        private readonly KoboldLairConfiguration _config;
        private readonly string _userSettingsPath;
        private UserSettings _userSettings;
        private readonly object _lock = new();

        public ProviderConfigurationService(
            ILogger<ProviderConfigurationService> logger,
            IOptions<KoboldLairConfiguration> config,
            string userSettingsPath = "./user-settings.json")
        {
            _logger = logger;
            _config = config.Value;
            _userSettingsPath = userSettingsPath;
            _userSettings = new UserSettings();

            LogConfiguration();
            LoadUserSettings();
        }

        private void LogConfiguration()
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("Provider Configuration");
            _logger.LogInformation("========================================");
            _logger.LogInformation("Default Provider: {Default}", _config.DefaultProvider);
            _logger.LogInformation("Providers ({Count}):", _config.Providers.Count);
            foreach (var provider in _config.Providers)
            {
                var status = provider.IsEnabled ? "enabled" : "disabled";
                _logger.LogInformation("  - {Name}: {Status}, model={Model}",
                    provider.Name, status, provider.DefaultModel);
            }
            _logger.LogInformation("========================================");
        }

        /// <summary>
        /// Gets all available (enabled) providers
        /// </summary>
        public List<ProviderConfig> GetAvailableProviders()
        {
            lock (_lock)
            {
                return _config.Providers.Where(p => p.IsEnabled).ToList();
            }
        }

        /// <summary>
        /// Gets all providers regardless of enabled status
        /// </summary>
        public List<ProviderConfig> GetAllProviders()
        {
            lock (_lock)
            {
                return _config.Providers.ToList();
            }
        }

        /// <summary>
        /// Gets providers compatible with a specific agent type
        /// </summary>
        public List<ProviderConfig> GetProvidersForAgent(string agentType)
        {
            lock (_lock)
            {
                var type = agentType.ToLowerInvariant();
                return _config.Providers
                    .Where(p => p.IsEnabled &&
                           (p.CompatibleAgents.Contains(type) ||
                            p.CompatibleAgents.Contains("all")))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the configured provider name for an agent type
        /// </summary>
        public string GetProviderForAgent(string agentType)
        {
            lock (_lock)
            {
                var userProvider = agentType.ToLowerInvariant() switch
                {
                    "dragon" => _userSettings.DragonProvider,
                    "wyvern" => _userSettings.WyvernProvider,
                    "wyrm" => _userSettings.WyrmProvider,
                    "kobold" => _userSettings.KoboldProvider,
                    _ => null
                };

                return userProvider ?? _config.DefaultProvider;
            }
        }

        /// <summary>
        /// Gets the configured provider name for a specific Kobold agent type.
        /// Resolution precedence:
        /// 1. KoboldAgentTypeSettings[agentType] (if matching entry exists)
        /// 2. KoboldProvider (global Kobold fallback)
        /// 3. DefaultProvider (system default)
        /// </summary>
        public string GetProviderForKoboldAgentType(string agentType)
        {
            lock (_lock)
            {
                // Check for agent-type-specific setting
                var agentTypeSetting = _userSettings.KoboldAgentTypeSettings
                    .FirstOrDefault(s => s.AgentType.Equals(agentType, StringComparison.OrdinalIgnoreCase));

                if (agentTypeSetting?.Provider != null)
                {
                    return agentTypeSetting.Provider;
                }

                // Fall back to global Kobold provider
                return _userSettings.KoboldProvider ?? _config.DefaultProvider;
            }
        }

        /// <summary>
        /// Gets the provider configuration and agent options for a specific Kobold agent type.
        /// Resolution precedence:
        /// 1. KoboldAgentTypeSettings[agentType] (if matching entry exists)
        /// 2. KoboldProvider/KoboldModel (global Kobold fallback)
        /// 3. DefaultProvider (system default)
        /// </summary>
        public (string provider, Dictionary<string, string> config, AgentOptions options) GetProviderSettingsForKoboldAgentType(
            string agentType,
            string? workingDirectory = null)
        {
            lock (_lock)
            {
                var providerName = GetProviderForKoboldAgentType(agentType);
                var providerConfig = _config.Providers.FirstOrDefault(p => p.Name == providerName);

                if (providerConfig == null)
                {
                    throw new InvalidOperationException($"Provider '{providerName}' not found for Kobold agent type '{agentType}'");
                }

                // Build configuration dictionary
                var config = new Dictionary<string, string>(providerConfig.Configuration);

                // Get model override - check agent-type-specific setting first, then global
                var agentTypeSetting = _userSettings.KoboldAgentTypeSettings
                    .FirstOrDefault(s => s.AgentType.Equals(agentType, StringComparison.OrdinalIgnoreCase));

                var modelOverride = agentTypeSetting?.Model ?? _userSettings.KoboldModel;

                config["model"] = modelOverride ?? providerConfig.DefaultModel;

                // Add API key from environment if needed
                if (providerConfig.RequiresApiKey)
                {
                    var apiKeyEnvVar = GetApiKeyEnvironmentVariable(providerConfig.Type);
                    var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        config["apiKey"] = apiKey;
                    }
                    else if (!config.ContainsKey("apiKey") || string.IsNullOrWhiteSpace(config["apiKey"]))
                    {
                        _logger.LogWarning("Provider {Provider} requires API key but none found in {EnvVar}",
                            providerName, apiKeyEnvVar);
                    }
                }

                var options = new AgentOptions
                {
                    WorkingDirectory = workingDirectory ?? "./workspace",
                    Verbose = false
                };

                return (providerConfig.Type, config, options);
            }
        }

        /// <summary>
        /// Sets the provider for a specific Kobold agent type (persisted to user-settings.json)
        /// </summary>
        public void SetProviderForKoboldAgentType(string agentType, string? provider, string? model = null)
        {
            lock (_lock)
            {
                // Validate provider if specified
                if (provider != null)
                {
                    var providerConfig = _config.Providers.FirstOrDefault(p => p.Name == provider);
                    if (providerConfig == null)
                    {
                        throw new ArgumentException($"Provider '{provider}' not found");
                    }

                    if (!providerConfig.IsEnabled)
                    {
                        throw new ArgumentException($"Provider '{provider}' is not enabled");
                    }
                }

                // Find existing entry or create new one
                var existingEntry = _userSettings.KoboldAgentTypeSettings
                    .FirstOrDefault(s => s.AgentType.Equals(agentType, StringComparison.OrdinalIgnoreCase));

                if (provider == null && model == null)
                {
                    // Remove the entry if both are null
                    if (existingEntry != null)
                    {
                        _userSettings.KoboldAgentTypeSettings.Remove(existingEntry);
                        _logger.LogInformation("Removed Kobold agent type settings for {AgentType}", agentType);
                    }
                }
                else if (existingEntry != null)
                {
                    // Update existing entry
                    existingEntry.Provider = provider;
                    existingEntry.Model = model;
                    _logger.LogInformation("Updated Kobold agent type {AgentType} to provider={Provider}, model={Model}",
                        agentType, provider ?? "(default)", model ?? "(default)");
                }
                else
                {
                    // Add new entry
                    _userSettings.KoboldAgentTypeSettings.Add(new KoboldAgentTypeProviderSettings
                    {
                        AgentType = agentType.ToLowerInvariant(),
                        Provider = provider,
                        Model = model
                    });
                    _logger.LogInformation("Set Kobold agent type {AgentType} to provider={Provider}, model={Model}",
                        agentType, provider ?? "(default)", model ?? "(default)");
                }

                SaveUserSettings();
            }
        }

        /// <summary>
        /// Gets the provider configuration and agent options for a specific agent type
        /// </summary>
        public (string provider, Dictionary<string, string> config, AgentOptions options) GetProviderSettingsForAgent(
            string agentType,
            string? workingDirectory = null)
        {
            lock (_lock)
            {
                var providerName = GetProviderForAgent(agentType);
                var providerConfig = _config.Providers.FirstOrDefault(p => p.Name == providerName);

                if (providerConfig == null)
                {
                    throw new InvalidOperationException($"Provider '{providerName}' not found for agent type '{agentType}'");
                }

                // Build configuration dictionary
                var config = new Dictionary<string, string>(providerConfig.Configuration);

                // Get model override from user settings
                var modelOverride = agentType.ToLowerInvariant() switch
                {
                    "dragon" => _userSettings.DragonModel,
                    "wyvern" => _userSettings.WyvernModel,
                    "wyrm" => _userSettings.WyrmModel,
                    "kobold" => _userSettings.KoboldModel,
                    _ => null
                };

                config["model"] = modelOverride ?? providerConfig.DefaultModel;

                // Add API key from environment if needed
                if (providerConfig.RequiresApiKey)
                {
                    var apiKeyEnvVar = GetApiKeyEnvironmentVariable(providerConfig.Type);
                    var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        config["apiKey"] = apiKey;
                    }
                    else if (!config.ContainsKey("apiKey") || string.IsNullOrWhiteSpace(config["apiKey"]))
                    {
                        _logger.LogWarning("Provider {Provider} requires API key but none found in {EnvVar}",
                            providerName, apiKeyEnvVar);
                    }
                }

                var options = new AgentOptions
                {
                    WorkingDirectory = workingDirectory ?? "./workspace",
                    Verbose = false
                };

                return (providerConfig.Type, config, options);
            }
        }

        /// <summary>
        /// Updates the provider selection for an agent type (persisted to user-settings.json)
        /// </summary>
        public void SetProviderForAgent(string agentType, string providerName, string? modelOverride = null)
        {
            lock (_lock)
            {
                // Validate provider exists and is enabled
                var provider = _config.Providers.FirstOrDefault(p => p.Name == providerName);
                if (provider == null)
                {
                    throw new ArgumentException($"Provider '{providerName}' not found");
                }

                if (!provider.IsEnabled)
                {
                    throw new ArgumentException($"Provider '{providerName}' is not enabled");
                }

                var type = agentType.ToLowerInvariant();
                if (!provider.CompatibleAgents.Contains(type) && !provider.CompatibleAgents.Contains("all"))
                {
                    throw new ArgumentException($"Provider '{providerName}' is not compatible with '{agentType}'");
                }

                // Update user settings
                switch (type)
                {
                    case "dragon":
                        _userSettings.DragonProvider = providerName;
                        _userSettings.DragonModel = modelOverride;
                        break;
                    case "wyvern":
                        _userSettings.WyvernProvider = providerName;
                        _userSettings.WyvernModel = modelOverride;
                        break;
                    case "wyrm":
                        _userSettings.WyrmProvider = providerName;
                        _userSettings.WyrmModel = modelOverride;
                        break;
                    case "kobold":
                        _userSettings.KoboldProvider = providerName;
                        _userSettings.KoboldModel = modelOverride;
                        break;
                    default:
                        throw new ArgumentException($"Unknown agent type '{agentType}'");
                }

                SaveUserSettings();
                _logger.LogInformation("Set {AgentType} provider to {Provider}", agentType, providerName);
            }
        }

        /// <summary>
        /// Validates that a provider is properly configured
        /// </summary>
        public (bool isValid, string message) ValidateProvider(string providerName)
        {
            lock (_lock)
            {
                var provider = _config.Providers.FirstOrDefault(p => p.Name == providerName);
                if (provider == null)
                {
                    return (false, $"Provider '{providerName}' not found");
                }

                if (!provider.IsEnabled)
                {
                    return (false, $"Provider '{providerName}' is disabled");
                }

                if (provider.RequiresApiKey)
                {
                    var apiKeyEnvVar = GetApiKeyEnvironmentVariable(provider.Type);
                    var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar);
                    var configHasKey = provider.Configuration.ContainsKey("apiKey") &&
                                      !string.IsNullOrWhiteSpace(provider.Configuration["apiKey"]);

                    if (string.IsNullOrWhiteSpace(apiKey) && !configHasKey)
                    {
                        return (false, $"API key required. Set environment variable: {apiKeyEnvVar}");
                    }
                }

                return (true, "Provider is configured correctly");
            }
        }

        /// <summary>
        /// Gets the default provider name
        /// </summary>
        public string GetDefaultProvider() => _config.DefaultProvider;

        /// <summary>
        /// Gets the default agent limits
        /// </summary>
        public AgentLimits GetDefaultLimits() => _config.Limits;

        /// <summary>
        /// Gets current user settings (for API responses)
        /// </summary>
        public UserSettings GetUserSettings()
        {
            lock (_lock)
            {
                return new UserSettings
                {
                    DragonProvider = _userSettings.DragonProvider,
                    WyrmProvider = _userSettings.WyrmProvider,
                    WyvernProvider = _userSettings.WyvernProvider,
                    KoboldProvider = _userSettings.KoboldProvider,
                    DragonModel = _userSettings.DragonModel,
                    WyrmModel = _userSettings.WyrmModel,
                    WyvernModel = _userSettings.WyvernModel,
                    KoboldModel = _userSettings.KoboldModel,
                    KoboldAgentTypeSettings = _userSettings.KoboldAgentTypeSettings
                        .Select(s => new KoboldAgentTypeProviderSettings
                        {
                            AgentType = s.AgentType,
                            Provider = s.Provider,
                            Model = s.Model
                        })
                        .ToList()
                };
            }
        }

        private void LoadUserSettings()
        {
            try
            {
                if (File.Exists(_userSettingsPath))
                {
                    var json = File.ReadAllTextAsync(_userSettingsPath).GetAwaiter().GetResult();
                    var settings = JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (settings != null)
                    {
                        _userSettings = settings;
                        _logger.LogInformation("Loaded user settings from {Path}", _userSettingsPath);
                        LogUserSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load user settings, using defaults");
            }
        }

        private void LogUserSettings()
        {
            _logger.LogInformation("User agent assignments:");
            _logger.LogInformation("  Dragon: {Provider}", _userSettings.DragonProvider ?? "(default)");
            _logger.LogInformation("  Wyrm: {Provider}", _userSettings.WyrmProvider ?? "(default)");
            _logger.LogInformation("  Wyvern: {Provider}", _userSettings.WyvernProvider ?? "(default)");
            _logger.LogInformation("  Kobold: {Provider}", _userSettings.KoboldProvider ?? "(default)");

            if (_userSettings.KoboldAgentTypeSettings.Count > 0)
            {
                _logger.LogInformation("  Kobold agent type overrides ({Count}):", _userSettings.KoboldAgentTypeSettings.Count);
                foreach (var setting in _userSettings.KoboldAgentTypeSettings)
                {
                    _logger.LogInformation("    - {AgentType}: provider={Provider}, model={Model}",
                        setting.AgentType,
                        setting.Provider ?? "(default)",
                        setting.Model ?? "(default)");
                }
            }
        }

        private void SaveUserSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_userSettings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllTextAsync(_userSettingsPath, json).GetAwaiter().GetResult();
                _logger.LogInformation("Saved user settings to {Path}", _userSettingsPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save user settings");
            }
        }

        private static string GetApiKeyEnvironmentVariable(string providerType)
        {
            return providerType.ToLowerInvariant() switch
            {
                "openai" => "OPENAI_API_KEY",
                "claude" => "ANTHROPIC_API_KEY",
                "gemini" => "GOOGLE_API_KEY",
                "azureopenai" => "AZURE_OPENAI_API_KEY",
                "githubcopilot" => "GITHUB_COPILOT_TOKEN",
                "llamacpp" => "LLAMACPP_API_KEY",
                "vllm" => "VLLM_API_KEY",
                "zai" => "ZAI_API_KEY",
                _ => $"{providerType.ToUpperInvariant()}_API_KEY"
            };
        }
    }
}
