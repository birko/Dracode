using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Drake parks a task on a pending decision and keeps it parked until the decision is answered or times out
/// (TASK-019, FEATURE-027 D1–D4): the work picker skips it and its dependents, a Kobold cannot overwrite it, a
/// decision with no timeout never expires, and the stored decision is the source of truth after a restart.
/// </summary>
public class ParkedTaskDrakeTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlProjectRepository _projects = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private const string ProjectId = "proj-park";

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_park_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");
        _projects = new SqlProjectRepository(db);
        await _projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();

        // A provider must exist to build a Kobold; nothing here sends a request to it
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
        _kobolds = new KoboldFactory(_projects, NullLoggerFactory.Instance, config);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private (Drake drake, TaskTracker tracker) NewDrake(bool withDecisions = true, TaskTracker? tracker = null)
    {
        tracker ??= new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId, pendingDecisions: withDecisions ? _decisions : null);
        return (drake, tracker);
    }

    private static PendingDecision Decision(TaskRecord task, TimeSpan? timeout, DateTime? createdAt = null,
        EscalationAlert? escalation = null) => new()
    {
        ProjectId = ProjectId,
        TaskId = task.Id,
        Source = escalation is null ? PendingDecisionSource.AskHuman : PendingDecisionSource.Escalation,
        Question = "Which database?",
        Escalation = escalation,
        CreatedAt = createdAt ?? DateTime.UtcNow,
        Timeout = timeout
    };

    [Fact]
    public async Task A_parked_task_and_its_dependent_are_not_offered_as_work_while_others_are()
    {
        var (drake, tracker) = NewDrake();
        var parked = tracker.AddTask("[api-1] Pick the storage engine");
        var dependent = tracker.AddTask("[api-2] Build the store", dependencies: new() { "api-1" });
        var other = tracker.AddTask("[ui-1] Draw the login form");

        (await drake.ParkTaskAsync(parked, Decision(parked, null))).Should().BeTrue();

        parked.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        drake.IsTaskParked(parked.Id).Should().BeTrue();
        (await _decisions.GetUnresolvedForTaskAsync(parked.Id)).Should().NotBeNull("the decision is stored first");
        var offered = drake.GetUnassignedTasks().Select(t => t.Task.Id).ToList();
        offered.Should().Contain(other.Id).And.NotContain(parked.Id).And.NotContain(dependent.Id);
    }

    [Fact]
    public async Task Without_a_decision_store_parking_is_refused_and_changes_nothing()
    {
        var (drake, tracker) = NewDrake(withDecisions: false);
        var task = tracker.AddTask("[api-1] Pick the storage engine");

        (await drake.ParkTaskAsync(task, Decision(task, null))).Should().BeFalse();

        task.Status.Should().Be(TaskStatus.Unassigned);
        drake.IsTaskParked(task.Id).Should().BeFalse();
    }

    [Fact]
    public async Task A_lingering_Kobold_does_not_overwrite_a_parked_status()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Pick the storage engine");
        await drake.ParkTaskAsync(task, Decision(task, null));
        var kobold = _kobolds.CreateKobold("ollama", "csharp", projectId: ProjectId);
        kobold.AssignTask(Guid.Parse(task.Id), task.Task, ProjectId);

        await drake.SyncTaskFromKoboldAsync(kobold);

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
    }

    [Fact]
    public async Task An_expired_decision_unparks_the_task_and_is_recorded_as_timed_out()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Pick the storage engine");
        var now = DateTime.UtcNow;
        var decision = Decision(task, TimeSpan.FromMinutes(5), now.AddHours(-1),
            new EscalationAlert { ProjectId = ProjectId, TaskId = task.Id, Type = EscalationType.NeedsSplit, Summary = "too big" });
        await drake.ParkTaskAsync(task, decision);

        var expired = await drake.ExpirePendingDecisionsAsync(now);

        expired.Should().Be(1);
        task.Status.Should().Be(TaskStatus.Unassigned);
        drake.IsTaskParked(task.Id).Should().BeFalse();
        (await _decisions.GetUnresolvedForTaskAsync(task.Id)).Should().BeNull();
        (await _decisions.GetExpiredAsync(now)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_decision_with_no_timeout_never_expires()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Approve the result");
        await drake.ParkTaskAsync(task, Decision(task, null, DateTime.UtcNow.AddYears(-1)));

        (await drake.ExpirePendingDecisionsAsync(DateTime.UtcNow.AddYears(1))).Should().Be(0);

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
    }

    [Fact]
    public async Task After_a_restart_the_stored_decisions_decide_which_tasks_are_parked()
    {
        // The tracker file lost the parked status of one task and kept it on another whose decision was answered
        var tracker = new TaskTracker();
        var lostPark = tracker.AddTask("[api-1] Pick the storage engine");
        var staleParked = tracker.AddTask("[api-2] Approve the schema");
        tracker.UpdateTask(staleParked, TaskStatus.AwaitingHumanDecision);
        await _decisions.CreateAsync(Decision(lostPark, null));
        var answered = Decision(staleParked, null);
        await _decisions.CreateAsync(answered);
        await _decisions.ResolveAsync(answered.Id, PendingDecisionResolution.Answered, "yes", DateTime.UtcNow);

        var (drake, _) = NewDrake(tracker: tracker);
        await drake.ReconcilePendingDecisionsAsync();

        lostPark.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        staleParked.Status.Should().Be(TaskStatus.Unassigned);
    }

    [Fact]
    public async Task The_reasoning_monitor_treats_a_parked_tasks_Kobold_as_waiting_not_stalled()
    {
        var (drake, tracker) = NewDrake();
        var parked = tracker.AddTask("[api-1] Pick the storage engine");
        var running = tracker.AddTask("[ui-1] Draw the login form");
        await drake.ParkTaskAsync(parked, Decision(parked, null));
        var onParked = _kobolds.CreateKobold("ollama", "csharp", projectId: ProjectId);
        onParked.AssignTask(Guid.Parse(parked.Id), parked.Task, ProjectId);
        var onRunning = _kobolds.CreateKobold("ollama", "csharp", projectId: ProjectId);
        onRunning.AssignTask(Guid.Parse(running.Id), running.Task, ProjectId);

        ReasoningMonitorService.BelongsToParkedTask(onParked, new[] { drake }).Should().BeTrue();
        ReasoningMonitorService.BelongsToParkedTask(onRunning, new[] { drake }).Should().BeFalse();
    }

    [Fact]
    public async Task Orphan_recovery_leaves_a_parked_task_alone()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Pick the storage engine");
        await drake.ParkTaskAsync(task, Decision(task, null));

        await drake.RecoverOrphanedTasksAsync();

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
    }

    [Fact]
    public void An_area_with_a_parked_task_keeps_its_Drake_so_the_timeout_can_fire()
    {
        // Every unassigned task waits on the parked one, so by dependencies alone the area looks fully blocked
        var tracker = new TaskTracker();
        var parked = tracker.AddTask("[api-1] Pick the storage engine");
        tracker.UpdateTask(parked, TaskStatus.AwaitingHumanDecision);
        tracker.AddTask("[api-2] Build the store", dependencies: new() { "api-1" });

        DrakeExecutionService.HasTasksNeedingADrake(tracker.GetAllTasks()).Should().BeTrue();
    }

    [Fact]
    public async Task Only_the_Drake_holding_the_task_expires_its_decision()
    {
        // One Drake per area: the frontend Drake must not claim the backend task's decision
        var (backend, backendTasks) = NewDrake();
        var (frontend, _) = NewDrake();
        var task = backendTasks.AddTask("[api-1] Pick the storage engine");
        var now = DateTime.UtcNow;
        await backend.ParkTaskAsync(task, Decision(task, TimeSpan.FromMinutes(5), now.AddHours(-1)));

        (await frontend.ExpirePendingDecisionsAsync(now)).Should().Be(0);
        (await _decisions.GetUnresolvedForTaskAsync(task.Id)).Should().NotBeNull();

        (await backend.ExpirePendingDecisionsAsync(now)).Should().Be(1);
        task.Status.Should().Be(TaskStatus.Unassigned);
    }

    [Fact]
    public async Task A_task_already_parked_cannot_be_parked_again()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Pick the storage engine");
        await drake.ParkTaskAsync(task, Decision(task, null));

        (await drake.ParkTaskAsync(task, Decision(task, null))).Should().BeFalse();
        (await _decisions.GetUnresolvedForProjectAsync(ProjectId)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_decision_is_stored_under_the_Drakes_own_project_whatever_the_caller_set()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Pick the storage engine");
        var decision = Decision(task, null);
        decision.ProjectId = "someone-elses-project";

        await drake.ParkTaskAsync(task, decision);

        (await _decisions.GetUnresolvedForTaskAsync(task.Id))!.ProjectId.Should().Be(ProjectId);
    }

    [Fact]
    public async Task Statistics_count_parked_tasks()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Pick the storage engine");
        await drake.ParkTaskAsync(task, Decision(task, null));

        drake.GetStatistics().AwaitingDecisionTasks.Should().Be(1);
    }
}
