using System.Text.Json;
using Birko.AI;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Security;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Service for managing provider configuration.
    ///
    /// Two backing modes (chosen by the constructor):
    /// <list type="bullet">
    ///   <item><b>DB-backed</b> (TASK-073) — when a <see cref="SqlProviderConfigRepository"/> + <see cref="ProviderKeyCipher"/>
    ///   are supplied: providers, their switchable models, and agent→provider/model assignments live in the
    ///   database (API keys encrypted at rest). <see cref="InitializeAsync"/> imports the legacy
    ///   appsettings/user-settings/env config once into an empty DB, then the DB is the source of truth and
    ///   env vars are ignored. Edits persist to the DB and refresh the in-memory cache (runtime reload).</item>
    ///   <item><b>Legacy</b> — when no repo is supplied: providers come from <c>appsettings.json</c>, agent
    ///   assignments from <c>user-settings.json</c>, and API keys from environment variables (original behaviour).</item>
    /// </list>
    /// The public API is identical in both modes, so callers (factories, Drake) are unaffected.
    /// </summary>
    public class ProviderConfigurationService
    {
        private readonly ILogger<ProviderConfigurationService> _logger;
        private readonly KoboldLairConfiguration _config;
        private readonly string _userSettingsPath;
        private readonly SqlProviderConfigRepository? _repo;
        private readonly ProviderKeyCipher? _cipher;
        private readonly bool _dbMode;

        private List<ProviderConfig> _providers;
        private UserSettings _userSettings;
        // DB mode only: provider name → encrypted API key (decrypted on demand in ResolveApiKey).
        private Dictionary<string, string?> _apiKeyCiphertext = new(StringComparer.OrdinalIgnoreCase);
        // DB mode only: the default provider, stored as a reserved agent-setting row (the authoritative source — no appsettings fallback).
        private string? _defaultProviderDb;
        private const string DefaultProviderKey = "__default__";
        private readonly object _lock = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public ProviderConfigurationService(
            ILogger<ProviderConfigurationService> logger,
            IOptions<KoboldLairConfiguration> config,
            string userSettingsPath = "./user-settings.json",
            SqlProviderConfigRepository? repo = null,
            ProviderKeyCipher? cipher = null)
        {
            _logger = logger;
            _config = config.Value;
            _userSettingsPath = userSettingsPath;
            _repo = repo;
            _cipher = cipher;
            _dbMode = repo != null && cipher != null;
            _userSettings = new UserSettings();

            if (_dbMode)
            {
                // Cache is filled by InitializeAsync (no IO in the ctor).
                _providers = new List<ProviderConfig>();
            }
            else
            {
                _providers = _config.Providers;
                LogConfiguration();
                LoadUserSettings();
            }
        }

        /// <summary>
        /// DB mode: resolve the master key, import legacy config into an empty DB once, then load the cache.
        /// No-op in legacy mode (the ctor already loaded everything). Call once at startup.
        /// </summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!_dbMode) return;

            await _cipher!.InitializeAsync(ct);

            if (await _repo!.IsEmptyAsync())
            {
                _logger.LogInformation("Provider config DB is empty — importing appsettings + user-settings + env keys (one-time)");
                await ImportLegacyConfigAsync();
            }

            await ReloadCacheAsync();
            LogConfiguration();
            LogUserSettings();
        }

        private void LogConfiguration()
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("Provider Configuration ({Mode})", _dbMode ? "database" : "appsettings");
            _logger.LogInformation("========================================");
            _logger.LogInformation("Default Provider: {Default}", _config.DefaultProvider);
            _logger.LogInformation("Providers ({Count}):", _providers.Count);
            foreach (var provider in _providers)
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
                return _providers.Where(p => p.IsEnabled).ToList();
            }
        }

        /// <summary>
        /// Gets all providers regardless of enabled status
        /// </summary>
        public List<ProviderConfig> GetAllProviders()
        {
            lock (_lock)
            {
                return _providers.ToList();
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
                return _providers
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

                return userProvider ?? EffectiveDefault();
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
                var agentTypeSetting = _userSettings.KoboldAgentTypeSettings
                    .FirstOrDefault(s => s.AgentType.Equals(agentType, StringComparison.OrdinalIgnoreCase));

                if (agentTypeSetting?.Provider != null)
                {
                    return agentTypeSetting.Provider;
                }

                return _userSettings.KoboldProvider ?? EffectiveDefault();
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
                var providerConfig = _providers.FirstOrDefault(p => p.Name == providerName)
                    ?? throw new InvalidOperationException($"Provider '{providerName}' not found for Kobold agent type '{agentType}'");

                var config = new Dictionary<string, string>(providerConfig.Configuration);

                var agentTypeSetting = _userSettings.KoboldAgentTypeSettings
                    .FirstOrDefault(s => s.AgentType.Equals(agentType, StringComparison.OrdinalIgnoreCase));

                var modelOverride = agentTypeSetting?.Model ?? _userSettings.KoboldModel;
                config["model"] = modelOverride ?? providerConfig.DefaultModel;

                ApplyApiKey(providerConfig, config);

                var options = new AgentOptions
                {
                    WorkingDirectory = workingDirectory ?? "./workspace",
                    Verbose = false
                };

                return (providerConfig.Type, config, options);
            }
        }

        /// <summary>
        /// Sets the provider for a specific Kobold agent type (persisted to DB or user-settings.json)
        /// </summary>
        public void SetProviderForKoboldAgentType(string agentType, string? provider, string? model = null)
        {
            lock (_lock)
            {
                if (provider != null)
                {
                    var providerConfig = _providers.FirstOrDefault(p => p.Name == provider)
                        ?? throw new ArgumentException($"Provider '{provider}' not found");
                    if (!providerConfig.IsEnabled)
                        throw new ArgumentException($"Provider '{provider}' is not enabled");
                }

                var existingEntry = _userSettings.KoboldAgentTypeSettings
                    .FirstOrDefault(s => s.AgentType.Equals(agentType, StringComparison.OrdinalIgnoreCase));

                if (provider == null && model == null)
                {
                    if (existingEntry != null)
                    {
                        _userSettings.KoboldAgentTypeSettings.Remove(existingEntry);
                        _logger.LogInformation("Removed Kobold agent type settings for {AgentType}", agentType);
                    }
                }
                else if (existingEntry != null)
                {
                    existingEntry.Provider = provider;
                    existingEntry.Model = model;
                    _logger.LogInformation("Updated Kobold agent type {AgentType} to provider={Provider}, model={Model}",
                        agentType, provider ?? "(default)", model ?? "(default)");
                }
                else
                {
                    _userSettings.KoboldAgentTypeSettings.Add(new KoboldAgentTypeProviderSettings
                    {
                        AgentType = agentType.ToLowerInvariant(),
                        Provider = provider,
                        Model = model
                    });
                    _logger.LogInformation("Set Kobold agent type {AgentType} to provider={Provider}, model={Model}",
                        agentType, provider ?? "(default)", model ?? "(default)");
                }

                PersistUserSettings();
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
                var providerConfig = _providers.FirstOrDefault(p => p.Name == providerName)
                    ?? throw new InvalidOperationException($"Provider '{providerName}' not found for agent type '{agentType}'");

                var config = new Dictionary<string, string>(providerConfig.Configuration);

                var modelOverride = agentType.ToLowerInvariant() switch
                {
                    "dragon" => _userSettings.DragonModel,
                    "wyvern" => _userSettings.WyvernModel,
                    "wyrm" => _userSettings.WyrmModel,
                    "kobold" => _userSettings.KoboldModel,
                    _ => null
                };

                config["model"] = modelOverride ?? providerConfig.DefaultModel;

                ApplyApiKey(providerConfig, config);

                var options = new AgentOptions
                {
                    WorkingDirectory = workingDirectory ?? "./workspace",
                    Verbose = false
                };

                return (providerConfig.Type, config, options);
            }
        }

        /// <summary>
        /// Updates the provider selection for an agent type (persisted to DB or user-settings.json)
        /// </summary>
        public void SetProviderForAgent(string agentType, string providerName, string? modelOverride = null)
        {
            lock (_lock)
            {
                var provider = _providers.FirstOrDefault(p => p.Name == providerName)
                    ?? throw new ArgumentException($"Provider '{providerName}' not found");
                if (!provider.IsEnabled)
                    throw new ArgumentException($"Provider '{providerName}' is not enabled");

                var type = agentType.ToLowerInvariant();
                if (!provider.CompatibleAgents.Contains(type) && !provider.CompatibleAgents.Contains("all"))
                    throw new ArgumentException($"Provider '{providerName}' is not compatible with '{agentType}'");

                switch (type)
                {
                    case "dragon": _userSettings.DragonProvider = providerName; _userSettings.DragonModel = modelOverride; break;
                    case "wyvern": _userSettings.WyvernProvider = providerName; _userSettings.WyvernModel = modelOverride; break;
                    case "wyrm": _userSettings.WyrmProvider = providerName; _userSettings.WyrmModel = modelOverride; break;
                    case "kobold": _userSettings.KoboldProvider = providerName; _userSettings.KoboldModel = modelOverride; break;
                    default: throw new ArgumentException($"Unknown agent type '{agentType}'");
                }

                PersistUserSettings();
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
                var provider = _providers.FirstOrDefault(p => p.Name == providerName);
                if (provider == null)
                    return (false, $"Provider '{providerName}' not found");

                if (!provider.IsEnabled)
                    return (false, $"Provider '{providerName}' is disabled");

                if (provider.RequiresApiKey && string.IsNullOrWhiteSpace(ResolveApiKey(provider)))
                {
                    return _dbMode
                        ? (false, $"API key required for '{providerName}'. Set it via the provider config (no key stored).")
                        : (false, $"API key required. Set environment variable: {GetApiKeyEnvironmentVariable(provider.Type)}");
                }

                return (true, "Provider is configured correctly");
            }
        }

        /// <summary>
        /// Gets the default provider name. In DB mode this is the authoritative DB flag (no appsettings
        /// fallback); in legacy mode it's the appsettings value.
        /// </summary>
        public string GetDefaultProvider()
        {
            lock (_lock) return EffectiveDefault();
        }

        /// <summary>The effective default provider for the current mode (no lock; callers hold <see cref="_lock"/>).</summary>
        private string EffectiveDefault() => _dbMode ? (_defaultProviderDb ?? "") : _config.DefaultProvider;

        /// <summary>
        /// Sets the default provider (DB mode only) — a reserved DB flag, not appsettings. Validates the
        /// provider exists and is enabled, persists it, and updates the cache.
        /// </summary>
        public void SetDefaultProvider(string providerName)
        {
            lock (_lock)
            {
                if (!_dbMode)
                    throw new InvalidOperationException("the default provider can only be changed in DB mode");
                var provider = _providers.FirstOrDefault(p => p.Name == providerName)
                    ?? throw new ArgumentException($"Provider '{providerName}' not found");
                if (!provider.IsEnabled)
                    throw new ArgumentException($"Provider '{providerName}' is not enabled");

                _repo!.UpsertAgentSettingAsync(DefaultProviderKey, providerName, null).GetAwaiter().GetResult();
                _defaultProviderDb = providerName;
                _logger.LogInformation("Default provider set to {Provider}", providerName);
            }
        }

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

        /// <summary>
        /// Re-reads providers + agent settings from the database into the in-memory cache (DB mode only;
        /// no-op in legacy mode). The admin REST surface calls this after a repo write so the change takes
        /// effect on the next agent run without a server restart.
        /// </summary>
        public async Task ReloadAsync()
        {
            if (_dbMode) await ReloadCacheAsync();
        }

        // ---- API key resolution -----------------------------------------------------------------

        /// <summary>Adds the resolved API key to <paramref name="config"/> when the provider needs one.</summary>
        private void ApplyApiKey(ProviderConfig providerConfig, Dictionary<string, string> config)
        {
            if (!providerConfig.RequiresApiKey) return;
            var apiKey = ResolveApiKey(providerConfig);
            if (!string.IsNullOrWhiteSpace(apiKey))
                config["apiKey"] = apiKey;
            else if (!config.ContainsKey("apiKey") || string.IsNullOrWhiteSpace(config["apiKey"]))
                _logger.LogWarning("Provider {Provider} requires an API key but none is configured", providerConfig.Name);
        }

        /// <summary>DB mode: decrypt the stored ciphertext. Legacy: env var, then config dict.</summary>
        private string? ResolveApiKey(ProviderConfig provider)
        {
            if (_dbMode)
                return _cipher!.Decrypt(_apiKeyCiphertext.GetValueOrDefault(provider.Name));

            var envKey = Environment.GetEnvironmentVariable(GetApiKeyEnvironmentVariable(provider.Type));
            if (!string.IsNullOrWhiteSpace(envKey)) return envKey;
            return provider.Configuration.TryGetValue("apiKey", out var k) ? k : null;
        }

        // ---- DB-mode load / import / persist -----------------------------------------------------

        private async Task ReloadCacheAsync()
        {
            var entities = await _repo!.GetAllProvidersAsync();
            var providers = entities.Select(MapToProviderConfig).ToList();
            var ciphertext = entities.ToDictionary(e => e.Name, e => e.ApiKeyCiphertext, StringComparer.OrdinalIgnoreCase);
            var rows = await _repo.GetAgentSettingsAsync();
            var settings = MapUserSettings(rows);
            var dbDefault = rows.FirstOrDefault(r => r.AgentKey == DefaultProviderKey)?.Provider;

            lock (_lock)
            {
                _providers = providers;
                _apiKeyCiphertext = ciphertext;
                _userSettings = settings;
                _defaultProviderDb = dbDefault;
            }
        }

        private async Task ImportLegacyConfigAsync()
        {
            foreach (var cfg in _config.Providers)
            {
                await _repo!.UpsertProviderAsync(MapToEntity(cfg));
                if (!string.IsNullOrWhiteSpace(cfg.DefaultModel))
                    await _repo.UpsertModelAsync(new ProviderModelEntity { ProviderName = cfg.Name, ModelId = cfg.DefaultModel, SortOrder = 0 });
            }

            var fileSettings = TryReadUserSettingsFile(_userSettingsPath);
            if (fileSettings != null)
                await PersistUserSettingsToDbAsync(fileSettings);
        }

        private ProviderConfigEntity MapToEntity(ProviderConfig cfg)
        {
            var config = new Dictionary<string, string>(cfg.Configuration);
            config.TryGetValue("apiKey", out var keyFromCfg);
            config.Remove("apiKey");

            var envKey = cfg.RequiresApiKey
                ? Environment.GetEnvironmentVariable(GetApiKeyEnvironmentVariable(cfg.Type))
                : null;
            var plainKey = !string.IsNullOrWhiteSpace(keyFromCfg) ? keyFromCfg
                : (!string.IsNullOrWhiteSpace(envKey) ? envKey : null);

            return new ProviderConfigEntity
            {
                Name = cfg.Name,
                DisplayName = cfg.DisplayName,
                Type = cfg.Type,
                Enabled = cfg.IsEnabled,
                BaseUrl = config.GetValueOrDefault("baseUrl") ?? config.GetValueOrDefault("endpoint"),
                RequiresApiKey = cfg.RequiresApiKey,
                DefaultModel = cfg.DefaultModel,
                CompatibleAgentsJson = JsonSerializer.Serialize(cfg.CompatibleAgents, JsonOpts),
                ConfigurationJson = JsonSerializer.Serialize(config, JsonOpts),
                ApiKeyCiphertext = plainKey != null ? _cipher!.Encrypt(plainKey) : null
            };
        }

        private static ProviderConfig MapToProviderConfig(ProviderConfigEntity e) => new()
        {
            Name = e.Name,
            DisplayName = e.DisplayName ?? "",
            Type = e.Type,
            DefaultModel = e.DefaultModel ?? "",
            IsEnabled = e.Enabled,
            RequiresApiKey = e.RequiresApiKey,
            CompatibleAgents = SafeDeserialize<List<string>>(e.CompatibleAgentsJson) ?? new(),
            Configuration = SafeDeserialize<Dictionary<string, string>>(e.ConfigurationJson) ?? new()
        };

        private static UserSettings MapUserSettings(IEnumerable<AgentProviderSettingEntity> rows)
        {
            var us = new UserSettings();
            foreach (var r in rows)
            {
                switch (r.AgentKey)
                {
                    case "dragon": us.DragonProvider = r.Provider; us.DragonModel = r.Model; break;
                    case "wyvern": us.WyvernProvider = r.Provider; us.WyvernModel = r.Model; break;
                    case "wyrm": us.WyrmProvider = r.Provider; us.WyrmModel = r.Model; break;
                    case "kobold": us.KoboldProvider = r.Provider; us.KoboldModel = r.Model; break;
                    default:
                        if (r.AgentKey.StartsWith("kobold:", StringComparison.Ordinal))
                            us.KoboldAgentTypeSettings.Add(new KoboldAgentTypeProviderSettings
                            {
                                AgentType = r.AgentKey["kobold:".Length..],
                                Provider = r.Provider,
                                Model = r.Model
                            });
                        break;
                }
            }
            return us;
        }

        private async Task PersistUserSettingsToDbAsync(UserSettings us)
        {
            await _repo!.UpsertAgentSettingAsync("dragon", us.DragonProvider, us.DragonModel);
            await _repo.UpsertAgentSettingAsync("wyvern", us.WyvernProvider, us.WyvernModel);
            await _repo.UpsertAgentSettingAsync("wyrm", us.WyrmProvider, us.WyrmModel);
            await _repo.UpsertAgentSettingAsync("kobold", us.KoboldProvider, us.KoboldModel);

            var desired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in us.KoboldAgentTypeSettings)
            {
                var key = $"kobold:{s.AgentType.ToLowerInvariant()}";
                desired.Add(key);
                await _repo.UpsertAgentSettingAsync(key, s.Provider, s.Model);
            }

            // Reconcile: drop kobold:* rows no longer present in the current settings.
            foreach (var existing in await _repo.GetAgentSettingsAsync())
                if (existing.AgentKey.StartsWith("kobold:", StringComparison.Ordinal) && !desired.Contains(existing.AgentKey))
                    await _repo.DeleteAgentSettingAsync(existing.AgentKey);
        }

        /// <summary>Persists the current <see cref="_userSettings"/> to the active backing store.</summary>
        private void PersistUserSettings()
        {
            if (_dbMode)
                PersistUserSettingsToDbAsync(_userSettings).GetAwaiter().GetResult();
            else
                SaveUserSettings();
        }

        // ---- legacy file load/save ---------------------------------------------------------------

        private void LoadUserSettings()
        {
            var settings = TryReadUserSettingsFile(_userSettingsPath);
            if (settings != null)
            {
                _userSettings = settings;
                _logger.LogInformation("Loaded user settings from {Path}", _userSettingsPath);
                LogUserSettings();
            }
        }

        private UserSettings? TryReadUserSettingsFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<UserSettings>(json, JsonOpts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load user settings from {Path}", path);
            }
            return null;
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
                        setting.AgentType, setting.Provider ?? "(default)", setting.Model ?? "(default)");
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
                File.WriteAllText(_userSettingsPath, json);
                _logger.LogInformation("Saved user settings to {Path}", _userSettingsPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save user settings");
            }
        }

        private static T? SafeDeserialize<T>(string json)
        {
            try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
            catch { return default; }
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
