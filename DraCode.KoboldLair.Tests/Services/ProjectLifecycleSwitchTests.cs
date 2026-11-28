using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The lifecycle layer is switched on per project (TASK-211, FEATURE-078 D5): a project Dragon creates starts with it on
/// unless the person said no when asked; an imported project and one created before the switch existed start off; Dragon
/// (Warden's tool) and the web UI turn it on and off. Each Drake reads it live.
/// </summary>
public class ProjectLifecycleSwitchTests : IAsyncLifetime
{
    private string _dir = null!;
    private string _db = null!;
    private SqlProjectRepository _projects = null!;
    private KoboldLairConfiguration _config = null!;
    private ProjectService _service = null!;
    private DrakeFactory _drakes = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_lifecycle_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "koboldlair.db");
        _projects = new SqlProjectRepository(_db);
        await _projects.InitializeAsync();
        _config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir, DefaultProvider = "ollama",
            Providers = new() { new ProviderConfig { Name = "ollama", Type = "ollama", DefaultModel = "m1", IsEnabled = true,
                RequiresApiKey = false, CompatibleAgents = new() { "all" }, Configuration = new() } }
        };
        var providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config), Path.Combine(_dir, "user-settings.json"));
        _drakes = new DrakeFactory(new KoboldFactory(_projects, NullLoggerFactory.Instance, _config), providers, _config,
            NullLoggerFactory.Instance, projectRepository: _projects);
        _service = new ProjectService(_projects, new WyvernFactory(providers, _projects, _config), NullLogger<ProjectService>.Instance,
            new GitService(NullLogger<GitService>.Instance), _config, _drakes);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private Project Register(string name, bool? lifecycle = null)
    {
        var folder = Path.Combine(_dir, name);
        Directory.CreateDirectory(folder);
        var spec = Path.Combine(folder, "specification.md");
        File.WriteAllText(spec, $"# {name}");
        return lifecycle is { } on ? _service.RegisterProject(name, spec, "owner", lifecycleEnabled: on) : _service.RegisterProject(name, spec, "owner");
    }

    [Fact]
    public async Task A_new_project_starts_on_unless_asked_not_to_and_the_choice_survives_a_reload()
    {
        var on = Register("menu");
        var off = Register("notes", lifecycle: false);

        on.LifecycleEnabled.Should().BeTrue();
        off.LifecycleEnabled.Should().BeFalse();
        var reloaded = new SqlProjectRepository(_db);
        await reloaded.InitializeAsync();
        reloaded.GetById(on.Id)!.LifecycleEnabled.Should().BeTrue();
        reloaded.GetById(off.Id)!.LifecycleEnabled.Should().BeFalse();
    }

    [Fact]
    public void An_imported_codebase_starts_off()
    {
        var source = Path.Combine(_dir, "existing-app");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "app.py"), "print('hi')\n");

        var id = _service.RegisterExistingProject("existing-app", source, "owner");

        _projects.GetById(id!)!.LifecycleEnabled.Should().BeFalse("an imported project is changed only once adopted (D5)");
    }

    [Fact]
    public async Task A_project_created_before_the_switch_existed_is_off()
    {
        var project = Register("legacy");
        SqliteConnection.ClearAllPools();
        using (var connection = new SqliteConnection($"Data Source={_db};Pooling=False"))
        {
            connection.Open();
            using var drop = connection.CreateCommand();
            drop.CommandText = "ALTER TABLE projects DROP COLUMN LifecycleEnabled";
            drop.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        var reopened = new SqlProjectRepository(_db);
        await reopened.InitializeAsync();

        reopened.GetById(project.Id)!.LifecycleEnabled.Should().BeFalse("an older table gains the column, off for every row");
    }

    [Fact]
    public async Task The_switch_turns_on_and_off_and_a_Drake_reads_it_live()
    {
        var project = Register("shop", lifecycle: false);
        var taskFile = Path.Combine(_dir, "shop", "tasks", "api-tasks.md");
        Directory.CreateDirectory(Path.GetDirectoryName(taskFile)!);
        var drake = _drakes.CreateDrake(taskFile, "api", projectId: project.Id);

        drake.LifecycleEnabled!().Should().BeFalse();
        (await _service.SetLifecycleEnabledAsync(project.Id, true)).Should().BeTrue();
        drake.LifecycleEnabled!().Should().BeTrue("the Drake asks the project each time");
        _projects.GetById(project.Id)!.LifecycleEnabled.Should().BeTrue();
        (await _service.SetLifecycleEnabledAsync(project.Id, false)).Should().BeTrue();
        drake.LifecycleEnabled!().Should().BeFalse();
        (await _service.SetLifecycleEnabledAsync("no-such-project", true)).Should().BeFalse();
    }

    [Fact]
    public async Task Dragons_specification_tool_records_the_persons_answer_for_the_new_project()
    {
        var tool = new SpecificationManagementTool(new Dictionary<string, Specification>(), projectsPath: _dir);

        await tool.ExecuteAsync(_dir, new Dictionary<string, object>
            { ["action"] = "create", ["name"] = "without", ["content"] = "# Without", ["lifecycle_layer"] = false });
        await tool.ExecuteAsync(_dir, new Dictionary<string, object>
            { ["action"] = "create", ["name"] = "with", ["content"] = "# With", ["lifecycle_layer"] = "true" });
        var unanswered = await tool.ExecuteAsync(_dir, new Dictionary<string, object>
            { ["action"] = "create", ["name"] = "unanswered", ["content"] = "# Unanswered" });

        (await SpecificationService.ReadLifecycleChoiceAsync(Path.Combine(_dir, "without"))).Should().BeFalse();
        (await SpecificationService.ReadLifecycleChoiceAsync(Path.Combine(_dir, "with"))).Should().BeTrue();
        unanswered.Should().StartWith("Error: lifecycle_layer is required").And.Contain("Ask them");
        File.Exists(Path.Combine(_dir, "unanswered", "specification.md")).Should().BeFalse("no answer, no specification");
    }

    [Fact]
    public async Task Wardens_tool_shows_and_switches_the_layer_for_the_persons_own_projects()
    {
        var mine = Register("mine", lifecycle: false);
        var theirs = Register("theirs");
        var tool = new LifecycleLayerTool(
            nameOrId => _service.FindProjectByName(nameOrId) ?? _service.GetProject(nameOrId),
            (id, on) => _service.SetLifecycleEnabledAsync(id, on),
            () => [(mine.Id, mine.Name)]);

        (await tool.ExecuteAsync(_dir, new() { ["action"] = "status", ["project"] = "mine" })).Should().Contain("off");
        (await tool.ExecuteAsync(_dir, new() { ["action"] = "on", ["project"] = "mine" })).Should().Contain("on");
        _projects.GetById(mine.Id)!.LifecycleEnabled.Should().BeTrue();
        (await tool.ExecuteAsync(_dir, new() { ["action"] = "off", ["project"] = "theirs" })).Should().Contain("not found");
        _projects.GetById(theirs.Id)!.LifecycleEnabled.Should().BeTrue("another person's project is out of reach");
    }
}
