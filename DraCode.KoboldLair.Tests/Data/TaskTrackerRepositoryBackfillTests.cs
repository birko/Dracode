using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// TASK-091: tasks that reach a <see cref="TaskTracker"/> from a file never pass through <c>AddTask</c>, so they
/// had no database row and the REST task endpoints could not see them. <see cref="TaskTracker.EnsureInRepositoryAsync"/>
/// adds the missing rows; Wyvern calls it after writing a task file and Drake after loading one.
/// </summary>
public class TaskTrackerRepositoryBackfillTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlTaskRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_backfill_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _repo = new SqlTaskRepository(Path.Combine(_dir, "tasks.db"));
        await _repo.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    /// <summary>A tracker holding two tasks that came from a task file, the way Drake loads them.</summary>
    private TaskTracker FileLoadedTracker(string projectId)
    {
        var writer = new TaskTracker();
        writer.AddTask("[script-1] Create hello.py");
        writer.AddTask("[script-2] Create greet.py", TaskPriority.High);
        var json = Path.Combine(_dir, "scripting-tasks.json");
        writer.SaveToJsonFile(json);

        var tracker = new TaskTracker { Repository = _repo, ProjectId = projectId, AreaName = "scripting" };
        tracker.LoadFromJsonFile(json);
        return tracker;
    }

    [Fact]
    public async Task File_loaded_tasks_get_database_rows_for_their_project()
    {
        var projectId = Guid.NewGuid().ToString();
        var tracker = FileLoadedTracker(projectId);

        (await _repo.GetByProjectAsync(projectId)).Should().BeEmpty("loading from a file writes nothing");

        (await tracker.EnsureInRepositoryAsync()).Should().Be(2);

        var rows = await _repo.GetByProjectAsync(projectId);
        rows.Select(r => r.Id).Should().BeEquivalentTo(tracker.GetAllTasks().Select(t => t.Id));
        (await _repo.GetByIdAsync(tracker.GetAllTasks()[0].Id)).Should().NotBeNull("the REST retry endpoint looks tasks up by id");
    }

    [Fact]
    public async Task Ensuring_twice_adds_nothing_the_second_time()
    {
        var projectId = Guid.NewGuid().ToString();
        var tracker = FileLoadedTracker(projectId);

        await tracker.EnsureInRepositoryAsync();
        (await tracker.EnsureInRepositoryAsync()).Should().Be(0);
        (await _repo.CountByProjectAsync(projectId)).Should().Be(2);
    }

    [Fact]
    public async Task An_update_from_a_record_without_a_project_id_keeps_the_stored_one()
    {
        var projectId = Guid.NewGuid().ToString();
        var tracker = FileLoadedTracker(projectId);
        await tracker.EnsureInRepositoryAsync();

        // The record Drake/Kobold later update was loaded from the file, so it carries no project id.
        var fromFile = tracker.GetAllTasks()[0];
        var update = new TaskRecord { Id = fromFile.Id, Task = fromFile.Task, Status = Models.Tasks.TaskStatus.Done, ProjectId = null };
        await _repo.UpdateTaskAsync(update);

        (await _repo.GetByProjectAsync(projectId)).Should().HaveCount(2, "the update must not detach the task from its project");
        (await _repo.GetByIdAsync(fromFile.Id))!.Status.Should().Be(Models.Tasks.TaskStatus.Done);
    }

    [Fact]
    public async Task Without_a_repository_it_does_nothing()
    {
        var tracker = new TaskTracker();
        tracker.AddTask("[x-1] Something");
        (await tracker.EnsureInRepositoryAsync()).Should().Be(-1);
    }
}
