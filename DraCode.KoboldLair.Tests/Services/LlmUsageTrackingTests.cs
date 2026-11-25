using Birko.AI.Providers;
using Birko.AI.Resilience.Configuration;
using Birko.AI.Resilience.Services;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Every agent the pipeline builds calls its LLM through a tracked provider tagged with its agent type and project, so
/// usage, budgets and rate limits cover Wyrm, Wyvern and the Kobold planner as well as Kobolds (TASK-105 / FIELD-019).
/// Dragon's council is checked live (its agents are built inside a WebSocket session).
/// </summary>
public class LlmUsageTrackingTests : IAsyncLifetime
{
    private const string ProjectId = "project-105";
    // The configured name differs from the type, as for two vLLM providers (TASK-193)
    private const string ConfiguredName = "lan2";
    private string _dir = null!;
    private SqlProjectRepository _repo = null!;
    private KoboldLairConfiguration _config = null!;
    private ProviderConfigurationService _providers = null!;
    private CostTrackingService _costs = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_tracking_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _repo = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await _repo.InitializeAsync();

        // A provider must exist to build agents; nothing here sends a request to it
        _config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir,
            DefaultProvider = ConfiguredName,
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = ConfiguredName, Type = "ollama", DefaultModel = "m1",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" }, Configuration = new()
                }
            }
        };
        _providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config), Path.Combine(_dir, "user-settings.json"));
        _costs = new CostTrackingService(new CostTrackingConfiguration(), NullLogger<CostTrackingService>.Instance);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private static void ShouldBeTracked(ILlmProvider? provider, string agentType, string? projectId,
        string? providerName = ConfiguredName)
    {
        var tracked = provider.Should().BeOfType<TrackedLlmProvider>().Subject;
        tracked.AgentType.Should().Be(agentType);
        tracked.ProjectId.Should().Be(projectId);
        // Usage records, pricing and rate limits key on the configured name, not "Ollama (m1)" (TASK-193)
        tracked.ProviderName.Should().Be(providerName);
    }

    private async Task<Project> ProjectAsync()
    {
        var project = new Project { Id = ProjectId, Name = "tracked", OwnerId = Guid.NewGuid().ToString() };
        await _repo.AddAsync(project);
        return project;
    }

    [Fact]
    public void Wyvern_calls_through_a_tracked_provider()
    {
        var wyverns = new WyvernFactory(_providers, _repo, _config, costTracker: _costs);

        var wyvern = wyverns.CreateWyvern("tracked", Path.Combine(_dir, "specification.md"), _dir, projectId: ProjectId);

        ShouldBeTracked(wyvern.AnalyzerProvider, "wyvern", ProjectId);
    }

    [Fact]
    public async Task Wyrm_and_Wyrm_pre_analysis_call_through_tracked_providers()
    {
        var project = await ProjectAsync();
        var wyrms = new WyrmFactory(_repo, _providers, costTracker: _costs);

        ShouldBeTracked(wyrms.CreateWyrm(project).Provider, "wyrm", ProjectId);
        ShouldBeTracked(wyrms.CreateWyrmPreAnalysisAgent(project).Provider, "wyrm-preanalysis", ProjectId);
    }

    [Fact]
    public void The_Kobold_planner_calls_through_a_tracked_provider()
    {
        var kobolds = new KoboldFactory(_repo, NullLoggerFactory.Instance, _config, costTracker: _costs);
        var drakes = new DrakeFactory(kobolds, _providers, _config, NullLoggerFactory.Instance, projectRepository: _repo,
            costTracker: _costs);

        var drake = drakes.CreateDrake(Path.Combine(_dir, "tasks", "cli-tasks.md"), "tracked-cli", projectId: ProjectId);

        ShouldBeTracked(drake.PlannerProvider, "kobold-planner", ProjectId);
    }

    [Fact]
    public void A_Kobold_records_its_project_and_provider()
    {
        var kobolds = new KoboldFactory(_repo, NullLoggerFactory.Instance, _config, costTracker: _costs);
        var (type, config, _) = _providers.GetProviderSettingsForKoboldAgentType("coding");

        var kobold = kobolds.CreateKobold(type, "coding", config: config, projectId: ProjectId);

        ShouldBeTracked(kobold.Agent.Provider, "coding", ProjectId);
    }

    [Fact]
    public void Dragon_and_its_council_record_the_configured_provider()
    {
        var (type, config, _) = _providers.GetProviderSettingsForAgent("dragon");

        var provider = KoboldLairAgentFactory.CreateLlmProvider(type, config, "dragon", costTracker: _costs);

        ShouldBeTracked(provider, "dragon", projectId: null);
    }

    [Fact]
    public void Every_provider_config_names_its_configured_provider()
    {
        _providers.GetProviderSettingsForAgent("wyvern").config[ProviderConfigurationService.ConfiguredNameKey]
            .Should().Be(ConfiguredName);
        _providers.GetProviderSettingsForKoboldAgentType("coding").config[ProviderConfigurationService.ConfiguredNameKey]
            .Should().Be(ConfiguredName);
        _providers.GetProviderSettingsByName(ConfiguredName).config[ProviderConfigurationService.ConfiguredNameKey]
            .Should().Be(ConfiguredName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_config_without_a_configured_name_keys_on_the_client_name(string? name)
    {
        var config = new Dictionary<string, string> { ["model"] = "m1" };
        if (name != null) config[ProviderConfigurationService.ConfiguredNameKey] = name;

        var provider = KoboldLairAgentFactory.CreateLlmProvider("ollama", config, "coding", costTracker: _costs);

        ShouldBeTracked(provider, "coding", projectId: null, providerName: null);
    }
}
