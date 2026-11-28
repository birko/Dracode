using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Server.Jobs;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Repeated failures and repeated monitor alerts become tracked fix tasks (TASK-223, FEATURE-078 D14): one open fix per
/// failed task and kind, worked before other work of its priority in order of how many tasks wait on the failed one, and
/// a finished fix sends the failed task back to run.
/// </summary>
public class FixTaskTests : IAsyncLifetime
{
    private string _dir = null!;
    private KoboldFactory _kobolds = null!;
    private SqlProjectRepository _projects = null!;
    private const string ProjectId = "proj-fix";

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_fix_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_dir, "tasks"));
        _projects = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await _projects.InitializeAsync();
        _kobolds = new KoboldFactory(_projects, NullLoggerFactory.Instance, new KoboldLairConfiguration
        {
            ProjectsPath = _dir, DefaultProvider = "ollama",
            Providers = new() { new ProviderConfig { Name = "ollama", Type = "ollama", DefaultModel = "m1", IsEnabled = true,
                RequiresApiKey = false, CompatibleAgents = new() { "all" }, Configuration = new() } }
        });
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private (Drake drake, TaskTracker tracker) NewDrake()
    {
        var tracker = new TaskTracker();
        return (new Drake(_kobolds, tracker, Path.Combine(_dir, "tasks", "backend-tasks.md"), "ollama", projectId: ProjectId), tracker);
    }

    private static TaskRecord Failed(TaskTracker tracker, string text, TaskPriority priority = TaskPriority.Normal, string error = "Permanent: syntax error")
    {
        var task = tracker.AddTask(text, priority);
        tracker.UpdateTask(task, TaskStatus.Failed, "python");
        task.ErrorMessage = error;
        return task;
    }

    [Fact]
    public async Task A_failure_files_one_fix_task_naming_it_and_never_a_second_open_one()
    {
        var (drake, tracker) = NewDrake();
        var failed = Failed(tracker, "[api-1] Build the store (depends on: db-1)", TaskPriority.High);

        var fix = await drake.FileFixTaskAsync(failed, "failure", "it failed 5 times: connection refused");
        var again = await drake.FileFixTaskAsync(failed, "failure", "it failed 5 times: connection refused");
        var other = await drake.FileFixTaskAsync(failed, "monitor-stuck-loop", "Stuck loop detected");

        fix.Should().NotBeNull();
        again.Should().BeNull("the same failure already has an open fix task");
        other.Should().NotBeNull("another kind of failure is another fix");
        fix!.FixesTaskId.Should().Be(failed.Id);
        fix.FixKind.Should().Be("failure");
        fix.Priority.Should().Be(TaskPriority.High);
        fix.Task.Should().Contain("api-1").And.Contain("connection refused");
        fix.Task.Should().NotContain("[api-1]", "a fix must not read as the failed task when dependencies are matched")
            .And.NotContain("depends on");
        fix.Dependencies.Should().BeEmpty();

        tracker.UpdateTask(fix, TaskStatus.Failed, "python");
        (await drake.FileFixTaskAsync(failed, "failure", "again")).Should().BeNull("a failed fix still stands for the failure");
    }

    [Fact]
    public async Task Fix_tasks_go_first_within_their_priority_the_one_most_work_waits_on_first()
    {
        var (drake, tracker) = NewDrake();
        var lonely = Failed(tracker, "[a-1] Lonely");
        var busy = Failed(tracker, "[b-1] Busy");
        tracker.AddTask("[b-2] Uses b-1 (depends on: b-1)");
        tracker.AddTask("[b-3] Uses b-2 (depends on: b-2)");
        tracker.AddTask("[c-1] Plain normal work");
        tracker.AddTask("[d-1] Urgent work", TaskPriority.Critical);

        var lonelyFix = await drake.FileFixTaskAsync(lonely, "failure", "x");
        var busyFix = await drake.FileFixTaskAsync(busy, "failure", "y");

        var ready = drake.GetUnassignedTasks().Select(r => r.Task.Id).ToList();
        ready.Take(3).Should().Equal(
            tracker.GetAllTasks().Single(t => t.Task.StartsWith("[d-1]")).Id,   // a higher priority still goes first
            busyFix!.Id,                                                     // two tasks wait on b-1
            lonelyFix!.Id);                                                  // none wait on a-1
    }

    [Fact]
    public async Task A_finished_fix_sends_the_failed_task_back_to_run()
    {
        var (drake, tracker) = NewDrake();
        var failed = Failed(tracker, "[api-1] Build the store");
        failed.RetryCount = 5;
        var dependent = tracker.AddTask("[api-2] Use the store (depends on: api-1)");
        tracker.UpdateTask(dependent, TaskStatus.BlockedByFailure);
        var fix = await drake.FileFixTaskAsync(failed, "failure", "it keeps failing");
        tracker.UpdateTask(fix!, TaskStatus.Done, "python");

        var ready = drake.GetUnassignedTasks().Select(r => r.Task.Id).ToList();

        failed.Status.Should().Be(TaskStatus.Unassigned);
        failed.RetryCount.Should().Be(0);
        failed.ErrorMessage.Should().BeNull();
        ready.Should().Contain(failed.Id);
        dependent.Status.Should().Be(TaskStatus.Unassigned, "its failed dependency runs again");

        // A failure after that run is new: it may file a fix of its own, and the old fix does not reopen it again
        tracker.UpdateTask(failed, TaskStatus.Failed, "python");
        drake.GetUnassignedTasks();
        failed.Status.Should().Be(TaskStatus.Failed);
        (await drake.FileFixTaskAsync(failed, "failure", "failed again")).Should().NotBeNull();
    }

    [Fact]
    public void A_tag_quoted_inside_another_tasks_text_does_not_meet_a_dependency()
    {
        var (drake, tracker) = NewDrake();
        Failed(tracker, "[api-1] Build the store");
        var quoting = tracker.AddTask("[doc-1] Write notes. Original task: [api-1] Build the store; see [intent] notes");
        tracker.UpdateTask(quoting, TaskStatus.Done, "python");
        var dependent = tracker.AddTask("[api-2] Use the store (depends on: api-1)");
        var other = tracker.AddTask("[ui-1] Show the notes (depends on: intent)");

        var ready = drake.GetUnassignedTasks().Select(r => r.Task.Id).ToList();

        ready.Should().NotContain(dependent.Id, "only a task whose own tag is [api-1] is api-1 (TASK-231)");
        dependent.Status.Should().Be(TaskStatus.BlockedByFailure);
        ready.Should().NotContain(other.Id, "no task is named [intent]");
    }

    [Fact]
    public void A_task_whose_own_tag_is_done_meets_the_dependency()
    {
        var (drake, tracker) = NewDrake();
        var store = tracker.AddTask("  [API-1] Build the store");
        tracker.UpdateTask(store, TaskStatus.Done, "python");
        var dependent = tracker.AddTask("[api-2] Use the store (depends on: api-1)");

        drake.GetUnassignedTasks().Select(r => r.Task.Id).Should().Contain(dependent.Id);
    }

    [Theory]
    [InlineData("Permanent error: syntax error in menu.py", 0, true)]
    [InlineData("Network timeout talking to the provider", 5, true)]
    [InlineData("Network timeout talking to the provider", 2, false)]
    [InlineData("acceptance check failed after 2 reopen(s): menu.py is empty", 0, false)]
    [InlineData(null, 5, false)]
    public void Recovery_gives_up_on_a_permanent_error_or_when_its_retries_are_used_up(string? error, int retries, bool gaveUp)
    {
        var task = new TaskRecord { Task = "[api-1] x", Status = TaskStatus.Failed, ErrorMessage = error, RetryCount = retries };

        (FailureRecoveryJob.GaveUpReason(task, maxRetryAttempts: 5) != null).Should().Be(gaveUp);
    }

    [Fact]
    public void A_fix_task_that_fails_gets_no_fix_of_its_own()
    {
        var fix = new TaskRecord { Task = "x", Status = TaskStatus.Failed, ErrorMessage = "Permanent error", FixesTaskId = "t", FixKind = "failure" };

        FailureRecoveryJob.GaveUpReason(fix, maxRetryAttempts: 5).Should().BeNull();
    }

    private static EscalationAlert Monitor(string summary) => new()
    {
        Source = EscalationSource.ReasoningMonitor, Type = EscalationType.WrongApproach, Summary = summary
    };

    [Fact]
    public void A_monitor_alert_is_a_repeat_when_the_task_already_had_one_of_its_kind()
    {
        var plan = new KoboldImplementationPlan { TaskId = "t", ProjectId = ProjectId };

        ReasoningMonitorService.RepeatedKind(plan, "Stuck loop detected: 2 file operation(s) repeated 3+ times").Should().BeNull();
        plan.Escalations.Add(Monitor("Stuck loop detected: 1 file operation(s) repeated 3+ times"));
        ReasoningMonitorService.RepeatedKind(plan, "No step progress for 12 minutes despite active LLM responses").Should().BeNull();
        ReasoningMonitorService.RepeatedKind(plan, "Stuck loop detected: 2 file operation(s) repeated 3+ times")
            .Should().Be("monitor-stuck-loop");
    }

    [Fact]
    public async Task A_repeated_monitor_alert_files_a_fix_task_through_the_Drake_holding_the_task()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Build the store");
        tracker.UpdateTask(task, TaskStatus.Working, "python");
        var alert = Monitor("Stuck loop detected: 2 file operation(s) repeated 3+ times");
        alert.ProjectId = ProjectId;
        alert.TaskId = task.Id;

        await ReasoningMonitorService.RouteToHoldingDrakeAsync(alert, () => [drake], NullLogger.Instance, 1, TimeSpan.Zero,
            repeatedKind: "monitor-stuck-loop");

        tracker.GetAllTasks().Should().ContainSingle(t => t.FixesTaskId == task.Id && t.FixKind == "monitor-stuck-loop");
    }
}
