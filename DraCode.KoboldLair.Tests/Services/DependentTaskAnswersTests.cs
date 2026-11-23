using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
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
/// A person's answer reaches the tasks that depend on the task that asked (TASK-175, FEATURE-028 D8): every dependent,
/// directly or through other tasks and in any area, hears what a person wrote, named as another task's decision, so the
/// same question is not asked twice. Timeouts and "handle it automatically" carry no words from a person and stay with
/// the task that asked.
/// </summary>
public class DependentTaskAnswersTests : IAsyncLifetime
{
    private string _dir = null!;
    private string _tasksDir = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private const string ProjectId = "proj-deps";

    private sealed class FixedPolicy(EscalationDisposition disposition, TimeSpan? timeout = null) : IEscalationDispositionPolicy
    {
        public EscalationRoute Choose(EscalationAlert alert, TaskRecord? task) => new(disposition, timeout);
    }

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_deps_{Guid.NewGuid():N}");
        _tasksDir = Path.Combine(_dir, "tasks");
        Directory.CreateDirectory(_tasksDir);
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

    private (Drake drake, TaskTracker tracker, string path) NewDrake(string area, IEscalationDispositionPolicy? policy = null)
    {
        var tracker = new TaskTracker();
        var path = Path.Combine(_tasksDir, $"{area}-tasks.md");
        var drake = new Drake(_kobolds, tracker, path, "ollama", projectId: ProjectId, pendingDecisions: _decisions,
            dispositionPolicy: policy);
        return (drake, tracker, path);
    }

    private static TaskRecord Add(TaskTracker tracker, string text, params string[] dependsOn)
    {
        var task = tracker.AddTask(text, dependencies: dependsOn.ToList());
        tracker.UpdateTask(task, TaskStatus.Working, "javascript");
        return task;
    }

    /// <summary>The task asks, a person answers, the task finishes.</summary>
    private static async Task<string> AskedAndAnswered(Drake drake, TaskTracker tracker, TaskRecord task, string answer)
    {
        var asked = await drake.AskHumanAsync(task,
            new AskHumanQuestion("Which greeting language?", ["English", "Slovak"], AllowFreeText: false, Context: "the spec leaves it to the owner"));
        asked.Parked.Should().BeTrue();
        (await drake.ResolvePendingDecisionAsync(asked.DecisionId!, answer)).Should().Be(DecisionAnswerResult.Resolved);
        tracker.UpdateTask(task, TaskStatus.Done, "javascript");
        return asked.DecisionId!;
    }

    private static EscalationAlert Alert(TaskRecord task) => new()
    {
        ProjectId = ProjectId, TaskId = task.Id, AgentType = "javascript", Type = EscalationType.WrongApproach, Summary = "Two ways to do it"
    };

    [Fact]
    public async Task A_dependent_task_hears_the_answer_to_its_dependencys_question_named_as_that_tasks()
    {
        var (drake, tracker, _) = NewDrake("backend");
        var question = Add(tracker, "[decision-1] Ask owner to choose greeting language");
        var dependent = Add(tracker, "[backend-1] Implement greeter.js using owner-selected language", "decision-1");
        var unrelated = Add(tracker, "[backend-2] Add a logger");
        var decisionId = await AskedAndAnswered(drake, tracker, question, "Slovak");

        var answer = (await drake.LoadHumanAnswersAsync(dependent.Id)).Should().ContainSingle(
            "the person decided it once; the dependent task must not ask again").Subject;

        answer.Tag.Should().Be(HumanAnswer.TagFor(decisionId), "attributed to the decision, as the asking task hears it");
        answer.ToMessage().Should().Contain("A person answered a question asked by another task")
            .And.Contain("decision-1").And.Contain("Which greeting language?").And.Contain("Their answer: Slovak")
            .And.NotContain("your ask_human question", "the dependent never asked it");
        (await drake.LoadHumanAnswersAsync(unrelated.Id)).Should().BeEmpty("only dependents hear another task's answer");
        (await drake.LoadHumanAnswersAsync(question.Id)).Should().ContainSingle()
            .Which.ToMessage().Should().Contain("your ask_human question", "the asking task still hears it as its own");
    }

    [Fact]
    public async Task The_answer_reaches_a_task_two_steps_away_in_another_area()
    {
        var (backend, backendTasks, backendPath) = NewDrake("backend");
        var question = Add(backendTasks, "[decision-1] Ask owner to choose greeting language");
        var middle = Add(backendTasks, "[backend-1] Implement greeter.js", "decision-1");
        var decisionId = await AskedAndAnswered(backend, backendTasks, question, "Slovak");
        backendTasks.UpdateTask(middle, TaskStatus.Done, "javascript");
        await backendTasks.SaveToFileAsync(backendPath);

        var (frontend, frontendTasks, _) = NewDrake("frontend");
        var far = Add(frontendTasks, "[ui-1] Show the greeting on the page", "backend-1");

        var answer = (await frontend.LoadHumanAnswersAsync(far.Id)).Should().ContainSingle(
            "a chain of dependencies must not lose the answer after one step").Subject;
        answer.Tag.Should().Be(HumanAnswer.TagFor(decisionId));
        answer.ToMessage().Should().Contain("decision-1").And.Contain("Their answer: Slovak");
    }

    [Fact]
    public async Task Only_what_a_person_wrote_is_passed_on()
    {
        var (drake, tracker, _) = NewDrake("backend", new FixedPolicy(EscalationDisposition.HumanDecisionRequired, TimeSpan.FromMinutes(5)));
        var timedOut = Add(tracker, "[api-1] Pick the storage engine");
        var automatic = Add(tracker, "[api-2] Pick the cache");
        var instructed = Add(tracker, "[api-3] Pick the queue");
        var dependent = Add(tracker, "[api-4] Wire it all together", "api-1", "api-2", "api-3");

        await drake.HandleEscalationAsync(Alert(timedOut));
        var expiring = (await _decisions.GetUnresolvedForTaskAsync(timedOut.Id))!;
        await drake.ExpirePendingDecisionsAsync(expiring.CreatedAt.AddHours(1));

        await drake.HandleEscalationAsync(Alert(automatic));
        await drake.ResolvePendingDecisionAsync((await _decisions.GetUnresolvedForTaskAsync(automatic.Id))!.Id,
            PendingDecisionNotification.HandleAutomatically);

        await drake.HandleEscalationAsync(Alert(instructed));
        var instructedId = (await _decisions.GetUnresolvedForTaskAsync(instructed.Id))!.Id;
        await drake.ResolvePendingDecisionAsync(instructedId, "Use RabbitMQ, the team already runs it");

        foreach (var t in new[] { timedOut, automatic, instructed })
            tracker.UpdateTask(t, TaskStatus.Done, "javascript");

        var answer = (await drake.LoadHumanAnswersAsync(dependent.Id)).Should().ContainSingle(
            "a timeout and \"handle it automatically\" carry no words from a person").Subject;
        answer.Tag.Should().Be(HumanAnswer.TagFor(instructedId));
        answer.ToMessage().Should().Contain("another task").And.Contain("api-3")
            .And.Contain("Use RabbitMQ, the team already runs it").And.NotContain("your escalation");
        (await drake.LoadHumanAnswersAsync(timedOut.Id)).Should().ContainSingle("the asking task still hears its own timeout");
    }
}
