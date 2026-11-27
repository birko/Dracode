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
/// A Kobold's <c>ask_human</c> goes through the question chain (TASK-202, FEATURE-078 D23): from the worker's level it
/// climbs Drake, Wyvern and Dragon; a question no level answers parks the task as before, with its path kept, and the
/// person's answer goes back down it. One a level answers is recorded answered and the task keeps running.
/// </summary>
public class AskHumanChainTests : IAsyncLifetime
{
    private const string ProjectId = "proj-chain";
    private static readonly AskHumanQuestion Asked = new("Which database?", ["SQLite", "Postgres"], false, "the spec names neither");

    private string _dir = null!;
    private SqlProjectRepository _projects = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private readonly List<PendingDecision> _cards = new();

    /// <summary>A level that answers when told to, and records the answers it hears coming back down.</summary>
    private sealed class TestLevel(QuestionLevel level, string? answer = null) : IQuestionLevel
    {
        public QuestionLevel Level { get; } = level;
        public readonly List<ChainQuestion> Asked = new();
        public readonly List<string> Heard = new();

        public Task<LevelAnswer?> TryAnswerAsync(ChainQuestion question, CancellationToken cancellationToken)
        {
            Asked.Add(question);
            return Task.FromResult(answer is null ? null : new LevelAnswer(answer, "the plan says so"));
        }

        public Task OnAnsweredAsync(ChainQuestion question, string answer, QuestionLevel answeredBy, CancellationToken cancellationToken)
        {
            Heard.Add(answer);
            return Task.CompletedTask;
        }
    }

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_askchain_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");
        _projects = new SqlProjectRepository(db);
        await _projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();
        _kobolds = new KoboldFactory(_projects, NullLoggerFactory.Instance, new KoboldLairConfiguration { ProjectsPath = _dir });
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private (Drake Drake, TaskRecord Task) NewDrake(QuestionChain chain)
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId, pendingDecisions: _decisions, onDecisionRequired: (_, d) => _cards.Add(d))
        {
            QuestionChain = chain
        };
        var task = tracker.AddTask("[api-1] Pick the storage engine");
        tracker.UpdateTask(task, TaskStatus.Working, "csharp");
        return (drake, task);
    }

    [Fact]
    public async Task A_question_no_level_answers_climbs_every_level_and_parks_the_task_for_the_person_as_before()
    {
        var supervisor = new TestLevel(QuestionLevel.Supervisor);
        var (drake, task) = NewDrake(new QuestionChain().Register(supervisor));

        var outcome = await drake.AskHumanAsync(task, Asked);

        outcome.Parked.Should().BeTrue();
        outcome.Answer.Should().BeNull();
        supervisor.Asked.Should().ContainSingle(q => q.AskedBy == QuestionLevel.Worker && q.TaskId == task.Id);
        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        var decision = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;
        decision.Path.Should().Equal(QuestionLevel.Supervisor, QuestionLevel.Overseer, QuestionLevel.Dragon, QuestionLevel.Person);
        _cards.Should().ContainSingle(c => c.Id == decision.Id);
    }

    [Fact]
    public async Task A_question_a_level_answers_is_recorded_answered_and_the_task_keeps_running()
    {
        var supervisor = new TestLevel(QuestionLevel.Supervisor);
        var overseer = new TestLevel(QuestionLevel.Overseer, "SQLite");
        var (drake, task) = NewDrake(new QuestionChain().Register(supervisor).Register(overseer));

        var outcome = await drake.AskHumanAsync(task, Asked);

        outcome.Parked.Should().BeFalse();
        outcome.Answer.Should().Be("SQLite");
        outcome.AnsweredBy.Should().Be(QuestionLevel.Overseer);
        task.Status.Should().Be(TaskStatus.Working, "no person is waited on");
        _cards.Should().ContainSingle(c => c.AwaitsReview, "the person gets the agent's answer for information only (TASK-208)");
        (await _decisions.GetUnresolvedForTaskAsync(task.Id)).Should().BeNull();
        var recorded = await _decisions.GetByIdAsync(outcome.DecisionId!);
        recorded!.Resolution.Should().Be("SQLite");
        recorded.Path.Should().Equal(QuestionLevel.Supervisor, QuestionLevel.Overseer);
        supervisor.Heard.Should().Equal(new[] { "SQLite" }, "the levels below the answerer hear the answer");
    }

    [Fact]
    public async Task The_persons_answer_goes_back_down_the_path_to_every_level_it_passed()
    {
        var supervisor = new TestLevel(QuestionLevel.Supervisor);
        var dragon = new TestLevel(QuestionLevel.Dragon);
        var chain = new QuestionChain().Register(supervisor).Register(dragon);
        var config = new KoboldLairConfiguration { ProjectsPath = _dir };
        var providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Microsoft.Extensions.Options.Options.Create(config), Path.Combine(_dir, "user-settings.json"));
        var drakes = new DrakeFactory(_kobolds, providers, config, NullLoggerFactory.Instance, projectRepository: _projects,
            pendingDecisions: _decisions) { Chain = chain };
        var (drake, task) = NewDrake(chain);
        var outcome = await drake.AskHumanAsync(task, Asked);

        // No Drake of the factory holds the task here, so the store records the answer (a held task is resumed by its
        // Drake, as EscalationToHumanTests covers); either way the answer goes back down the path
        (await drakes.ResolvePendingDecisionAsync(ProjectId, outcome.DecisionId!, "2")).Should().Be(DecisionAnswerResult.Resolved);

        supervisor.Heard.Should().Equal("Postgres");
        dragon.Heard.Should().Equal("Postgres");
    }
}
