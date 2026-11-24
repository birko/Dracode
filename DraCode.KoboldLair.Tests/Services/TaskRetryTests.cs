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
/// A retry puts a failed task back where Drake works from (TASK-153): its Drake's tracker and task file, or the task file
/// when no Drake holds it, with the task store following. Resetting only the store row left the task Failed for Drake.
/// </summary>
public class TaskRetryTests : IAsyncLifetime
{
    private const string ProjectId = "proj-retry";
    private string _dir = null!;
    private SqlTaskRepository _tasks = null!;
    private DrakeFactory _drakes = null!;
    private SqlProjectRepository _projects = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_retry_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_dir, "tasks"));
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _projects = projects;
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

    /// <summary>A task file with one Failed and one Done task, and their store rows.</summary>
    private async Task<(string File, string FailedId, string DoneId)> SeedTaskFile()
    {
        var file = Path.Combine(_dir, "tasks", "backend-tasks.md");
        var tracker = new TaskTracker();
        var failed = tracker.AddTask("Build the API");
        tracker.UpdateTask(failed, TaskStatus.Failed, "csharp");
        tracker.SetError(failed, "compile error");
        var done = tracker.AddTask("Write the README");
        tracker.UpdateTask(done, TaskStatus.Done, "documentation");
        tracker.SaveToFile(file);
        foreach (var task in tracker.GetAllTasks())
            await _tasks.AddTaskAsync(ProjectId, "backend", new TaskRecord
            {
                Id = task.Id, Task = task.Task, ProjectId = ProjectId, Status = task.Status, ErrorMessage = task.ErrorMessage
            });
        return (file, failed.Id, done.Id);
    }

    private static Project ProjectWith(string file) => new()
    {
        Id = ProjectId, Name = "retry",
        Paths = new ProjectPaths { TaskFiles = new Dictionary<string, string> { ["backend"] = file } }
    };

    private static TaskRecord FromFile(string file, string id)
    {
        var tracker = new TaskTracker();
        tracker.LoadFromFile(file);
        return tracker.GetTaskById(id)!;
    }

    [Fact]
    public async Task With_a_Drake_holding_it_the_task_is_reset_in_the_Drake_and_its_file()
    {
        var (file, failedId, _) = await SeedTaskFile();
        var drake = _drakes.CreateDrake(file, "retry-backend", projectId: ProjectId);

        var (outcome, _) = await _drakes.RetryFailedTaskAsync(ProjectWith(file), failedId);

        outcome.Should().Be(TaskRetryOutcome.Retried);
        var live = drake.GetAllTasks().Single(t => t.Id == failedId);
        live.Status.Should().Be(TaskStatus.Unassigned, "Drake picks up what its own tracker says");
        live.ErrorMessage.Should().BeNullOrEmpty();
        FromFile(file, failedId).Status.Should().Be(TaskStatus.Unassigned, "a restart reloads the task file");
    }

    [Fact]
    public async Task With_no_Drake_the_task_is_reset_in_its_file_and_the_store_follows()
    {
        var (file, failedId, _) = await SeedTaskFile();

        var (outcome, _) = await _drakes.RetryFailedTaskAsync(ProjectWith(file), failedId);

        outcome.Should().Be(TaskRetryOutcome.Retried);
        var fromFile = FromFile(file, failedId);
        fromFile.Status.Should().Be(TaskStatus.Unassigned);
        fromFile.ErrorMessage.Should().BeNullOrEmpty();
        var row = await _tasks.GetByIdAsync(failedId);
        row!.Status.Should().Be(TaskStatus.Unassigned);
        row.ErrorMessage.Should().BeNullOrEmpty();
        (await _tasks.GetByProjectAsync(ProjectId)).Should().HaveCount(2, "loading the file adds no rows");
    }

    [Fact]
    public async Task A_task_that_has_not_failed_is_left_alone_and_an_unknown_one_is_not_found()
    {
        var (file, _, doneId) = await SeedTaskFile();
        _drakes.CreateDrake(file, "retry-backend", projectId: ProjectId);

        var (outcome, status) = await _drakes.RetryFailedTaskAsync(ProjectWith(file), doneId);
        outcome.Should().Be(TaskRetryOutcome.NotFailed);
        status.Should().Be(TaskStatus.Done);

        (await _drakes.RetryFailedTaskAsync(ProjectWith(file), "no-such-task")).Outcome.Should().Be(TaskRetryOutcome.NotFound);
    }

    [Fact]
    public async Task A_task_known_only_to_the_store_is_reset_there()
    {
        var id = await _tasks.AddTaskAsync(ProjectId, "backend",
            new TaskRecord { Task = "orphan", ProjectId = ProjectId, Status = TaskStatus.Failed, ErrorMessage = "boom" });

        var (outcome, _) = await _drakes.RetryFailedTaskAsync(new Project { Id = ProjectId, Name = "retry" }, id);

        outcome.Should().Be(TaskRetryOutcome.Retried);
        (await _tasks.GetByIdAsync(id))!.Status.Should().Be(TaskStatus.Unassigned);
    }

    // --- a retry in a project already past InProgress (TASK-196) ----------------------------------

    private Project StoredProject(string file, ProjectStatus status)
    {
        var project = ProjectWith(file);
        project.OwnerId = "o";
        project.Status = status;
        _projects.Add(project);
        return project;
    }

    [Theory]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.AwaitingVerification)]
    [InlineData(ProjectStatus.Verified)]
    [InlineData(ProjectStatus.Failed)]
    public async Task A_retry_in_a_project_past_InProgress_reopens_the_project_so_Drake_runs_the_task(ProjectStatus status)
    {
        var (file, failedId, _) = await SeedTaskFile();
        var project = StoredProject(file, status);

        var retry = await _drakes.RetryFailedTaskAsync(project, failedId);

        retry.Outcome.Should().Be(TaskRetryOutcome.Retried);
        retry.ReopenedFrom.Should().Be(status, "the caller tells the person the project was reopened");
        _projects.GetById(ProjectId)!.Status.Should().Be(ProjectStatus.InProgress, "Drake only runs InProgress projects' tasks");
    }

    [Fact]
    public async Task A_retry_in_a_project_still_in_progress_leaves_the_project_alone()
    {
        var (file, failedId, _) = await SeedTaskFile();
        var project = StoredProject(file, ProjectStatus.InProgress);

        var retry = await _drakes.RetryFailedTaskAsync(project, failedId);

        retry.ReopenedFrom.Should().BeNull();
        _projects.GetById(ProjectId)!.Status.Should().Be(ProjectStatus.InProgress);
    }

    [Fact]
    public async Task A_retry_that_changes_nothing_does_not_reopen_the_project()
    {
        var (file, _, doneId) = await SeedTaskFile();
        var project = StoredProject(file, ProjectStatus.Completed);

        var retry = await _drakes.RetryFailedTaskAsync(project, doneId);

        retry.Outcome.Should().Be(TaskRetryOutcome.NotFailed);
        retry.ReopenedFrom.Should().BeNull();
        _projects.GetById(ProjectId)!.Status.Should().Be(ProjectStatus.Completed);
    }
}
