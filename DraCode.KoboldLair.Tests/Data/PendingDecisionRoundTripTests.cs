using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// A parked task and its pending decision survive a server restart (TASK-019): written through one set of
/// repositories, read back through a fresh set on the same database file.
/// </summary>
public class PendingDecisionRoundTripTests : IAsyncLifetime
{
    private string _dbPath = null!;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_pending_{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    private static PendingDecision Decision(string taskId, TimeSpan? timeout, DateTime createdAt) => new()
    {
        ProjectId = "proj-1",
        TaskId = taskId,
        Disposition = EscalationDisposition.HumanDecisionRequired,
        Source = PendingDecisionSource.Escalation,
        Question = "{\"text\":\"Split the task?\"}",
        Escalation = new EscalationAlert
        {
            ProjectId = "proj-1",
            TaskId = taskId,
            AgentType = "csharp",
            Type = EscalationType.NeedsSplit,
            Summary = "Too big for one plan"
        },
        CreatedAt = createdAt,
        Timeout = timeout
    };

    [Fact]
    public async Task A_parked_task_and_its_pending_decision_survive_a_restart()
    {
        var created = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var task = new TaskRecord { Id = Guid.NewGuid().ToString(), Task = "Big task", Status = TaskStatus.AwaitingHumanDecision };
        var decision = Decision(task.Id, TimeSpan.FromMinutes(30), created);

        var tasks = new SqlTaskRepository(_dbPath);
        await tasks.InitializeAsync();
        await tasks.AddTaskAsync("proj-1", "backend", task);
        var decisions = new SqlPendingDecisionRepository(_dbPath);
        await decisions.InitializeAsync();
        await decisions.CreateAsync(decision);

        // Restart: drop every pooled connection and build fresh repositories on the same file
        SqliteConnection.ClearAllPools();
        var tasksAfter = new SqlTaskRepository(_dbPath);
        await tasksAfter.InitializeAsync();
        var decisionsAfter = new SqlPendingDecisionRepository(_dbPath);
        await decisionsAfter.InitializeAsync();

        (await tasksAfter.GetByIdAsync(task.Id))!.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        var loaded = (await decisionsAfter.GetUnresolvedForTaskAsync(task.Id))!;
        loaded.Should().NotBeNull();
        loaded.Id.Should().Be(decision.Id);
        loaded.ProjectId.Should().Be("proj-1");
        loaded.Disposition.Should().Be(EscalationDisposition.HumanDecisionRequired);
        loaded.Source.Should().Be(PendingDecisionSource.Escalation);
        loaded.Question.Should().Be(decision.Question);
        loaded.Escalation!.Type.Should().Be(EscalationType.NeedsSplit);
        loaded.Escalation.Summary.Should().Be("Too big for one plan");
        loaded.CreatedAt.Should().Be(created);
        loaded.Timeout.Should().Be(TimeSpan.FromMinutes(30));
        loaded.TimeoutAt.Should().Be(created.AddMinutes(30));
        loaded.Resolution.Should().BeNull();
        loaded.ResolutionKind.Should().BeNull();
    }

    [Fact]
    public async Task The_table_is_added_to_a_database_created_before_it_existed()
    {
        var tasks = new SqlTaskRepository(_dbPath);
        await tasks.InitializeAsync();
        SqliteConnection.ClearAllPools();

        var decisions = new SqlPendingDecisionRepository(_dbPath);
        await decisions.InitializeAsync();
        await decisions.CreateAsync(Decision("t-1", null, DateTime.UtcNow));

        (await decisions.GetUnresolvedForProjectAsync("proj-1")).Should().ContainSingle();
    }

    [Fact]
    public async Task Only_expired_decisions_with_a_timeout_are_returned_and_resolving_is_once_only()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var decisions = new SqlPendingDecisionRepository(_dbPath);
        await decisions.InitializeAsync();
        var expired = Decision("t-expired", TimeSpan.FromMinutes(10), now.AddHours(-1));
        var running = Decision("t-running", TimeSpan.FromHours(5), now.AddHours(-1));
        var forever = Decision("t-forever", null, now.AddYears(-1));
        foreach (var d in new[] { expired, running, forever })
            await decisions.CreateAsync(d);

        (await decisions.GetExpiredAsync(now)).Select(d => d.TaskId).Should().BeEquivalentTo(new[] { "t-expired" });
        (await decisions.GetExpiredAsync(now.AddYears(1))).Select(d => d.TaskId)
            .Should().BeEquivalentTo(new[] { "t-expired", "t-running" }, "a decision with no timeout never expires");

        (await decisions.ResolveAsync(expired.Id, PendingDecisionResolution.TimedOut, null, now)).Should().BeTrue();
        (await decisions.ResolveAsync(expired.Id, PendingDecisionResolution.Answered, "late", now)).Should().BeFalse();
        (await decisions.GetUnresolvedForTaskAsync("t-expired")).Should().BeNull();
        (await decisions.GetExpiredAsync(now)).Should().BeEmpty();
    }

    [Fact]
    public async Task Resolving_concurrently_succeeds_exactly_once()
    {
        var decisions = new SqlPendingDecisionRepository(_dbPath);
        await decisions.InitializeAsync();
        var other = new SqlPendingDecisionRepository(_dbPath);
        var decision = Decision("t-race", TimeSpan.FromMinutes(1), DateTime.UtcNow.AddHours(-1));
        await decisions.CreateAsync(decision);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            Task.Run(() => (i % 2 == 0 ? decisions : other).ResolveAsync(
                decision.Id, PendingDecisionResolution.TimedOut, null, DateTime.UtcNow))));

        results.Count(r => r).Should().Be(1, "an answer and a timeout must not both win");
    }

    [Fact]
    public async Task Times_are_stored_as_UTC_whatever_kind_the_caller_passed()
    {
        var decisions = new SqlPendingDecisionRepository(_dbPath);
        await decisions.InitializeAsync();
        var localCreated = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Local);
        await decisions.CreateAsync(Decision("t-local", TimeSpan.FromMinutes(30), localCreated));

        var loaded = (await decisions.GetUnresolvedForTaskAsync("t-local"))!;

        loaded.CreatedAt.Should().Be(localCreated.ToUniversalTime());
        loaded.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
