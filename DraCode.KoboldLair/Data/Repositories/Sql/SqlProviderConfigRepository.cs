using Birko.Configuration;
using Birko.Data.SQL.Repositories;
using DraCode.KoboldLair.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQLite-backed store for LLM provider configuration (TASK-073): providers, their switchable models,
    /// and agent→provider/model assignments. Mirrors <see cref="SqlPlanRepository"/> (one wrapped
    /// <see cref="AsyncSqLiteModelRepository{T}"/> per entity, all pointed at the shared DB file). API keys
    /// are persisted only as ciphertext — encryption/decryption is the caller's concern
    /// (<c>ProviderConfigurationService</c> via <c>ProviderKeyCipher</c>); this layer never sees plaintext.
    /// </summary>
    public class SqlProviderConfigRepository
    {
        private readonly AsyncSqLiteModelRepository<ProviderConfigEntity> _providers = new();
        private readonly AsyncSqLiteModelRepository<ProviderModelEntity> _models = new();
        private readonly AsyncSqLiteModelRepository<AgentProviderSettingEntity> _settings = new();
        private readonly ILogger<SqlProviderConfigRepository>? _logger;

        public SqlProviderConfigRepository(string dbPath, ILogger<SqlProviderConfigRepository>? logger = null)
        {
            _logger = logger;

            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            var settings = new PasswordSettings(dbDir, dbFile);
            _providers.SetSettings(settings);
            _models.SetSettings(settings);
            _settings.SetSettings(settings);
        }

        public async Task InitializeAsync()
        {
            await _providers.CreateSchemaAsync();
            await _models.CreateSchemaAsync();
            await _settings.CreateSchemaAsync();
            _logger?.LogInformation("SQLite provider-config repository initialized");
        }

        // ---- providers ---------------------------------------------------------------------------

        /// <summary>All providers (any enabled state).</summary>
        public async Task<List<ProviderConfigEntity>> GetAllProvidersAsync()
        {
            var all = await _providers.ReadAsync(CancellationToken.None);
            return all.ToList();
        }

        public Task<ProviderConfigEntity?> GetProviderAsync(string name) =>
            _providers.ReadAsync(e => e.Name == name, CancellationToken.None);

        /// <summary>True when no provider rows exist — the one-time-import guard.</summary>
        public async Task<bool> IsEmptyAsync() => (await _providers.ReadAsync(CancellationToken.None)).Any() == false;

        /// <summary>
        /// Creates or updates a provider's metadata. On UPDATE the existing
        /// <see cref="ProviderConfigEntity.ApiKeyCiphertext"/> is preserved (manage the key via
        /// <see cref="SetApiKeyCiphertextAsync"/>); on CREATE any ciphertext on <paramref name="entity"/> is kept.
        /// </summary>
        public async Task UpsertProviderAsync(ProviderConfigEntity entity)
        {
            var existing = await _providers.ReadAsync(e => e.Name == entity.Name, CancellationToken.None);
            if (existing != null)
            {
                existing.DisplayName = entity.DisplayName;
                existing.Type = entity.Type;
                existing.Enabled = entity.Enabled;
                existing.BaseUrl = entity.BaseUrl;
                existing.RequiresApiKey = entity.RequiresApiKey;
                existing.DefaultModel = entity.DefaultModel;
                existing.CompatibleAgentsJson = entity.CompatibleAgentsJson;
                existing.ConfigurationJson = entity.ConfigurationJson;
                existing.UpdatedAt = DateTime.UtcNow;
                await _providers.UpdateAsync(existing);
            }
            else
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                await _providers.CreateAsync(entity);
            }
        }

        /// <summary>Sets (or clears, when null) a provider's encrypted API key. No-op if the provider is unknown.</summary>
        public async Task SetApiKeyCiphertextAsync(string name, string? ciphertext)
        {
            var existing = await _providers.ReadAsync(e => e.Name == name, CancellationToken.None);
            if (existing == null) return;
            existing.ApiKeyCiphertext = ciphertext;
            existing.UpdatedAt = DateTime.UtcNow;
            await _providers.UpdateAsync(existing);
        }

        /// <summary>Deletes a provider and all of its model rows.</summary>
        public async Task DeleteProviderAsync(string name)
        {
            var existing = await _providers.ReadAsync(e => e.Name == name, CancellationToken.None);
            if (existing != null)
                await _providers.DeleteAsync(existing);

            foreach (var model in await GetModelsAsync(name))
                await _models.DeleteAsync(model);
        }

        // ---- models ------------------------------------------------------------------------------

        public async Task<List<ProviderModelEntity>> GetModelsAsync(string providerName)
        {
            var rows = await _models.ReadAsync(e => e.ProviderName == providerName, orderBy: null, limit: null, offset: null);
            return rows.OrderBy(m => m.SortOrder).ToList();
        }

        /// <summary>Creates or updates a model row, keyed by (provider, modelId).</summary>
        public async Task UpsertModelAsync(ProviderModelEntity entity)
        {
            var existing = await _models.ReadAsync(
                e => e.ProviderName == entity.ProviderName && e.ModelId == entity.ModelId, CancellationToken.None);
            if (existing != null)
            {
                existing.DisplayName = entity.DisplayName;
                existing.Enabled = entity.Enabled;
                existing.SortOrder = entity.SortOrder;
                existing.Reasoning = entity.Reasoning;
                existing.ContextWindow = entity.ContextWindow;
                existing.InputModalities = entity.InputModalities;
                existing.UpdatedAt = DateTime.UtcNow;
                await _models.UpdateAsync(existing);
            }
            else
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                await _models.CreateAsync(entity);
            }
        }

        public async Task DeleteModelAsync(string providerName, string modelId)
        {
            var existing = await _models.ReadAsync(
                e => e.ProviderName == providerName && e.ModelId == modelId, CancellationToken.None);
            if (existing != null)
                await _models.DeleteAsync(existing);
        }

        // ---- agent assignments -------------------------------------------------------------------

        public async Task<List<AgentProviderSettingEntity>> GetAgentSettingsAsync()
        {
            var all = await _settings.ReadAsync(CancellationToken.None);
            return all.ToList();
        }

        /// <summary>Creates or updates an agent assignment, keyed by <see cref="AgentProviderSettingEntity.AgentKey"/>.</summary>
        public async Task UpsertAgentSettingAsync(string agentKey, string? provider, string? model)
        {
            var existing = await _settings.ReadAsync(e => e.AgentKey == agentKey, CancellationToken.None);
            if (existing != null)
            {
                existing.Provider = provider;
                existing.Model = model;
                existing.UpdatedAt = DateTime.UtcNow;
                await _settings.UpdateAsync(existing);
            }
            else
            {
                await _settings.CreateAsync(new AgentProviderSettingEntity
                {
                    AgentKey = agentKey,
                    Provider = provider,
                    Model = model,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        public async Task DeleteAgentSettingAsync(string agentKey)
        {
            var existing = await _settings.ReadAsync(e => e.AgentKey == agentKey, CancellationToken.None);
            if (existing != null)
                await _settings.DeleteAsync(existing);
        }
    }
}
