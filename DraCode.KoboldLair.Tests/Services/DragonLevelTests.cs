using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
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
/// Dragon's level, the last before the person (TASK-206, FEATURE-078 D19): Dragon answers only from the specification and
/// the decisions already answered in the project, quoting them, never from its own judgement; a question neither settles
/// reaches the person with a note asking them to define it.
/// </summary>
public class DragonLevelTests : IAsyncLifetime
{
    private const string ProjectId = "proj-dragon";
    private const string Spec = "# Notes\n\n## Storage\n\nNotes are kept in one JSON file per note under ./notes.\n";
    private static readonly AskHumanQuestion Storage = new("Where are the notes stored?", [], true, "the plan does not say");

    private string _dir = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private readonly ScriptedProvider _model = new();
    private string? _spec = Spec;

    private sealed class ScriptedProvider : ILlmProvider
    {
        public readonly Queue<string> Replies = new();
        public int Calls;
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new LlmResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock> { new() { Type = "text", Text = Replies.Dequeue() } }
            });
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_dragonlevel_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();
        _kobolds = new KoboldFactory(projects, NullLoggerFactory.Instance, new KoboldLairConfiguration { ProjectsPath = _dir });
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private QuestionChain Chain() =>
        new QuestionChain().Register(new DragonLevel(_ => _spec, _decisions, _ => _model));

    private static ChainQuestion FromOverseer(AskHumanQuestion q) => new(ProjectId, null, QuestionLevel.Overseer, q);

    private static string Reply(string? answer, string quote) =>
        $"{{\"answer\": {(answer is null ? "null" : $"\"{answer}\"")}, \"quote\": \"{quote}\"}}";

    [Fact]
    public async Task A_question_the_specification_settles_is_answered_by_Dragon_quoting_it()
    {
        _model.Replies.Enqueue(Reply("One JSON file per note under ./notes", "Notes are kept in one JSON file per note under ./notes."));

        var outcome = await Chain().AskAsync(FromOverseer(Storage));

        outcome.AnsweredBy.Should().Be(QuestionLevel.Dragon);
        outcome.Answer!.Answer.Should().Be("One JSON file per note under ./notes");
        outcome.Answer.Reason.Should().Contain("Notes are kept in one JSON file per note");
    }

    [Fact]
    public async Task A_question_a_recorded_decision_settles_is_answered_from_it_and_a_repeated_one_needs_no_model()
    {
        _spec = "# Notes\n";
        var port = new AskHumanQuestion("Which port should the server listen on?", [], true, null);
        await _decisions.CreateAsync(new PendingDecision
        {
            ProjectId = ProjectId, TaskId = "", Source = PendingDecisionSource.AskHuman, Question = port.ToJson(),
            Resolution = "8080", ResolutionKind = PendingDecisionResolution.Answered, ResolvedAt = DateTime.UtcNow
        });

        var outcome = await Chain().AskAsync(FromOverseer(port));

        outcome.AnsweredBy.Should().Be(QuestionLevel.Dragon);
        outcome.Answer!.Answer.Should().Be("8080");
        _model.Calls.Should().Be(0, "the same question was answered before in the project");
    }

    [Theory]
    [InlineData("SQLite", "Use SQLite everywhere")]   // a quote that is not in the specification: Dragon's own judgement
    [InlineData(null, "")]                             // the specification is silent
    public async Task A_question_neither_settles_reaches_the_person_asking_them_to_define_it(string? answer, string quote)
    {
        _model.Replies.Enqueue(Reply(answer, quote));

        var outcome = await Chain().AskAsync(FromOverseer(new AskHumanQuestion("Which database?", [], true, null)));

        outcome.ReachesPerson.Should().BeTrue();
        outcome.PersonNote.Should().Be(DragonLevel.DefineNote);
    }

    [Fact]
    public async Task The_card_a_worker_question_raises_asks_the_person_to_define_it()
    {
        _model.Replies.Enqueue(Reply(null, ""));
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId, pendingDecisions: _decisions) { QuestionChain = Chain() };
        var task = tracker.AddTask("[api-1] Store the notes");
        tracker.UpdateTask(task, TaskStatus.Working, "python");

        var outcome = await drake.AskHumanAsync(task, new AskHumanQuestion("Which database?", ["SQLite", "Postgres"], false, "the plan names none"));

        outcome.Parked.Should().BeTrue();
        var asked = AskHumanQuestion.TryParse((await _decisions.GetByIdAsync(outcome.DecisionId!))!)!;
        asked.Question.Should().Be("Which database?");
        asked.Context.Should().Be($"the plan names none {DragonLevel.DefineNote}");
    }

    [Fact]
    public async Task Without_a_specification_or_any_decision_Dragon_holds_nothing_to_answer_from()
    {
        _spec = null;

        var outcome = await Chain().AskAsync(FromOverseer(Storage));

        outcome.ReachesPerson.Should().BeTrue();
        _model.Calls.Should().Be(0);
    }
}
