using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Integration tests for <see cref="SqlProviderConfigRepository"/> against a real SQLite file (TASK-073):
/// provider upsert/read/delete, the switchable-models child table, agent assignments, the encrypted-key
/// seam (ciphertext round-trips; metadata updates preserve the key), and the one-time-import guard.
/// Each test gets a fresh temp database.
/// </summary>
public class SqlProviderConfigRepositoryTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private SqlProviderConfigRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_providercfg_{Guid.NewGuid():N}.db");
        _repo = new SqlProviderConfigRepository(_dbPath);
        await _repo.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    private static ProviderConfigEntity Provider(string name = "openai") => new()
    {
        Name = name,
        DisplayName = "OpenAI",
        Type = name,
        Enabled = true,
        BaseUrl = "https://api.openai.com",
        RequiresApiKey = true,
        DefaultModel = "gpt-4o",
        CompatibleAgentsJson = "[\"all\"]",
        ConfigurationJson = "{}"
    };

    [Fact]
    public async Task IsEmpty_is_true_before_any_provider_then_false()
    {
        (await _repo.IsEmptyAsync()).Should().BeTrue();
        await _repo.UpsertProviderAsync(Provider());
        (await _repo.IsEmptyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_then_get_round_trips_a_provider()
    {
        await _repo.UpsertProviderAsync(Provider());

        var loaded = await _repo.GetProviderAsync("openai");
        loaded.Should().NotBeNull();
        loaded!.Type.Should().Be("openai");
        loaded.DefaultModel.Should().Be("gpt-4o");
        loaded.Enabled.Should().BeTrue();

        (await _repo.GetAllProvidersAsync()).Should().ContainSingle(p => p.Name == "openai");
    }

    [Fact]
    public async Task Upsert_updates_metadata_in_place_no_duplicate()
    {
        await _repo.UpsertProviderAsync(Provider());
        var changed = Provider();
        changed.Enabled = false;
        changed.DefaultModel = "gpt-4o-mini";
        await _repo.UpsertProviderAsync(changed);

        var all = await _repo.GetAllProvidersAsync();
        all.Should().ContainSingle(p => p.Name == "openai");
        all[0].Enabled.Should().BeFalse();
        all[0].DefaultModel.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task ApiKey_ciphertext_round_trips_and_metadata_update_preserves_it()
    {
        await _repo.UpsertProviderAsync(Provider());
        await _repo.SetApiKeyCiphertextAsync("openai", "ENC(sk-abc123)");

        (await _repo.GetProviderAsync("openai"))!.ApiKeyCiphertext.Should().Be("ENC(sk-abc123)");

        // A later metadata-only upsert must NOT wipe the stored key.
        var meta = Provider();
        meta.DisplayName = "OpenAI (prod)";
        await _repo.UpsertProviderAsync(meta);

        var after = await _repo.GetProviderAsync("openai");
        after!.DisplayName.Should().Be("OpenAI (prod)");
        after.ApiKeyCiphertext.Should().Be("ENC(sk-abc123)", "metadata upserts must preserve the key");
    }

    [Fact]
    public async Task Provider_has_many_switchable_models()
    {
        await _repo.UpsertProviderAsync(Provider());
        await _repo.UpsertModelAsync(new ProviderModelEntity { ProviderName = "openai", ModelId = "gpt-4o", SortOrder = 0 });
        await _repo.UpsertModelAsync(new ProviderModelEntity { ProviderName = "openai", ModelId = "gpt-4o-mini", SortOrder = 1 });
        await _repo.UpsertModelAsync(new ProviderModelEntity { ProviderName = "openai", ModelId = "o1", SortOrder = 2, Enabled = false });

        var models = await _repo.GetModelsAsync("openai");
        models.Select(m => m.ModelId).Should().Equal("gpt-4o", "gpt-4o-mini", "o1");
        models.Single(m => m.ModelId == "o1").Enabled.Should().BeFalse();

        // Toggling/upserting an existing model edits in place.
        await _repo.UpsertModelAsync(new ProviderModelEntity { ProviderName = "openai", ModelId = "o1", SortOrder = 2, Enabled = true });
        (await _repo.GetModelsAsync("openai")).Single(m => m.ModelId == "o1").Enabled.Should().BeTrue();

        await _repo.DeleteModelAsync("openai", "gpt-4o-mini");
        (await _repo.GetModelsAsync("openai")).Select(m => m.ModelId).Should().Equal("gpt-4o", "o1");
    }

    [Fact]
    public async Task Deleting_a_provider_removes_its_models()
    {
        await _repo.UpsertProviderAsync(Provider());
        await _repo.UpsertModelAsync(new ProviderModelEntity { ProviderName = "openai", ModelId = "gpt-4o" });

        await _repo.DeleteProviderAsync("openai");

        (await _repo.GetProviderAsync("openai")).Should().BeNull();
        (await _repo.GetModelsAsync("openai")).Should().BeEmpty();
    }

    [Fact]
    public async Task Agent_assignments_upsert_and_delete()
    {
        await _repo.UpsertAgentSettingAsync("dragon", "claude", "claude-sonnet-4-6");
        await _repo.UpsertAgentSettingAsync("kobold:csharp", "openai", "gpt-4o");

        var settings = await _repo.GetAgentSettingsAsync();
        settings.Should().HaveCount(2);
        settings.Single(s => s.AgentKey == "dragon").Model.Should().Be("claude-sonnet-4-6");

        // Upsert edits in place.
        await _repo.UpsertAgentSettingAsync("dragon", "openai", "gpt-4o");
        (await _repo.GetAgentSettingsAsync()).Single(s => s.AgentKey == "dragon").Provider.Should().Be("openai");

        await _repo.DeleteAgentSettingAsync("dragon");
        (await _repo.GetAgentSettingsAsync()).Should().ContainSingle(s => s.AgentKey == "kobold:csharp");
    }

    [Fact]
    public async Task Data_survives_a_fresh_repository_pointed_at_the_same_file()
    {
        await _repo.UpsertProviderAsync(Provider());
        await _repo.SetApiKeyCiphertextAsync("openai", "ENC(key)");

        var reopened = new SqlProviderConfigRepository(_dbPath);
        await reopened.InitializeAsync();

        var loaded = await reopened.GetProviderAsync("openai");
        loaded.Should().NotBeNull();
        loaded!.ApiKeyCiphertext.Should().Be("ENC(key)");
    }
}
