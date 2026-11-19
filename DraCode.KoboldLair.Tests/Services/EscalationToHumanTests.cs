using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Options;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Drake routes an escalation to a person when its disposition is HumanDecisionRequired (TASK-020, FEATURE-027 D5):
/// the task parks, a decision card is raised, its run is stopped, and an answer resumes it. Every other disposition
/// keeps today's automatic routes.
/// </summary>
public class EscalationToHumanTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private KoboldLairConfiguration _config = null!;
    private const string ProjectId = "proj-route";

    private readonly List<PendingDecision> _raised = new();
    private readonly List<PendingDecision> _resolved = new();

    private sealed class FixedPolicy(EscalationDisposition disposition, TimeSpan? timeout = null) : IEscalationDispositionPolicy
    {
        public EscalationRoute Choose(EscalationAlert alert, TaskRecord? task) => new(disposition, timeout);
    }

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_route_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();

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
        _kobolds = new KoboldFactory(projects, NullLoggerFactory.Instance, config);
        _config = config;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private (Drake drake, TaskTracker tracker) NewDrake(IEscalationDispositionPolicy? policy = null, bool withDecisions = true)
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId,
            pendingDecisions: withDecisions ? _decisions : null,
            dispositionPolicy: policy,
            onDecisionRequired: (_, d) => _raised.Add(d),
            onDecisionResolved: (_, d) => _resolved.Add(d));
        return (drake, tracker);
    }

    private static EscalationAlert Alert(TaskRecord task, EscalationType type = EscalationType.WrongAgentType) => new()
    {
        ProjectId = ProjectId, TaskId = task.Id, AgentType = "csharp", Type = type, Summary = "Wrong agent for this"
    };

    private static TaskRecord Working(TaskTracker tracker, string text = "[api-1] Pick the storage engine")
    {
        var task = tracker.AddTask(text);
        tracker.UpdateTask(task, TaskStatus.Working, "csharp");
        return task;
    }

    [Fact]
    public async Task An_escalation_chosen_for_a_person_parks_the_task_and_raises_a_decision_card()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired, TimeSpan.FromHours(2)));
        var task = Working(tracker);

        await drake.HandleEscalationAsync(Alert(task));

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        var stored = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;
        stored.Source.Should().Be(PendingDecisionSource.Escalation);
        stored.Escalation!.Type.Should().Be(EscalationType.WrongAgentType);
        stored.Timeout.Should().Be(TimeSpan.FromHours(2));

        var card = _raised.Should().ContainSingle().Subject;
        var metadata = PendingDecisionNotification.ToMetadata(card);
        metadata[PendingDecisionNotification.RequiresResponse].Should().Be("true");
        metadata[PendingDecisionNotification.DecisionId].Should().Be(stored.Id);
        metadata[PendingDecisionNotification.TaskId].Should().Be(task.Id);
        metadata[PendingDecisionNotification.EscalationType].Should().Be("WrongAgentType");
        metadata[PendingDecisionNotification.ResponseSchema].Should().Contain(PendingDecisionNotification.HandleAutomatically);
    }

    [Fact]
    public async Task By_default_escalations_keep_their_automatic_routes()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker);

        await drake.HandleEscalationAsync(Alert(task));

        task.Status.Should().Be(TaskStatus.Unassigned, "WrongAgentType resets the task, as before");
        (await _decisions.GetUnresolvedForProjectAsync(ProjectId)).Should().BeEmpty();
        _raised.Should().BeEmpty();
    }

    [Fact]
    public async Task An_explicit_automatic_disposition_overrides_the_policy()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired));
        var task = Working(tracker);

        await drake.HandleEscalationAsync(Alert(task), EscalationDisposition.AutoResolve);

        task.Status.Should().Be(TaskStatus.Unassigned);
        _raised.Should().BeEmpty();
    }

    [Fact]
    public async Task A_timed_out_escalation_is_not_parked_again_by_the_policy()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired, TimeSpan.FromMinutes(5)));
        var task = Working(tracker);
        tracker.SetError(task, "picked the wrong agent");
        await drake.HandleEscalationAsync(Alert(task));
        var decision = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;

        await drake.ExpirePendingDecisionsAsync(decision.CreatedAt.AddHours(1));

        task.Status.Should().Be(TaskStatus.Unassigned);
        task.ErrorMessage.Should().BeNull("the automatic route ran (WrongAgentType clears the error), not a bare unpark");
        (await _decisions.GetUnresolvedForTaskAsync(task.Id)).Should().BeNull();
        _resolved.Should().ContainSingle(d => d.Id == decision.Id && d.ResolutionKind == PendingDecisionResolution.TimedOut);
    }

    [Fact]
    public async Task An_escalation_for_a_task_already_parked_leaves_it_parked_with_one_decision()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired));
        var task = Working(tracker);
        await drake.HandleEscalationAsync(Alert(task));

        await drake.HandleEscalationAsync(Alert(task, EscalationType.NeedsSplit));

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        (await _decisions.GetUnresolvedForProjectAsync(ProjectId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Without_a_decision_store_the_escalation_falls_through_to_its_automatic_route()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired), withDecisions: false);
        var task = Working(tracker);

        await drake.HandleEscalationAsync(Alert(task));

        task.Status.Should().Be(TaskStatus.Unassigned);
    }

    [Fact]
    public async Task An_answer_resumes_the_task_and_is_recorded_once()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired));
        var task = Working(tracker);
        await drake.HandleEscalationAsync(Alert(task));
        var decision = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;

        (await drake.ResolvePendingDecisionAsync(decision.Id, "Use the python agent instead")).Should().BeTrue();

        task.Status.Should().Be(TaskStatus.Unassigned);
        drake.GetUnassignedTasks().Select(t => t.Task.Id).Should().Contain(task.Id);
        var answered = (await _decisions.GetResolvedForTaskAsync(task.Id)).Should().ContainSingle().Subject;
        answered.Resolution.Should().Be("Use the python agent instead");
        answered.ResolutionKind.Should().Be(PendingDecisionResolution.Answered);
        _resolved.Should().ContainSingle(d => d.Id == decision.Id);

        (await drake.ResolvePendingDecisionAsync(decision.Id, "again")).Should().BeFalse();
    }

    [Fact]
    public async Task An_answer_after_the_timeout_is_refused()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired, TimeSpan.FromMinutes(5)));
        var task = Working(tracker);
        await drake.HandleEscalationAsync(Alert(task));
        var decision = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;
        await drake.ExpirePendingDecisionsAsync(decision.CreatedAt.AddHours(1));

        (await drake.ResolvePendingDecisionAsync(decision.Id, "too late")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_it_automatically_runs_the_escalations_automatic_route()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired));
        var task = Working(tracker);
        tracker.SetError(task, "picked the wrong agent");
        await drake.HandleEscalationAsync(Alert(task));
        var decision = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;

        (await drake.ResolvePendingDecisionAsync(decision.Id, PendingDecisionNotification.HandleAutomatically)).Should().BeTrue();

        task.Status.Should().Be(TaskStatus.Unassigned);
        task.ErrorMessage.Should().BeNull("WrongAgentType's route clears the error; a plain answer would keep it");
    }

    [Fact]
    public async Task Parking_a_task_cancels_its_running_Kobolds_run()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker);
        using var run = new CancellationTokenSource();
        drake.BeginRun(task.Id, run);

        await drake.ParkTaskAsync(task, new PendingDecision { Source = PendingDecisionSource.AskHuman, Question = "?" });

        run.IsCancellationRequested.Should().BeTrue();
        drake.EndRun(task.Id, run);
    }

    [Fact]
    public async Task An_answer_for_another_projects_decision_is_refused()
    {
        var (drake, _) = NewDrake();
        var foreign = new PendingDecision { ProjectId = "other-project", TaskId = "t-x", Question = "?" };
        await _decisions.CreateAsync(foreign);

        (await drake.ResolvePendingDecisionAsync(foreign.Id, "yes")).Should().BeFalse();
        (await _decisions.GetByIdAsync(foreign.Id))!.ResolutionKind.Should().BeNull();
    }

    [Fact]
    public async Task With_no_Drake_alive_an_answer_is_stored_for_reconcile_to_apply()
    {
        var providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config), Path.Combine(_dir, "user-settings.json"));
        var factory = new DrakeFactory(_kobolds, providers, _config, NullLoggerFactory.Instance, pendingDecisions: _decisions);
        var decision = new PendingDecision { ProjectId = ProjectId, TaskId = "t-1", Question = "?" };
        await _decisions.CreateAsync(decision);

        (await factory.ResolvePendingDecisionAsync("other-project", decision.Id, "yes")).Should().BeFalse();
        (await factory.ResolvePendingDecisionAsync(ProjectId, decision.Id, "yes")).Should().BeTrue();
        (await factory.ResolvePendingDecisionAsync(ProjectId, decision.Id, "again")).Should().BeFalse();
        (await _decisions.GetByIdAsync(decision.Id))!.Resolution.Should().Be("yes");
    }

    [Fact]
    public async Task Two_escalations_at_once_park_the_task_on_one_decision()
    {
        var (drake, tracker) = NewDrake(new FixedPolicy(EscalationDisposition.HumanDecisionRequired));
        var task = Working(tracker);

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => drake.HandleEscalationAsync(Alert(task)))));

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        (await _decisions.GetUnresolvedForProjectAsync(ProjectId)).Should().ContainSingle();
        _raised.Should().ContainSingle();
    }

    [Fact]
    public async Task A_parked_task_absorbs_an_escalation_the_policy_would_resolve_automatically()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker);
        await drake.ParkTaskAsync(task, new PendingDecision { Source = PendingDecisionSource.AskHuman, Question = "?" });

        await drake.HandleEscalationAsync(Alert(task));

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision, "a late escalation must not reset a task its person is deciding on");
    }

    [Fact]
    public async Task Handle_it_automatically_answered_while_no_Drake_ran_is_applied_when_one_starts()
    {
        var tracker = new TaskTracker();
        var task = Working(tracker);
        tracker.SetError(task, "picked the wrong agent");
        var (parker, _) = (new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId, pendingDecisions: _decisions,
            dispositionPolicy: new FixedPolicy(EscalationDisposition.HumanDecisionRequired)), 0);
        await parker.HandleEscalationAsync(Alert(task));
        var decision = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;

        // The Drake is gone; the answer is stored directly
        var providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config), Path.Combine(_dir, "user-settings.json"));
        var factory = new DrakeFactory(_kobolds, providers, _config, NullLoggerFactory.Instance, pendingDecisions: _decisions)
        {
            OnDecisionResolved = (_, d) => _resolved.Add(d)
        };
        (await factory.ResolvePendingDecisionAsync(ProjectId, decision.Id, PendingDecisionNotification.HandleAutomatically))
            .Should().BeTrue();
        _resolved.Should().ContainSingle(d => d.Id == decision.Id, "the card is retired even with no Drake running");

        // A Drake starts on the same tasks and reconciles
        var restarted = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId, pendingDecisions: _decisions);
        await restarted.ReconcilePendingDecisionsAsync();

        task.Status.Should().Be(TaskStatus.Unassigned);
        task.ErrorMessage.Should().BeNull("the escalation's automatic route ran on reconcile");
    }
}
