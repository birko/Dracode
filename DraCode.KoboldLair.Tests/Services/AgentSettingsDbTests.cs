using DraCode.KoboldLair.Agents.Tools;
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
/// Per-project agent settings live only in the project repository (TASK-094): the limit the UI sets
/// through ProjectService is the one the factories enforce, and Dragon/Warden manage_agents writes the
/// flag the pipeline reads.
/// </summary>
public class AgentSettingsDbTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlProjectRepository _repo = null!;
    private ProjectService _service = null!;
    private KoboldLairConfiguration _config = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_agentsettings_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        _repo = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await _repo.InitializeAsync();

        _config = new KoboldLairConfiguration { ProjectsPath = _dir };
        var providerConfig = new ProviderConfigurationService(
            NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config),
            Path.Combine(_dir, "user-settings.json"));
        var wyvernFactory = new WyvernFactory(providerConfig, _repo, _config);
        var gitService = new GitService(NullLogger<GitService>.Instance);

        _service = new ProjectService(_repo, wyvernFactory, NullLogger<ProjectService>.Instance, gitService, _config);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private string RegisterProject() =>
        _service.RegisterProject("p", Path.Combine(_dir, "p", "specification.md"), Guid.NewGuid().ToString()).Id;

    private KoboldFactory NewKoboldFactory() => new(_repo, NullLoggerFactory.Instance, _config);

    private static void AssignActiveKobold(KoboldFactory factory, string? projectId) =>
        factory.CreateKobold("ollama", "coding").AssignTask(Guid.NewGuid(), "task", projectId);

    [Fact]
    public async Task KoboldFactory_ShouldEnforce_LimitSetThroughProjectService()
    {
        var projectId = RegisterProject();
        await _service.SetMaxParallelKoboldsAsync(projectId, 2);
        var factory = NewKoboldFactory();

        AssignActiveKobold(factory, projectId);
        factory.CanCreateKoboldForProject(projectId).Should().BeTrue("1 of 2 slots is used");

        AssignActiveKobold(factory, projectId);
        factory.CanCreateKoboldForProject(projectId).Should().BeFalse("2 of 2 slots are used");

        await _service.SetMaxParallelKoboldsAsync(projectId, 3);
        factory.CanCreateKoboldForProject(projectId).Should().BeTrue("the factory reads the stored limit live");
    }

    [Fact]
    public void KoboldFactory_ShouldFallBackToGlobalLimit_ForUnknownProject()
    {
        _config.Limits.MaxParallelKobolds = 2;
        var factory = NewKoboldFactory();
        const string unknownProject = "no-such-project";

        AssignActiveKobold(factory, unknownProject);
        factory.CanCreateKoboldForProject(unknownProject).Should().BeTrue();

        AssignActiveKobold(factory, unknownProject);
        factory.CanCreateKoboldForProject(unknownProject).Should().BeFalse();
    }

    [Fact]
    public void WyrmFactory_ShouldEnforce_StoredLimit_OverGlobalDefault()
    {
        _config.Limits.MaxParallelWyrms = 3;
        var projectId = RegisterProject();
        var providerConfig = new ProviderConfigurationService(
            NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config),
            Path.Combine(_dir, "user-settings.json"));
        var factory = new WyrmFactory(_repo, providerConfig);

        factory.RegisterWyrm(projectId);

        factory.CanCreateWyrmForProject(projectId).Should().BeFalse("the project's stored wyrm limit (1) wins over the global default (3)");
    }

    private AgentConfigurationTool NewManageAgentsTool(Func<List<Project>>? visibleProjects = null)
    {
        visibleProjects ??= _repo.GetAll;
        return new(
            idOrName => AgentConfigurationTool.Resolve(visibleProjects(), idOrName),
            () => visibleProjects().Select(p => (p.Id, p.Name)).ToList(),
            _repo.SetAgentEnabledAsync,
            _repo.SetAgentLimitAsync);
    }

    private static Task<string> RunAsync(AgentConfigurationTool tool, string action, string project, string? agentType = null, int? limit = null)
    {
        var input = new Dictionary<string, object> { ["action"] = action, ["project"] = project };
        if (agentType != null) input["agent_type"] = agentType;
        if (limit != null) input["limit"] = limit.Value;
        return tool.ExecuteAsync(".", input);
    }

    [Fact]
    public async Task ManageAgents_DisableEnable_ShouldChange_IsAgentEnabled()
    {
        var projectId = RegisterProject();
        var tool = NewManageAgentsTool();
        _service.IsAgentEnabled(projectId, "wyrm").Should().BeTrue();

        (await RunAsync(tool, "disable", "p", "wyrm")).Should().StartWith("✅");
        _service.IsAgentEnabled(projectId, "wyrm").Should().BeFalse();
        (await RunAsync(tool, "get", "p")).Should().Contain("| Wyrm    | ❌ Disabled |");

        (await RunAsync(tool, "enable", "p", "wyrm")).Should().StartWith("✅");
        _service.IsAgentEnabled(projectId, "wyrm").Should().BeTrue();
    }

    [Fact]
    public async Task ManageAgents_SetLimit_ShouldBe_TheLimitProjectServiceReports()
    {
        var projectId = RegisterProject();

        (await RunAsync(NewManageAgentsTool(), "set_limit", projectId, "kobold", 4)).Should().StartWith("✅");

        _service.GetMaxParallelKobolds(projectId).Should().Be(4);
    }

    [Fact]
    public async Task ManageAgents_ShouldNotChange_AProjectOutsideTheCallersVisibleProjects()
    {
        var otherOwnersProject = RegisterProject();
        var tool = NewManageAgentsTool(() => new List<Project>());

        (await RunAsync(tool, "disable", otherOwnersProject, "wyrm")).Should().Contain("not found");
        (await RunAsync(tool, "disable", "p", "wyrm")).Should().Contain("not found");

        _service.IsAgentEnabled(otherOwnersProject, "wyrm").Should().BeTrue();
    }
}
