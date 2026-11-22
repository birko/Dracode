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
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// A priority change lands where Drake works from (TASK-154), like a retry: its Drake's tracker and task file, or the
/// task file when no Drake holds it, with the task store following. Setting only the store row was overwritten by Drake.
/// </summary>
public class TaskPriorityTests : IAsyncLifetime
{
    private const string ProjectId = "proj-priority";
    private string _dir = null!;
    private SqlTaskRepository _tasks = null!;
    private DrakeFactory _drakes = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_priority_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_dir, "tasks"));
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _tasks = new SqlTaskRepository(db);
        await _tasks.InitializeAsync();

        // A provider must exist to build a Drake; nothing here sends a request to it
        var config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir,
            DefaultProvider = "ollama",
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = "ollama", Type = "ollama", DefaultModel = "m1",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" },
                    Configuration = new()
                }
            }
        };
        var providerConfig = new ProviderConfigurationService(
            NullLogger<ProviderConfigurationService>.Instance, Options.Create(config), Path.Combine(_dir, "user-settings.json"));
        _drakes = new DrakeFactory(new KoboldFactory(projects, NullLoggerFactory.Instance, config), providerConfig, config,
            NullLoggerFactory.Instance, taskRepository: _tasks, projectRepository: projects);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    /// <summary>A task file with one open Normal task and one Done task, and their store rows.</summary>
    private async Task<(string File, string OpenId, string DoneId)> SeedTaskFile()
    {
        var file = Path.Combine(_dir, "tasks", "backend-tasks.md");
        var tracker = new TaskTracker();
        var open = tracker.AddTask("Build the API", TaskPriority.Normal);
        var done = tracker.AddTask("Write the README", TaskPriority.Low);
        tracker.UpdateTask(done, TaskStatus.Done, "documentation");
        tracker.SaveToFile(file);
        foreach (var task in tracker.GetAllTasks())
            await _tasks.AddTaskAsync(ProjectId, "backend", new TaskRecord
            {
                Id = task.Id, Task = task.Task, ProjectId = ProjectId, Status = task.Status, Priority = task.Priority
            });
        return (file, open.Id, done.Id);
    }

    private static Project ProjectWith(string file) => new()
    {
        Id = ProjectId, Name = "priority",
        Paths = new ProjectPaths { TaskFiles = new Dictionary<string, string> { ["backend"] = file } }
    };

    private static TaskRecord FromFile(string file, string id)
    {
        var tracker = new TaskTracker();
        tracker.LoadFromFile(file);
        return tracker.GetTaskById(id)!;
    }

    [Fact]
    public async Task With_a_Drake_holding_it_the_priority_is_set_in_the_Drake_its_file_and_the_store()
    {
        var (file, openId, _) = await SeedTaskFile();
        var drake = _drakes.CreateDrake(file, "priority-backend", projectId: ProjectId);

        var change = await _drakes.SetTaskPriorityAsync(ProjectWith(file), openId, TaskPriority.Critical);

        change.Outcome.Should().Be(TaskPriorityOutcome.Set);
        change.Previous.Should().Be(TaskPriority.Normal);
        drake.GetAllTasks().Single(t => t.Id == openId).Priority.Should().Be(TaskPriority.Critical, "Drake's picker orders by its own tracker");
        FromFile(file, openId).Priority.Should().Be(TaskPriority.Critical, "a restart reloads the task file");
        (await _tasks.GetByIdAsync(openId))!.Priority.Should().Be(TaskPriority.Critical, "the board reads the store right after");
    }

    [Fact]
    public async Task With_no_Drake_the_priority_is_set_in_its_file_and_the_store_follows()
    {
        var (file, openId, _) = await SeedTaskFile();

        var change = await _drakes.SetTaskPriorityAsync(ProjectWith(file), openId, TaskPriority.High);

        change.Outcome.Should().Be(TaskPriorityOutcome.Set);
        FromFile(file, openId).Priority.Should().Be(TaskPriority.High);
        (await _tasks.GetByIdAsync(openId))!.Priority.Should().Be(TaskPriority.High);
        (await _tasks.GetByProjectAsync(ProjectId)).Should().HaveCount(2, "loading the file adds no rows");
    }

    [Fact]
    public async Task A_Done_task_is_left_alone_and_an_unknown_one_is_not_found()
    {
        var (file, _, doneId) = await SeedTaskFile();
        _drakes.CreateDrake(file, "priority-backend", projectId: ProjectId);

        var change = await _drakes.SetTaskPriorityAsync(ProjectWith(file), doneId, TaskPriority.Critical);
        change.Outcome.Should().Be(TaskPriorityOutcome.AlreadyDone);
        FromFile(file, doneId).Priority.Should().Be(TaskPriority.Low);

        (await _drakes.SetTaskPriorityAsync(ProjectWith(file), "no-such-task", TaskPriority.High)).Outcome.Should().Be(TaskPriorityOutcome.NotFound);
    }

    [Fact]
    public async Task A_task_known_only_to_the_store_is_set_there()
    {
        var id = await _tasks.AddTaskAsync(ProjectId, "backend",
            new TaskRecord { Task = "orphan", ProjectId = ProjectId, Status = TaskStatus.Unassigned, Priority = TaskPriority.Low });

        var change = await _drakes.SetTaskPriorityAsync(new Project { Id = ProjectId, Name = "priority" }, id, TaskPriority.Critical);

        change.Outcome.Should().Be(TaskPriorityOutcome.Set);
        (await _tasks.GetByIdAsync(id))!.Priority.Should().Be(TaskPriority.Critical);
    }
}
