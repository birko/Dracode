using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Security;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// DB-backed <see cref="ProviderConfigurationService"/> coverage (TASK-073): one-time import from
/// appsettings/user-settings/env, API keys encrypted at rest + decrypted on read, the DB as the sole
/// runtime authority (env ignored after import), and edits persisting across a fresh service on the same DB.
/// </summary>
public class ProviderConfigurationServiceDbTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private string _userSettingsPath = null!;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_provsvc_{Guid.NewGuid():N}.db");
        _userSettingsPath = Path.Combine(Path.GetTempPath(), $"dracode_usersettings_{Guid.NewGuid():N}.json");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_userSettingsPath); } catch { }
        return Task.CompletedTask;
    }

    private static KoboldLairConfiguration Config() => new()
    {
        DefaultProvider = "claude",
        Providers = new()
        {
            new ProviderConfig
            {
                Name = "claude", Type = "claude", DisplayName = "Claude", DefaultModel = "claude-sonnet-4-6",
                IsEnabled = true, RequiresApiKey = true, CompatibleAgents = new() { "all" },
                Configuration = new() { ["apiKey"] = "cfg-claude-key" }
            },
            new ProviderConfig
            {
                Name = "testprov", Type = "testprov", DefaultModel = "m1",
                IsEnabled = true, RequiresApiKey = true, CompatibleAgents = new() { "all" },
                Configuration = new()
            }
        }
    };

    private async Task<ProviderConfigurationService> NewServiceAsync(KoboldLairConfiguration cfg)
    {
        var repo = new SqlProviderConfigRepository(_dbPath);
        await repo.InitializeAsync();
        var cipher = new ProviderKeyCipher(new ConfigSecretProvider(
            new Dictionary<string, string> { [ProviderKeyCipher.MasterKeySecretName] = "unit-test-master-key" }));
        var svc = new ProviderConfigurationService(
            NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(cfg),
            _userSettingsPath,
            repo,
            cipher);
        await svc.InitializeAsync();
        return svc;
    }

    [Fact]
    public async Task Import_encrypts_key_at_rest_and_decrypts_on_read()
    {
        var svc = await NewServiceAsync(Config());

        // Resolve the default-provider (claude) settings → the decrypted key is handed to the provider.
        var (_, config, _) = svc.GetProviderSettingsForAgent("dragon");
        config["apiKey"].Should().Be("cfg-claude-key");

        // …but the DB row stores ciphertext, never the plaintext.
        var repo = new SqlProviderConfigRepository(_dbPath);
        await repo.InitializeAsync();
        var row = await repo.GetProviderAsync("claude");
        row!.ApiKeyCiphertext.Should().NotBeNullOrEmpty();
        row.ApiKeyCiphertext.Should().NotBe("cfg-claude-key");
    }

    [Fact]
    public async Task Db_is_the_runtime_authority_env_is_ignored_after_import()
    {
        Environment.SetEnvironmentVariable("TESTPROV_API_KEY", "env-at-import");
        try
        {
            var svc = await NewServiceAsync(Config());          // import reads env for testprov
            Environment.SetEnvironmentVariable("TESTPROV_API_KEY", "env-CHANGED-after-import");

            svc.SetProviderForAgent("dragon", "testprov", "m1");
            var (_, config, _) = svc.GetProviderSettingsForAgent("dragon");

            config["apiKey"].Should().Be("env-at-import",
                "the key is read from the DB (seeded once), not from the env var at runtime");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TESTPROV_API_KEY", null);
        }
    }

    [Fact]
    public async Task Edits_persist_across_a_fresh_service_on_the_same_db_no_reimport()
    {
        var svc1 = await NewServiceAsync(Config());
        svc1.SetProviderForKoboldAgentType("csharp", "testprov", "m1");
        svc1.SetProviderForAgent("wyvern", "testprov", "m1");

        // A second service over the same DB: the table is non-empty → no re-import; edits are visible.
        var svc2 = await NewServiceAsync(Config());
        var settings = svc2.GetUserSettings();
        settings.WyvernProvider.Should().Be("testprov");
        settings.WyvernModel.Should().Be("m1");
        settings.KoboldAgentTypeSettings.Should().ContainSingle(s => s.AgentType == "csharp" && s.Provider == "testprov");
    }

    [Fact]
    public async Task Removing_a_kobold_agent_type_override_is_reconciled_in_the_db()
    {
        var svc1 = await NewServiceAsync(Config());
        svc1.SetProviderForKoboldAgentType("csharp", "testprov", "m1");
        // Clearing it (both null) must delete the row, not leave a stale one.
        svc1.SetProviderForKoboldAgentType("csharp", null, null);

        var svc2 = await NewServiceAsync(Config());
        svc2.GetUserSettings().KoboldAgentTypeSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task Providers_are_loaded_from_the_db_into_the_public_api()
    {
        var svc = await NewServiceAsync(Config());
        svc.GetAllProviders().Select(p => p.Name).Should().BeEquivalentTo(new[] { "claude", "testprov" });
        svc.GetAvailableProviders().Should().OnlyContain(p => p.IsEnabled);
        svc.GetDefaultProvider().Should().Be("claude");
    }
}
