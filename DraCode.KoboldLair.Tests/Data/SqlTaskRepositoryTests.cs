using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Integration tests for SqlTaskRepository using real SQLite database.
/// </summary>
public class SqlTaskRepositoryTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private SqlTaskRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_test_{Guid.NewGuid():N}.db");
        _repo = new SqlTaskRepository(_dbPath);
        await _repo.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    private static TaskRecord CreateTask(string desc = "Test task", TaskStatus status = TaskStatus.Unassigned)
    {
        return new TaskRecord
        {
            Id = Guid.NewGuid().ToString(),
            Task = desc,
            AssignedAgent = "csharp",
            Status = status,
            Priority = TaskPriority.Normal,
            Dependencies = new List<string>(),
            OutputFiles = new List<string>()
        };
    }

    [Fact]
    public async Task AddAndGetById_ShouldRoundTrip()
    {
        var task = CreateTask("Implement auth module");
        await _repo.AddTaskAsync("proj-1", "backend", task);

        var loaded = await _repo.GetByIdAsync(task.Id);
        loaded.Should().NotBeNull();
        loaded!.Task.Should().Be("Implement auth module");
        loaded.AssignedAgent.Should().Be("csharp");
        loaded.ProjectId.Should().Be("proj-1");
    }

    [Fact]
    public async Task UpdateTask_ShouldPersistChanges()
    {
        var task = CreateTask();
        await _repo.AddTaskAsync("proj-1", "backend", task);

        task.Status = TaskStatus.Working;
        task.AssignedAgent = "typescript";
        task.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateTaskAsync(task);

        var loaded = await _repo.GetByIdAsync(task.Id);
        loaded!.Status.Should().Be(TaskStatus.Working);
        loaded.AssignedAgent.Should().Be("typescript");
    }

    [Fact]
    public async Task DeleteTask_ShouldRemove()
    {
        var task = CreateTask();
        await _repo.AddTaskAsync("proj-1", "backend", task);

        await _repo.DeleteTaskAsync(task.Id);

        var loaded = await _repo.GetByIdAsync(task.Id);
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task GetByProject_ShouldFilterByProjectId()
    {
        await _repo.AddTaskAsync("proj-1", "backend", CreateTask("t1"));
        await _repo.AddTaskAsync("proj-1", "frontend", CreateTask("t2"));
        await _repo.AddTaskAsync("proj-2", "backend", CreateTask("t3"));

        var proj1Tasks = await _repo.GetByProjectAsync("proj-1");
        proj1Tasks.Should().HaveCount(2);

        var proj2Tasks = await _repo.GetByProjectAsync("proj-2");
        proj2Tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByProjectAndArea_ShouldFilterBoth()
    {
        await _repo.AddTaskAsync("proj-1", "backend", CreateTask("t1"));
        await _repo.AddTaskAsync("proj-1", "frontend", CreateTask("t2"));
        await _repo.AddTaskAsync("proj-1", "frontend", CreateTask("t3"));

        var frontendTasks = await _repo.GetByProjectAndAreaAsync("proj-1", "frontend");
        frontendTasks.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByStatus_ShouldFilter()
    {
        await _repo.AddTaskAsync("proj-1", "backend", CreateTask("t1", TaskStatus.Unassigned));
        await _repo.AddTaskAsync("proj-1", "backend", CreateTask("t2", TaskStatus.Working));
        await _repo.AddTaskAsync("proj-1", "backend", CreateTask("t3", TaskStatus.Done));

        var working = await _repo.GetByStatusAsync(TaskStatus.Working);
        working.Should().HaveCount(1);
        working[0].Task.Should().Be("t2");
    }

    [Fact]
    public async Task SetError_ShouldMarkFailed()
    {
        var task = CreateTask();
        await _repo.AddTaskAsync("proj-1", "backend", task);

        await _repo.SetErrorAsync(task.Id, "Network timeout", "Transient");

        var loaded = await _repo.GetByIdAsync(task.Id);
        loaded!.Status.Should().Be(TaskStatus.Failed);
        loaded.ErrorMessage.Should().Be("Network timeout");
        loaded.ErrorCategory.Should().Be("Transient");
    }

    [Fact]
    public async Task ClearError_ShouldResetErrorFields()
    {
        var task = CreateTask();
        await _repo.AddTaskAsync("proj-1", "backend", task);
        await _repo.SetErrorAsync(task.Id, "error", "Transient");

        await _repo.ClearErrorAsync(task.Id);

        var loaded = await _repo.GetByIdAsync(task.Id);
        loaded!.ErrorMessage.Should().BeNull();
        loaded.ErrorCategory.Should().BeNull();
        loaded.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task Dependencies_ShouldRoundTrip()
    {
        var task = CreateTask();
        task.Dependencies = new List<string> { "backend-1", "backend-2" };
        task.OutputFiles = new List<string> { "src/auth.cs", "src/auth.test.cs" };
        await _repo.AddTaskAsync("proj-1", "backend", task);

        var loaded = await _repo.GetByIdAsync(task.Id);
        loaded!.Dependencies.Should().BeEquivalentTo(new[] { "backend-1", "backend-2" });
        loaded.OutputFiles.Should().BeEquivalentTo(new[] { "src/auth.cs", "src/auth.test.cs" });
    }

    [Fact]
    public async Task CountByProject_ShouldBeAccurate()
    {
        await _repo.AddTaskAsync("proj-1", "a", CreateTask());
        await _repo.AddTaskAsync("proj-1", "b", CreateTask());
        await _repo.AddTaskAsync("proj-2", "a", CreateTask());

        var count = await _repo.CountByProjectAsync("proj-1");
        count.Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentUpdates_ShouldNotCorrupt()
    {
        var tasks = new List<TaskRecord>();
        for (int i = 0; i < 10; i++)
        {
            var t = CreateTask($"task-{i}");
            await _repo.AddTaskAsync("proj-1", "backend", t);
            tasks.Add(t);
        }

        // Concurrently update all tasks
        var updates = tasks.Select(t =>
        {
            t.Status = TaskStatus.Done;
            t.UpdatedAt = DateTime.UtcNow;
            return _repo.UpdateTaskAsync(t);
        });

        await Task.WhenAll(updates);

        var allDone = await _repo.GetByProjectAndStatusAsync("proj-1", TaskStatus.Done);
        allDone.Should().HaveCount(10);
    }
}
