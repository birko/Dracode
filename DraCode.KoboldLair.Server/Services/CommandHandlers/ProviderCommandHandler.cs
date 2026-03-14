using DraCode.KoboldLair.Services;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services.CommandHandlers
{
    public class ProviderCommandHandler
    {
        private readonly ProviderConfigurationService _providerConfigService;

        public ProviderCommandHandler(ProviderConfigurationService providerConfigService)
        {
            _providerConfigService = providerConfigService;
        }

        public Task<object> GetProvidersAsync()
        {
            var providers = _providerConfigService.GetAllProviders().Select(p => new
            {
                p.Name,
                p.DisplayName,
                p.Type,
                p.DefaultModel,
                p.CompatibleAgents,
                p.IsEnabled,
                p.RequiresApiKey,
                p.Description,
                IsConfigured = _providerConfigService.ValidateProvider(p.Name).isValid
            });

            var userSettings = _providerConfigService.GetUserSettings();

            return Task.FromResult<object>(new
            {
                providers,
                defaultProvider = _providerConfigService.GetDefaultProvider(),
                agentProviders = new
                {
                    dragonProvider = userSettings.DragonProvider,
                    wyrmProvider = userSettings.WyrmProvider,
                    wyvernProvider = userSettings.WyvernProvider,
                    koboldProvider = userSettings.KoboldProvider,
                    dragonModel = userSettings.DragonModel,
                    wyrmModel = userSettings.WyrmModel,
                    wyvernModel = userSettings.WyvernModel,
                    koboldModel = userSettings.KoboldModel
                }
            });
        }

        public Task<object> ConfigureProviderAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var agentType = data.Value.GetProperty("agentType").GetString();
            var providerName = data.Value.GetProperty("providerName").GetString();
            var modelOverride = data.Value.TryGetProperty("modelOverride", out var model) ? model.GetString() : null;

            _providerConfigService.SetProviderForAgent(agentType!, providerName!, modelOverride);
            return Task.FromResult<object>(new { success = true, message = $"Updated {agentType} to use {providerName}" });
        }

        public Task<object> ValidateProviderAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var providerName = data.Value.GetProperty("providerName").GetString();
            var (isValid, message) = _providerConfigService.ValidateProvider(providerName!);
            return Task.FromResult<object>(new { isValid, message, providerName });
        }

        public Task<object> GetProvidersForAgentAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var agentType = data.Value.GetProperty("agentType").GetString();
            var providers = _providerConfigService.GetProvidersForAgent(agentType!);
            var currentProvider = _providerConfigService.GetProviderForAgent(agentType!);

            return Task.FromResult<object>(new
            {
                agentType,
                currentProvider,
                availableProviders = providers.Select(p => new
                {
                    p.Name,
                    p.DisplayName,
                    p.DefaultModel,
                    p.Description,
                    IsConfigured = _providerConfigService.ValidateProvider(p.Name).isValid
                })
            });
        }
    }
}
