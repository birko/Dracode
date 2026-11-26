using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Wyvern answers at the overseer's level (TASK-205, FEATURE-078 D18): from the specification, its analysis and the
/// project's decisions, quoting the line and naming where it came from; anything else goes up to Dragon's level.
/// </summary>
public class OverseerLevelTests : IAsyncLifetime
{
    private const string ProjectId = "proj-overseer";
    private string _dir = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private readonly ScriptedProvider _model = new();

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
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_overseerlevel_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "specification.md"), "# Notes\n\nA notes CLI.\n");
        File.WriteAllText(Path.Combine(_dir, "analysis.md"),
            "# Analysis\n\n## storage\n- [storage-1] Notes store: keeps notes as one JSON file per note under ./notes\n");
        _decisions = new SqlPendingDecisionRepository(Path.Combine(_dir, "koboldlair.db"));
        await _decisions.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private QuestionChain Chain() =>
        new QuestionChain().Register(new OverseerLevel(_ => _dir, _decisions, _ => _model));

    private static ChainQuestion FromWorker(string question) =>
        new(ProjectId, "t-1", QuestionLevel.Worker, new AskHumanQuestion(question, [], true, null));

    private static string Reply(string? answer, string quote) =>
        $"{{\"answer\": {(answer is null ? "null" : $"\"{answer}\"")}, \"quote\": \"{quote}\"}}";

    [Fact]
    public async Task A_question_the_analysis_settles_is_answered_by_Wyvern_naming_its_source()
    {
        _model.Replies.Enqueue(Reply("One JSON file per note under ./notes", "keeps notes as one JSON file per note under ./notes"));

        var outcome = await Chain().AskAsync(FromWorker("How does the notes store keep notes?"));

        outcome.AnsweredBy.Should().Be(QuestionLevel.Overseer);
        outcome.Answer!.Answer.Should().Be("One JSON file per note under ./notes");
        outcome.Answer.Reason.Should().Contain("analysis").And.Contain("one JSON file per note");
        outcome.Path.Should().Equal(QuestionLevel.Supervisor, QuestionLevel.Overseer);
    }

    [Theory]
    [InlineData("SQLite", "Use SQLite")]   // not in the sources
    [InlineData(null, "")]                  // the sources are silent
    public async Task A_question_the_sources_do_not_settle_goes_up(string? answer, string quote)
    {
        _model.Replies.Enqueue(Reply(answer, quote));

        var outcome = await Chain().AskAsync(FromWorker("Which database?"));

        outcome.ReachesPerson.Should().BeTrue();
        outcome.Path.Should().Equal(QuestionLevel.Supervisor, QuestionLevel.Overseer, QuestionLevel.Dragon, QuestionLevel.Person);
    }

    [Fact]
    public async Task A_question_already_decided_in_the_project_is_answered_from_the_record_without_a_model()
    {
        await _decisions.CreateAsync(new PendingDecision
        {
            ProjectId = ProjectId, TaskId = "", Source = PendingDecisionSource.AskHuman,
            Question = new AskHumanQuestion("Which port?", [], true, null).ToJson(),
            Resolution = "8080", ResolutionKind = PendingDecisionResolution.Answered, ResolvedAt = DateTime.UtcNow
        });

        var outcome = await Chain().AskAsync(FromWorker("Which port?"));

        outcome.AnsweredBy.Should().Be(QuestionLevel.Overseer);
        outcome.Answer!.Answer.Should().Be("8080");
        _model.Calls.Should().Be(0);
    }
}
