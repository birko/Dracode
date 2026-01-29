using System.Text.Json;
using DraCode.Agent;
using DraCode.KoboldLair.Server.Models;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Service for managing provider configuration and providing provider settings
    /// </summary>
    public class ProviderConfigurationService
    {
        private readonly ILogger<ProviderConfigurationService> _logger;
        private readonly string _configPath;
        private KoboldLairProviderConfiguration _configuration;
        private readonly object _lock = new();

        public ProviderConfigurationService(
            ILogger<ProviderConfigurationService> logger,
            IOptions<KoboldLairProviderConfiguration> defaultConfig,
            string configPath = "./provider-config.json")
        {
            _logger = logger;
            _configPath = configPath;
            _configuration = defaultConfig.Value;

            // Log loaded providers from appsettings
            _logger.LogInformation("========================================");
            _logger.LogInformation("Provider Configuration Loaded");
            _logger.LogInformation("========================================");
            _logger.LogInformation("Total Providers: {Count}", _configuration.Providers.Count);
            foreach (var provider in _configuration.Providers)
            {
                _logger.LogInformation("  - {Name} ({DisplayName}): Enabled={IsEnabled}, Model={Model}, Agents={Agents}",
                    provider.Name,
                    provider.DisplayName,
                    provider.IsEnabled,
                    provider.DefaultModel,
                    string.Join(", ", provider.CompatibleAgents));
            }
            _logger.LogInformation("Agent Assignments:");
            _logger.LogInformation("  - Dragon: {Provider}", _configuration.AgentProviders.DragonProvider);
            _logger.LogInformation("  - Wyvern: {Provider}", _configuration.AgentProviders.WyvernProvider);
            _logger.LogInformation("  - Kobold: {Provider}", _configuration.AgentProviders.KoboldProvider);
            _logger.LogInformation("========================================");

            // Try to load saved configuration, otherwise use defaults
            LoadConfiguration();
        }

        /// <summary>
        /// Gets the current provider configuration
        /// </summary>
        public KoboldLairProviderConfiguration GetConfiguration()
        {
            lock (_lock)
            {
                return _configuration;
            }
        }

        /// <summary>
        /// Gets all available providers
        /// </summary>
        public List<ProviderConfig> GetAvailableProviders()
        {
            lock (_lock)
            {
                return _configuration.Providers.Where(p => p.IsEnabled).ToList();
            }
        }

        /// <summary>
        /// Gets providers compatible with a specific agent type
        /// </summary>
        public List<ProviderConfig> GetProvidersForAgent(string agentType)
        {
            lock (_lock)
            {
                return _configuration.Providers
                    .Where(p => p.IsEnabled &&
                           (p.CompatibleAgents.Contains(agentType.ToLowerInvariant()) ||
                            p.CompatibleAgents.Contains("all")))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the configured provider for a specific agent type
        /// </summary>
        public string GetProviderForAgent(string agentType)
        {
            lock (_lock)
            {
                return agentType.ToLowerInvariant() switch
                {
                    "dragon" => _configuration.AgentProviders.DragonProvider,
                    "wyvern" => _configuration.AgentProviders.WyvernProvider,
                    "wyrm" => _configuration.AgentProviders.WyrmProvider,
                    "kobold" => _configuration.AgentProviders.KoboldProvider,
                    _ => _configuration.AgentProviders.KoboldProvider
                };
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
                var providerConfig = _configuration.Providers.FirstOrDefault(p => p.Name == providerName);

                if (providerConfig == null)
                {
                    throw new InvalidOperationException($"Provider '{providerName}' not found for agent type '{agentType}'");
                }

                // Build configuration dictionary
                var config = new Dictionary<string, string>(providerConfig.Configuration);

                // Add model override if specified
                var modelOverride = agentType.ToLowerInvariant() switch
                {
                    "dragon" => _configuration.AgentProviders.DragonModel,
                    "wyvern" => _configuration.AgentProviders.WyvernModel,
                    "wyrm" => _configuration.AgentProviders.WyrmModel,
                    "kobold" => _configuration.AgentProviders.KoboldModel,
                    _ => null
                };

                config["model"] = modelOverride ?? providerConfig.DefaultModel;

                // Add API key from environment if needed (overrides config file)
                if (providerConfig.RequiresApiKey)
                {
                    var apiKeyEnvVar = GetApiKeyEnvironmentVariable(providerConfig.Type);
                    var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        config["apiKey"] = apiKey;
                        _logger.LogDebug("Using API key from environment variable {EnvVar} for provider {Provider}",
                            apiKeyEnvVar, providerName);
                    }
                    else if (!config.ContainsKey("apiKey") || string.IsNullOrWhiteSpace(config["apiKey"]))
                    {
                        _logger.LogWarning("Provider {Provider} requires API key but none found in environment variable {EnvVar} or configuration",
                            providerName, apiKeyEnvVar);
                    }
                }

                // Create agent options
                var options = new AgentOptions
                {
                    WorkingDirectory = workingDirectory ?? "./workspace",
                    Verbose = false
                };

                return (providerConfig.Type, config, options);
            }
        }

        /// <summary>
        /// Updates the provider for a specific agent type
        /// </summary>
        public void SetProviderForAgent(string agentType, string providerName, string? modelOverride = null)
        {
            lock (_lock)
            {
                // Validate provider exists and is compatible
                var provider = _configuration.Providers.FirstOrDefault(p => p.Name == providerName);
                if (provider == null)
                {
                    throw new ArgumentException($"Provider '{providerName}' not found");
                }

                if (!provider.IsEnabled)
                {
                    throw new ArgumentException($"Provider '{providerName}' is not enabled");
                }

                if (!provider.CompatibleAgents.Contains(agentType.ToLowerInvariant()) &&
                    !provider.CompatibleAgents.Contains("all"))
                {
                    throw new ArgumentException($"Provider '{providerName}' is not compatible with agent type '{agentType}'");
                }

                // Update configuration
                switch (agentType.ToLowerInvariant())
                {
                    case "dragon":
                        _configuration.AgentProviders.DragonProvider = providerName;
                        _configuration.AgentProviders.DragonModel = modelOverride;
                        break;
                    case "wyvern":
                        _configuration.AgentProviders.WyvernProvider = providerName;
                        _configuration.AgentProviders.WyvernModel = modelOverride;
                        break;
                    case "wyrm":
                        _configuration.AgentProviders.WyrmProvider = providerName;
                        _configuration.AgentProviders.WyrmModel = modelOverride;
                        break;
                    case "kobold":
                        _configuration.AgentProviders.KoboldProvider = providerName;
                        _configuration.AgentProviders.KoboldModel = modelOverride;
                        break;
                    default:
                        throw new ArgumentException($"Unknown agent type '{agentType}'");
                }

                SaveConfiguration();
                _logger.LogInformation("Updated provider for {AgentType} to {Provider}", agentType, providerName);
            }
        }

        /// <summary>
        /// Enables or disables a provider
        /// </summary>
        public void SetProviderEnabled(string providerName, bool enabled)
        {
            lock (_lock)
            {
                var provider = _configuration.Providers.FirstOrDefault(p => p.Name == providerName);
                if (provider == null)
                {
                    throw new ArgumentException($"Provider '{providerName}' not found");
                }

                provider.IsEnabled = enabled;
                SaveConfiguration();
                _logger.LogInformation("Provider {Provider} enabled={Enabled}", providerName, enabled);
            }
        }

        /// <summary>
        /// Validates that a provider is properly configured
        /// </summary>
        public (bool isValid, string message) ValidateProvider(string providerName)
        {
            lock (_lock)
            {
                var provider = _configuration.Providers.FirstOrDefault(p => p.Name == providerName);
                if (provider == null)
                {
                    return (false, $"Provider '{providerName}' not found");
                }

                if (!provider.IsEnabled)
                {
                    return (false, $"Provider '{providerName}' is disabled");
                }

                // Check API key if required
                if (provider.RequiresApiKey)
                {
                    var apiKeyEnvVar = GetApiKeyEnvironmentVariable(provider.Type);
                    var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar);
                    var configHasKey = provider.Configuration.ContainsKey("apiKey") &&
                                      !string.IsNullOrWhiteSpace(provider.Configuration["apiKey"]);

                    if (string.IsNullOrWhiteSpace(apiKey) && !configHasKey)
                    {
                        return (false, $"API key not found. Please set environment variable: {apiKeyEnvVar} or add apiKey to configuration");
                    }
                }

                return (true, "Provider is properly configured");
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var savedConfig = JsonSerializer.Deserialize<KoboldLairProviderConfiguration>(json);
                    if (savedConfig != null)
                    {
                        // Merge with defaults - use saved agent providers but keep default provider list
                        _configuration.AgentProviders = savedConfig.AgentProviders;
                        _logger.LogInformation("Loaded provider configuration from {Path}", _configPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load provider configuration, using defaults");
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                var json = JsonSerializer.Serialize(_configuration, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_configPath, json);
                _logger.LogInformation("Saved provider configuration to {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save provider configuration");
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
                _ => $"{providerType.ToUpperInvariant()}_API_KEY"
            };
        }
    }
}
