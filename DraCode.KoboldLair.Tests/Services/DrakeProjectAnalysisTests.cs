using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// A Drake the factory creates has its project's Wyvern, and with it the saved analysis and Wyrm's recommendation
/// (TASK-225, FIELD-044): nothing called <c>Drake.SetWyvern</c>, so Kobolds never got the project's constraints and
/// everything else that reads the Wyvern through Drake saw none. Getting the Wyvern never changes the project's status.
/// </summary>
public class DrakeProjectAnalysisTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlProjectRepository _repo = null!;
    private KoboldLairConfiguration _config = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_drake_analysis_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _repo = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await _repo.InitializeAsync();
        _config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir,
            DefaultProvider = "ollama",
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = "ollama", Type = "ollama", DefaultModel = "m1",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" }, Configuration = new()
                }
            }
        };
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_drake_the_factory_creates_gives_a_summoned_kobold_the_projects_constraints()
    {
        var providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config), Path.Combine(_dir, "user-settings.json"));
        var kobolds = new KoboldFactory(_repo, NullLoggerFactory.Instance, _config);
        var drakes = new DrakeFactory(kobolds, providers, _config, NullLoggerFactory.Instance, projectRepository: _repo);
        var projects = new ProjectService(_repo, new WyvernFactory(providers, _repo, _config), NullLogger<ProjectService>.Instance,
            new GitService(NullLogger<GitService>.Instance), _config, drakes);

        // An analyzed project as Wyvern and Wyrm leave it on disk
        var folder = Path.Combine(_dir, "greet");
        Directory.CreateDirectory(Path.Combine(folder, "tasks"));
        var spec = Path.Combine(folder, "specification.md");
        File.WriteAllText(spec, "# greet");
        File.WriteAllText(Path.Combine(folder, "analysis.json"),
            "{\"projectName\":\"greet\",\"areas\":[{\"name\":\"cli\",\"tasks\":[{\"id\":\"cli-1\",\"name\":\"greet\",\"description\":\"Write greet.py\",\"agentType\":\"python\"}]}]," +
            "\"constraints\":[\"Only one script\"],\"outOfScope\":[\"A GUI\"]}");
        File.WriteAllText(Path.Combine(folder, "wyrm-recommendation.json"), "{\"constraints\":[\"Python 3 only\"],\"outOfScope\":[]}");
        var project = projects.RegisterProject("greet", spec, Guid.NewGuid().ToString());
        project.Paths.Output = folder;
        project.Status = ProjectStatus.InProgress;
        _repo.Update(project);
        var taskFile = Path.Combine(folder, "tasks", "cli-tasks.md");
        var tracker = new TaskTracker();
        tracker.AddTask("[cli-1] Write greet.py");
        tracker.SaveToFile(taskFile);

        var drake = drakes.CreateDrake(taskFile, "greet:cli", specificationPath: spec, projectId: project.Id);
        await drake.ReloadTasksFromFileAsync();
        var task = drake.GetUnassignedTasks().Single().Task;
        var kobold = await drake.SummonKoboldAsync(task, "python");

        kobold!.ProjectConstraints.Should().Contain("Only one script").And.Contain("OUT OF SCOPE: A GUI")
            .And.Contain("Python 3 only");
        _repo.GetById(project.Id)!.Status.Should().Be(ProjectStatus.InProgress, "getting the Wyvern changes nothing about the project");
    }
}
