using Birko.AI;
using Birko.AI.Models;
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
/// promise reopens the task with the unmet criteria (D3) up to <c>MaxAcceptanceReopens</c>, then fails it with a fix
/// task; what no machine can judge parks the task on a person's Approve / Reject (D4); with the switch off nothing
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

    [Fact]
    public async Task At_the_cap_the_task_fails_permanently_and_a_fix_task_names_the_unmet_criterion()
    {
        var (drake, tracker) = NewDrake(maxReopens: 2);
        var task = Working(tracker, AuthTask);
        task.AcceptanceAttempts = 2;
        MissingRefresh();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Failed);
        ErrorClassifier.IsPermanent(task.ErrorMessage).Should().BeTrue("failure recovery must not rerun it unchanged");
        var fix = tracker.GetAllTasks().Should().ContainSingle(t => t.Id != task.Id).Subject;
        fix.Task.Should().Contain("refresh");
        fix.Priority.Should().Be(TaskPriority.Critical, "a missing export breaks whoever imports it");
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
}
