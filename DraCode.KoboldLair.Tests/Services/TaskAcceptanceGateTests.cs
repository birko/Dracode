using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents;
using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Drake holds a finished task to its acceptance check before Done is written (TASK-025, FEATURE-031 D1–D5): a broken
/// promise reopens the task with the unmet criteria (D3) up to <c>MaxAcceptanceReopens</c>, then escalates it to a
/// person (TASK-222), whose "Handle it automatically" fails it with a fix task; what no machine can judge parks the task on a person's Approve / Reject (D4); with the switch off nothing
/// changes (D5).
/// </summary>
public class TaskAcceptanceGateTests : IAsyncLifetime
{
    private string _dir = null!;
    private string _workspace = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private const string ProjectId = "proj-accept";

    private const string AuthTask =
        "[api-1] Create authentication service (src/services/AuthService.ts). " +
        "Exports: AuthService class with login(email, password): Promise<TokenPair>, refresh(token): Promise<TokenPair>";

    private readonly List<PendingDecision> _raised = new();

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_gate_{Guid.NewGuid():N}");
        _workspace = Path.Combine(_dir, "workspace");
        Directory.CreateDirectory(_workspace);
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
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private (Drake drake, TaskTracker tracker) NewDrake(bool acceptance = true, int maxReopens = 2)
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId,
            pendingDecisions: _decisions,
            onDecisionRequired: (_, d) => _raised.Add(d),
            acceptance: acceptance ? new VerificationConfiguration { TaskAcceptanceEnabled = true, MaxAcceptanceReopens = maxReopens } : null);
        return (drake, tracker);
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_workspace, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>A Kobold that finished <paramref name="task"/> successfully in the test workspace.</summary>
    private Kobold Finished(TaskRecord task)
    {
        var kobold = _kobolds.CreateKobold("ollama", "csharp", new AgentOptions { WorkingDirectory = _workspace }, projectId: ProjectId);
        kobold.AssignTask(Guid.Parse(task.Id), task.Task, ProjectId);
        typeof(Kobold).GetProperty(nameof(Kobold.Status))!.SetValue(kobold, KoboldStatus.Done);
        return kobold;
    }

    private static TaskRecord Working(TaskTracker tracker, string text)
    {
        var task = tracker.AddTask(text);
        tracker.UpdateTask(task, TaskStatus.Working, "csharp");
        return task;
    }

    private void MissingRefresh() => Write("src/services/AuthService.ts", "export class AuthService {\n  login(email, password) {}\n}\n");

    private void Complete() => Write("src/services/AuthService.ts",
        "export class AuthService {\n  login(email, password) {}\n  refresh(token) {}\n}\n");

    [Fact]
    public async Task A_finished_task_that_missed_an_export_is_reopened_with_it_named_and_not_marked_done()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker, AuthTask);
        MissingRefresh();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Unassigned);
        task.AcceptanceAttempts.Should().Be(1);
        task.Task.Should().Contain("Acceptance check failed (attempt 1)").And.Contain("refresh");
        tracker.GetAllTasks().Should().ContainSingle("a reopen below the cap files no fix task");
    }

    /// <summary>A task that already went back to its worker twice: two reopen notes and the cap reached.</summary>
    private static TaskRecord ReopenedTwice(TaskTracker tracker)
    {
        var task = Working(tracker, AuthTask +
            "\n\nAcceptance check failed (attempt 1): Export refresh is not declared." +
            "\n\nAcceptance check failed (attempt 2): [intent] missing: refresh() is a stub");
        task.AcceptanceAttempts = 2;
        return task;
    }

    private async Task<(Drake drake, TaskTracker tracker, TaskRecord task, PendingDecision card)> Escalated()
    {
        var (drake, tracker) = NewDrake(maxReopens: 2);
        var task = ReopenedTwice(tracker);
        MissingRefresh();
        await drake.SyncTaskFromKoboldAsync(Finished(task));
        var card = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;
        return (drake, tracker, task, card);
    }

    [Fact]
    public async Task The_third_failure_escalates_to_a_person_with_the_findings_of_every_attempt_instead_of_reopening()
    {
        var (_, tracker, task, card) = await Escalated();

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        task.AcceptanceAttempts.Should().Be(2, "it is not reopened again on its own");
        tracker.GetAllTasks().Should().ContainSingle("no fix task until the person says so");
        card.Should().NotBeNull();
        card.Source.Should().Be(PendingDecisionSource.Approval);
        card.Timeout.Should().BeNull("an escalated task waits for its person");
        PendingDecisionNotification.QuestionText(card).Should().Contain("failed its checks 3 times")
            .And.Contain("- attempt 1: Export refresh is not declared").And.Contain("- attempt 2: [intent] missing: refresh() is a stub")
            .And.Contain("refresh", "this time's finding");
        PendingDecisionNotification.ResponseSchemaFor(card).Should().Contain("Approve")
            .And.Contain(PendingDecisionNotification.HandleAutomatically);
        _raised.Should().ContainSingle(d => d.Id == card.Id);
    }

    [Fact]
    public async Task Handle_it_automatically_fails_the_escalated_task_for_good_and_files_a_fix_task_naming_the_findings()
    {
        var (drake, tracker, task, card) = await Escalated();

        (await drake.ResolvePendingDecisionAsync(card.Id, PendingDecisionNotification.HandleAutomatically))
            .Should().Be(DecisionAnswerResult.Resolved);

        task.Status.Should().Be(TaskStatus.Failed);
        ErrorClassifier.IsPermanent(task.ErrorMessage).Should().BeTrue("failure recovery must not rerun it unchanged");
        var fix = tracker.GetAllTasks().Should().ContainSingle(t => t.Id != task.Id).Subject;
        fix.Task.Should().Contain("refresh");
        fix.Priority.Should().Be(TaskPriority.Critical, "a missing export breaks whoever imports it");
    }

    [Fact]
    public async Task The_acceptance_fix_is_a_fix_task_for_the_failed_one_and_finishing_it_reruns_that_task()
    {
        var (drake, tracker, task, card) = await Escalated();
        var dependent = tracker.AddTask("[api-2] Use the auth service (depends on: api-1)");
        await drake.ResolvePendingDecisionAsync(card.Id, PendingDecisionNotification.HandleAutomatically);
        var fix = tracker.GetAllTasks().Single(t => t.FixesTaskId != null);

        fix.FixesTaskId.Should().Be(task.Id);
        fix.FixKind.Should().Be("acceptance");
        fix.Task.Should().NotContain("[api-1]", "a finished fix must never read as the failed task (TASK-231)");
        tracker.UpdateTask(fix, TaskStatus.Done, "typescript");

        var ready = drake.GetUnassignedTasks().Select(r => r.Task.Id).ToList();

        ready.Should().NotContain(dependent.Id, "api-1 is not done: it runs again first");
        task.Status.Should().Be(TaskStatus.Unassigned);
        task.AcceptanceAttempts.Should().Be(0, "it starts its checks afresh");
        ready.Should().Contain(task.Id);
    }

    [Fact]
    public async Task A_persons_note_on_the_escalated_task_gives_it_one_more_attempt_and_its_next_failure_escalates_again()
    {
        var (drake, _, task, card) = await Escalated();

        await drake.ResolvePendingDecisionAsync(card.Id, "refresh must call the token endpoint, see auth.md");

        task.Status.Should().Be(TaskStatus.Unassigned, "a person may send it back beyond the cap");
        task.AcceptanceAttempts.Should().Be(3);
        task.Task.Should().Contain("refresh must call the token endpoint");

        MissingRefresh();
        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision, "past the cap every failure goes to the person");
        PendingDecisionNotification.QuestionText((await _decisions.GetUnresolvedForTaskAsync(task.Id))!)
            .Should().Contain("failed its checks 4 times");
    }

    [Fact]
    public async Task Approve_on_the_escalated_task_marks_it_done()
    {
        var (drake, _, task, card) = await Escalated();

        await drake.ResolvePendingDecisionAsync(card.Id, "Approve, the export lands in the next task");

        task.Status.Should().Be(TaskStatus.Done);
    }

    [Fact]
    public async Task Without_a_decision_store_the_third_failure_fails_the_task_and_files_a_fix_task_as_before()
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama", projectId: ProjectId,
            acceptance: new VerificationConfiguration { TaskAcceptanceEnabled = true, MaxAcceptanceReopens = 2 });
        var task = ReopenedTwice(tracker);
        MissingRefresh();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Failed);
        tracker.GetAllTasks().Should().Contain(t => t.Id != task.Id && t.Task.Contains("refresh"));
    }

    private sealed class AlwaysFinds : DraCode.KoboldLair.Services.Checks.ITaskChecker
    {
        public int Runs;
        public string Name => "always";
        public int Cost => 1;
        public Task<IReadOnlyList<DraCode.KoboldLair.Services.Checks.TaskFinding>> CheckAsync(
            DraCode.KoboldLair.Services.Checks.TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            Runs++;
            return Task.FromResult<IReadOnlyList<DraCode.KoboldLair.Services.Checks.TaskFinding>>([new("always", "missing", "something")]);
        }
    }

    [Fact]
    public async Task With_the_lifecycle_layer_off_the_checker_agents_do_not_run()
    {
        var (drake, tracker) = NewDrake();
        var checker = new AlwaysFinds();
        drake.TaskChecks = new DraCode.KoboldLair.Services.Checks.TaskCheckRunner([checker]);
        drake.LifecycleEnabled = () => false;
        var task = Working(tracker, AuthTask);
        Complete();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        checker.Runs.Should().Be(0, "the checker agents are the lifecycle layer's (TASK-211)");
        task.Status.Should().Be(TaskStatus.Done, "its own promises are kept, and the acceptance check stays on");
    }

    [Fact]
    public async Task With_the_lifecycle_layer_on_the_checker_agents_run()
    {
        var (drake, tracker) = NewDrake();
        var checker = new AlwaysFinds();
        drake.TaskChecks = new DraCode.KoboldLair.Services.Checks.TaskCheckRunner([checker]);
        drake.LifecycleEnabled = () => true;
        var task = Working(tracker, AuthTask);
        Complete();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        checker.Runs.Should().Be(1);
        task.Status.Should().Be(TaskStatus.Unassigned, "its finding reopens it");
    }

    [Fact]
    public async Task With_the_lifecycle_layer_off_the_third_failure_fails_with_a_fix_task_instead_of_a_persons_card()
    {
        var (drake, tracker) = NewDrake(maxReopens: 2);
        drake.LifecycleEnabled = () => false;
        var task = ReopenedTwice(tracker);
        MissingRefresh();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Failed, "as before FEATURE-078: no escalation card (TASK-211)");
        (await _decisions.GetUnresolvedForTaskAsync(task.Id)).Should().BeNull();
        tracker.GetAllTasks().Should().Contain(t => t.FixesTaskId == task.Id && t.FixKind == "acceptance");
    }

    [Fact]
    public async Task A_finished_task_that_keeps_its_promises_is_done()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker, AuthTask);
        Complete();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Done);
        task.AcceptanceAttempts.Should().Be(0);
    }

    [Fact]
    public async Task With_the_switch_off_a_finished_task_is_done_as_before()
    {
        var (drake, tracker) = NewDrake(acceptance: false);
        var task = Working(tracker, AuthTask);
        MissingRefresh();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Done);
    }

    private const string ProseTask =
        "[api-2] Create the login form (src/LoginForm.tsx). Must implement: shows an error under a wrong password";

    private async Task<(Drake drake, TaskTracker tracker, TaskRecord task, PendingDecision card)> ParkedForReview()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker, ProseTask);
        Write("src/LoginForm.tsx", "export function LoginForm() { return null; }\n");
        await drake.SyncTaskFromKoboldAsync(Finished(task));
        var card = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;
        return (drake, tracker, task, card);
    }

    [Fact]
    public async Task What_no_machine_can_judge_parks_the_task_on_a_review_card()
    {
        var (_, _, task, card) = await ParkedForReview();

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        card.Source.Should().Be(PendingDecisionSource.Approval);
        card.Timeout.Should().BeNull("a review card never times out");
        PendingDecisionNotification.QuestionText(card).Should().Contain("shows an error under a wrong password");
        PendingDecisionNotification.ResponseSchemaFor(card).Should().Contain("Approve").And.Contain("Reject");
        _raised.Should().ContainSingle(d => d.Id == card.Id);
    }

    [Fact]
    public async Task Approve_marks_the_task_done_and_keeps_a_remark()
    {
        var (drake, _, task, card) = await ParkedForReview();

        (await drake.ResolvePendingDecisionAsync(card.Id, "Approve, but rename the form later"))
            .Should().Be(DecisionAnswerResult.Resolved);

        task.Status.Should().Be(TaskStatus.Done);
        task.AcceptanceAttempts.Should().Be(0);
        (await _decisions.GetByIdAsync(card.Id))!.Resolution.Should().Be("Approve, but rename the form later");
    }

    [Fact]
    public async Task Any_other_answer_rejects_and_reopens_with_the_note_as_the_unmet_criterion()
    {
        var (drake, _, task, card) = await ParkedForReview();

        await drake.ResolvePendingDecisionAsync(card.Id, "the error never shows; wire it to the login response");

        task.Status.Should().Be(TaskStatus.Unassigned);
        task.AcceptanceAttempts.Should().Be(1);
        task.Task.Should().Contain("wire it to the login response");
        var heard = (await drake.LoadHumanAnswersAsync(task.Id))!.Should().ContainSingle().Subject;
        heard.ToMessage().Should().Contain("rejected").And.Contain("wire it to the login response");
    }

    [Fact]
    public async Task A_task_recovered_with_a_finished_plan_takes_the_same_check_rather_than_going_straight_to_done()
    {
        var projects = new ProjectRepository(_dir);
        projects.Add(new Project { Id = ProjectId, Name = "accept", OwnerId = "o", Paths = { Output = _dir } });
        var plans = new KoboldPlanService(_dir, projectRepository: projects, debounceIntervalMs: 0);
        var tracker = new TaskTracker();
        // The workspace is derived from the task file's place: <project>/tasks/<area>-tasks.md → <project>/workspace
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "tasks", "backend-tasks.md"), "ollama",
            projectId: ProjectId, planService: plans, pendingDecisions: _decisions,
            acceptance: new VerificationConfiguration { TaskAcceptanceEnabled = true });
        var task = Working(tracker, AuthTask);
        await plans.SavePlanAsync(new KoboldImplementationPlan
        {
            ProjectId = ProjectId, TaskId = task.Id, TaskDescription = AuthTask, Status = PlanStatus.Completed
        });
        MissingRefresh();

        (await drake.RecoverOrphanedTasksAsync()).Should().Be(1);

        task.Status.Should().Be(TaskStatus.Unassigned);
        task.AcceptanceAttempts.Should().Be(1, "recovery reopened it for the missing export");
    }

    /// <summary>A planner that answers a revision with one new step adding what the check found missing.</summary>
    private sealed class RevisingProvider : ILlmProvider
    {
        private int _calls;
        public int Calls => _calls;
        /// <summary>Runs as the planner's first model call starts: what happens while a revision is under way.</summary>
        public Action? DuringRevision { get; set; }
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1)
                DuringRevision?.Invoke();
            return Task.FromResult(_calls == 1
                ? new LlmResponse
                {
                    StopReason = "tool_use",
                    Content = new List<ContentBlock>
                    {
                        new()
                        {
                            Type = "tool_use", Id = "p1", Name = "create_implementation_plan",
                            Input = new Dictionary<string, object>
                            {
                                ["steps"] = System.Text.Json.JsonDocument.Parse(
                                    """[{"title":"Add refresh(token) to AuthService","description":"The acceptance check found refresh missing","files_to_modify":["src/services/AuthService.ts"]}]""").RootElement.Clone()
                            }
                        }
                    }
                }
                : new LlmResponse { StopReason = "end_turn", Content = new List<ContentBlock> { new() { Type = "text", Text = "revised" } } });
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>A Drake with a plan store and a scripted planner, its task file under tasks/ so the workspace is found.</summary>
    private (Drake drake, TaskTracker tracker, KoboldPlanService plans, RevisingProvider planner) NewDrakeWithPlanner(
        Action<string, EscalationAlert, string>? onEscalationNotify = null)
    {
        var projects = new ProjectRepository(_dir);
        projects.Add(new Project { Id = ProjectId, Name = "accept", OwnerId = "o", Paths = { Output = _dir } });
        var plans = new KoboldPlanService(_dir, projectRepository: projects, debounceIntervalMs: 0);
        Directory.CreateDirectory(Path.Combine(_dir, "tasks"));
        var provider = new RevisingProvider();
        var planner = new KoboldPlannerAgent(provider, new AgentOptions { WorkingDirectory = _workspace, MaxIterations = 3 });
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "tasks", "backend-tasks.md"), "ollama",
            projectId: ProjectId, planService: plans, plannerAgent: planner, pendingDecisions: _decisions,
            acceptance: new VerificationConfiguration { TaskAcceptanceEnabled = true }, onEscalationNotify: onEscalationNotify);
        return (drake, tracker, plans, provider);
    }

    /// <summary>A finished plan: its one step done, as a Kobold leaves it.</summary>
    private static Task SaveFinishedPlan(KoboldPlanService plans, TaskRecord task) => plans.SavePlanAsync(new KoboldImplementationPlan
    {
        ProjectId = ProjectId, TaskId = task.Id, TaskDescription = AuthTask, Status = PlanStatus.Completed, CurrentStepIndex = 1,
        Steps = [new ImplementationStep { Index = 1, Title = "Write AuthService with login", Status = StepStatus.Completed }]
    });

    private async Task ShouldBeRevisedForTheUnmetCriterion(KoboldPlanService plans, TaskRecord task, RevisingProvider planner)
    {
        planner.Calls.Should().BeGreaterThan(0, "the planner was asked to revise the saved plan");
        var plan = (await plans.LoadPlanAsync(ProjectId, task.Id))!;
        plan.Status.Should().Be(PlanStatus.InProgress, "a reopened task must not rerun a finished plan unchanged");
        plan.Steps.Select(s => (s.Title, s.Status)).Should().Equal(
            ("Write AuthService with login", StepStatus.Completed),
            ("Add refresh(token) to AuthService", StepStatus.Pending));
        plan.CurrentStepIndex.Should().Be(1, "the next run starts at the new step, keeping the finished one");
    }

    [Fact]
    public async Task A_task_reopened_by_orphan_recovery_gets_its_saved_plan_revised()
    {
        var (drake, tracker, plans, planner) = NewDrakeWithPlanner();
        var task = Working(tracker, AuthTask);
        await SaveFinishedPlan(plans, task);
        MissingRefresh();

        (await drake.RecoverOrphanedTasksAsync()).Should().Be(1);

        task.Status.Should().Be(TaskStatus.Unassigned);
        await ShouldBeRevisedForTheUnmetCriterion(plans, task, planner);
    }

    [Fact]
    public async Task A_task_reopened_after_its_Kobold_has_gone_gets_its_saved_plan_revised()
    {
        var (drake, tracker, plans, planner) = NewDrakeWithPlanner();
        var task = Working(tracker, AuthTask);
        await SaveFinishedPlan(plans, task);
        MissingRefresh();
        var kobold = Finished(task);
        _kobolds.RemoveKobold(kobold.Id); // its run has ended: the route cannot find it through the factory

        await drake.SyncTaskFromKoboldAsync(kobold);

        task.Status.Should().Be(TaskStatus.Unassigned);
        await ShouldBeRevisedForTheUnmetCriterion(plans, task, planner);
    }

    [Fact]
    public async Task An_answer_given_while_no_Drake_held_the_task_is_applied_when_one_reconciles()
    {
        var (_, tracker, task, card) = await ParkedForReview();
        await _decisions.ResolveAsync(card.Id, PendingDecisionResolution.Answered, "Approve", DateTime.UtcNow);
        var later = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama", projectId: ProjectId,
            pendingDecisions: _decisions, acceptance: new VerificationConfiguration { TaskAcceptanceEnabled = true });

        await later.ReconcilePendingDecisionsAsync();

        task.Status.Should().Be(TaskStatus.Done, "an approval is not a rerun");
    }

    [Fact]
    public async Task An_escalation_for_a_task_already_Done_is_dropped_without_a_plan_revision_or_a_notification()
    {
        // TASK-182: a run's last reflections can escalate after the task finished
        var notified = new List<EscalationAlert>();
        var (drake, tracker, plans, planner) = NewDrakeWithPlanner((_, alert, _) => notified.Add(alert));
        var task = tracker.AddTask(AuthTask);
        tracker.UpdateTask(task, TaskStatus.Done, "typescript");
        await SaveFinishedPlan(plans, task);

        await drake.HandleEscalationAsync(new EscalationAlert
        {
            ProjectId = ProjectId, TaskId = task.Id, Type = EscalationType.WrongApproach,
            Summary = "Progress stalled: last 3 reflections show no advancement (100→100→100%)"
        });

        planner.Calls.Should().Be(0, "a finished task's plan is not revised");
        notified.Should().BeEmpty("the person is not told about an escalation nothing acted on");
        (await plans.LoadPlanAsync(ProjectId, task.Id))!.Status.Should().Be(PlanStatus.Completed);
        task.Status.Should().Be(TaskStatus.Done);
    }

    [Fact]
    public async Task A_task_that_finishes_while_its_plan_is_revised_keeps_its_plan_and_raises_no_notification()
    {
        // TASK-182, seen in the human test: the escalation came while the task ran, the Kobold finished during the
        // revision, and the revision then replaced the finished plan and told the person
        var notified = new List<EscalationAlert>();
        var (drake, tracker, plans, planner) = NewDrakeWithPlanner((_, alert, _) => notified.Add(alert));
        var task = tracker.AddTask(AuthTask);
        tracker.UpdateTask(task, TaskStatus.Working, "typescript");
        await SaveFinishedPlan(plans, task);
        planner.DuringRevision = () => tracker.UpdateTask(task, TaskStatus.Done, "typescript");

        await drake.HandleEscalationAsync(new EscalationAlert
        {
            ProjectId = ProjectId, TaskId = task.Id, Type = EscalationType.WrongApproach,
            Summary = "Progress stalled: last 3 reflections show no advancement (97→80→85%)"
        });

        planner.Calls.Should().BeGreaterThan(0, "the task was still running when the escalation came");
        var plan = (await plans.LoadPlanAsync(ProjectId, task.Id))!;
        plan.Status.Should().Be(PlanStatus.Completed, "the revision is dropped: the plan matches what was built");
        plan.Steps.Should().OnlyContain(s => s.Status == StepStatus.Completed);
        notified.Should().BeEmpty();
    }
}
