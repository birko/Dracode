using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The per-project Drake switch (TASK-095): the background Drake cycle skips a project whose
/// <c>drake.enabled</c> is off and picks it up again once it is switched back on.
/// </summary>
public class DrakeExecutionSelectionTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlProjectRepository _repo = null!;
    private ProjectService _service = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_drakeselect_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        _repo = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await _repo.InitializeAsync();

        var config = new KoboldLairConfiguration { ProjectsPath = _dir };
        var providerConfig = new ProviderConfigurationService(
            NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(config),
            Path.Combine(_dir, "user-settings.json"));
        var wyvernFactory = new WyvernFactory(providerConfig, _repo, config);
        var gitService = new GitService(NullLogger<GitService>.Instance);

        _service = new ProjectService(_repo, wyvernFactory, NullLogger<ProjectService>.Instance, gitService, config);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private async Task<Project> AnalyzedProject(string name, ProjectExecutionState state = ProjectExecutionState.Running)
    {
        var project = _service.RegisterProject(name, Path.Combine(_dir, name, "specification.md"), Guid.NewGuid().ToString());
        project.Status = ProjectStatus.Analyzed;
        project.ExecutionState = state;
        await _repo.UpdateAsync(project);
        return project;
    }

    private DrakeCycleSelection Select() =>
        DrakeExecutionService.SelectProjects(_service.GetAllProjects(), id => _service.IsAgentEnabled(id, "drake"));

    [Fact]
    public async Task A_project_with_Drake_off_is_skipped_and_one_with_Drake_on_is_processed()
    {
        var on = await AnalyzedProject("on");
        var off = await AnalyzedProject("off");
        await _repo.SetAgentEnabledAsync(off.Id, "drake", false);

        var selection = Select();

        selection.ToProcess.Select(p => p.Id).Should().Equal(on.Id);
        selection.DrakeDisabled.Select(p => p.Id).Should().Equal(off.Id);
    }

    [Fact]
    public async Task A_paused_project_counts_as_paused_even_with_Drake_off()
    {
        var paused = await AnalyzedProject("paused", ProjectExecutionState.Paused);
        await _repo.SetAgentEnabledAsync(paused.Id, "drake", false);

        var selection = Select();

        selection.Paused.Select(p => p.Id).Should().Equal(paused.Id);
        selection.DrakeDisabled.Should().BeEmpty();
        selection.ToProcess.Should().BeEmpty();
    }

    [Fact]
    public async Task Switching_Drake_back_on_through_manage_agents_returns_the_project_to_the_next_cycle()
    {
        var project = await AnalyzedProject("toggle");
        var tool = new AgentConfigurationTool(
            idOrName => AgentConfigurationTool.Resolve(_repo.GetAll(), idOrName),
            () => _repo.GetAll().Select(p => (p.Id, p.Name)).ToList(),
            _repo.SetAgentEnabledAsync,
            _repo.SetAgentLimitAsync);
        var input = (string action) => new Dictionary<string, object>
        {
            ["action"] = action, ["project"] = "toggle", ["agent_type"] = "drake"
        };

        await tool.ExecuteAsync(".", input("disable"));
        Select().DrakeDisabled.Select(p => p.Id).Should().Equal(project.Id);

        await tool.ExecuteAsync(".", input("enable"));
        Select().ToProcess.Select(p => p.Id).Should().Equal(project.Id);
    }

    [Fact]
    public async Task A_project_whose_flag_cannot_be_read_is_skipped_rather_than_failing_the_cycle()
    {
        var project = await AnalyzedProject("gone");

        var selection = DrakeExecutionService.SelectProjects(
            _service.GetAllProjects(), _ => throw new InvalidOperationException("Project not found"));

        selection.ToProcess.Should().BeEmpty();
        selection.DrakeDisabled.Select(p => p.Id).Should().Equal(project.Id);
    }
}
