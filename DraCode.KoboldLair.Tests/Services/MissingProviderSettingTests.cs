using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Security;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// An agent setting that names a provider which no longer exists falls back to the default provider instead of breaking
/// the agent, a missing default too fails with a readable message, and deleting a provider clears the settings that name
/// it and refuses to delete the default (TASK-084 / FIELD-003).
/// </summary>
public class MissingProviderSettingTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private SqlProviderConfigRepository _repo = null!;
    private ProviderConfigurationService _svc = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_missingprov_{Guid.NewGuid():N}.db");
        _repo = new SqlProviderConfigRepository(_dbPath);
        await _repo.InitializeAsync();
        var cipher = new ProviderKeyCipher(new ConfigSecretProvider(
            new Dictionary<string, string> { [ProviderKeyCipher.MasterKeySecretName] = "unit-test-master-key" }));

        var config = new KoboldLairConfiguration
        {
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = "local", Type = "ollama", DefaultModel = "default-model",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" }, Configuration = new()
                },
                new ProviderConfig
                {
                    Name = "other", Type = "llamacpp", DefaultModel = "other-model",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" }, Configuration = new()
                }
            }
        };
        _svc = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance, Options.Create(config),
            Path.Combine(Path.GetTempPath(), $"dracode_missingprov_{Guid.NewGuid():N}.json"), _repo, cipher);
        await _svc.InitializeAsync();
        _svc.SetDefaultProvider("local");
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("dragon")]
    [InlineData("wyrm")]
    [InlineData("wyvern")]
    [InlineData("kobold")]
    public async Task A_setting_naming_a_missing_provider_falls_back_to_the_default(string agent)
    {
        // A row left behind by a rename or delete — exactly what broke Dragon on the dev DB ("zai" vs "pi-zai")
        await _repo.UpsertAgentSettingAsync(agent, "zai", "glm-5.3");
        await _svc.ReloadAsync();

        var (type, config, _) = _svc.GetProviderSettingsForAgent(agent);

        type.Should().Be("ollama");
        config["model"].Should().Be("default-model", "the stale setting's model belonged to the missing provider");
    }

    [Fact]
    public async Task A_kobold_agent_type_setting_naming_a_missing_provider_falls_back_to_the_default()
    {
        await _repo.UpsertAgentSettingAsync("kobold:csharp", "gone", "gone-model");
        await _svc.ReloadAsync();

        var (type, config, _) = _svc.GetProviderSettingsForKoboldAgentType("csharp");

        type.Should().Be("ollama");
        config["model"].Should().Be("default-model");
    }

    [Fact]
    public async Task A_setting_naming_an_existing_provider_keeps_its_provider_and_model()
    {
        await _repo.UpsertAgentSettingAsync("dragon", "other", "picked-model");
        await _svc.ReloadAsync();

        var (type, config, _) = _svc.GetProviderSettingsForAgent("dragon");

        type.Should().Be("llamacpp");
        config["model"].Should().Be("picked-model");
    }

    [Fact]
    public async Task When_the_default_is_missing_too_the_error_names_both()
    {
        await _repo.UpsertAgentSettingAsync("dragon", "gone", null);
        await _repo.UpsertAgentSettingAsync("__default__", "also-gone", null);
        await _svc.ReloadAsync();

        var act = () => _svc.GetProviderSettingsForAgent("dragon");

        act.Should().Throw<InvalidOperationException>().WithMessage("*'gone'*'also-gone'*");
    }

    [Fact]
    public async Task Deleting_a_provider_clears_the_settings_that_name_it()
    {
        _svc.SetProviderForAgent("dragon", "other", "picked-model");
        _svc.SetProviderForAgent("wyvern", "local");
        _svc.SetProviderForKoboldAgentType("python", "other");

        var cleared = await _svc.DeleteProviderAsync("other");

        cleared.Should().BeEquivalentTo("dragon", "kobold:python");
        var settings = _svc.GetUserSettings();
        settings.DragonProvider.Should().BeNull();
        settings.DragonModel.Should().BeNull();
        settings.WyvernProvider.Should().Be("local");
        settings.KoboldAgentTypeSettings.Should().BeEmpty();
        (await _repo.GetProviderAsync("other")).Should().BeNull();
    }

    [Fact]
    public async Task Deleting_the_default_provider_is_refused()
    {
        var act = () => _svc.DeleteProviderAsync("local");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*default provider*");
        (await _repo.GetProviderAsync("local")).Should().NotBeNull();
    }
}
