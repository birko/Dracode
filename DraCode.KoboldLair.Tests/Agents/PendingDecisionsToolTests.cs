using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents.SubAgents;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Agents;

/// <summary>
/// Warden's <c>pending_decisions</c> lists and answers a person's open decisions when a live prompt was missed
/// (TASK-024, FEATURE-030 D4), limited to the projects the session can see.
/// </summary>
public class PendingDecisionsToolTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private static readonly DecisionDesk.ProjectRef Mine = new("proj-mine", "Shop");

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_pdtool_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _decisions = new SqlPendingDecisionRepository(Path.Combine(_dir, "koboldlair.db"));
        await _decisions.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private async Task<PendingDecision> Asked(string projectId, string question, params string[] options)
    {
        var decision = new PendingDecision
        {
            ProjectId = projectId, TaskId = Guid.NewGuid().ToString(), Source = PendingDecisionSource.AskHuman,
            Question = new AskHumanQuestion(question, options, options.Length == 0, "the spec names neither").ToJson()
        };
        await _decisions.CreateAsync(decision);
        return decision;
    }

    private PendingDecisionsTool Tool() => new(
        new DecisionDesk(_decisions, async (_, id, answer) =>
        {
            var decision = (await _decisions.GetByIdAsync(id))!;
            if (decision.IsResolved)
                return DecisionAnswerResult.AlreadyResolved;
            if (!PendingDecisionNotification.AcceptsAnswer(decision, answer, out var stored))
                return DecisionAnswerResult.InvalidAnswer;
            await _decisions.ResolveAsync(id, PendingDecisionResolution.Answered, stored, DateTime.UtcNow);
            return DecisionAnswerResult.Resolved;
        }),
        () => new[] { Mine });

    private static Dictionary<string, object> Input(params (string Key, object Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public async Task List_shows_each_open_decision_of_the_visible_projects_with_how_to_answer_it()
    {
        var asked = await Asked(Mine.Id, "Which database?", "SQLite", "Postgres");
        await Asked("proj-theirs", "Their secret question?");

        var text = await Tool().ExecuteAsync(_dir, Input(("action", "list")));

        text.Should().Contain(asked.Id).And.Contain("Shop").And.Contain("Which database?")
            .And.Contain("SQLite").And.Contain("Postgres").And.Contain("the spec names neither")
            .And.NotContain("Their secret question?");
    }

    [Fact]
    public async Task List_with_nothing_open_says_so()
    {
        (await Tool().ExecuteAsync(_dir, Input(("action", "list")))).Should().Contain("No open decisions");
    }

    [Fact]
    public async Task Answer_resolves_the_decision_and_a_second_answer_says_nothing_changed()
    {
        var asked = await Asked(Mine.Id, "Which database?", "SQLite", "Postgres");
        var tool = Tool();

        var first = await tool.ExecuteAsync(_dir, Input(("action", "answer"), ("decision_id", asked.Id), ("answer", "Postgres")));
        var second = await tool.ExecuteAsync(_dir, Input(("action", "answer"), ("decision_id", asked.Id), ("answer", "SQLite")));

        first.Should().Contain("will resume");
        second.Should().Contain("already answered").And.Contain("nothing changed");
        (await _decisions.GetByIdAsync(asked.Id))!.Resolution.Should().Be("Postgres");
    }

    [Fact]
    public async Task Answer_to_another_users_decision_reads_as_unknown()
    {
        var theirs = await Asked("proj-theirs", "Their secret question?");

        var text = await Tool().ExecuteAsync(_dir, Input(("action", "answer"), ("decision_id", theirs.Id), ("answer", "yes")));

        text.Should().Contain("no decision").And.NotContain("Their secret question?");
        (await _decisions.GetByIdAsync(theirs.Id))!.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task Answer_without_an_id_or_an_answer_explains_what_is_needed()
    {
        (await Tool().ExecuteAsync(_dir, Input(("action", "answer"), ("answer", "x"))))
            .Should().Contain("decision_id");
        (await Tool().ExecuteAsync(_dir, Input(("action", "answer"), ("decision_id", "dec-1"))))
            .Should().Contain("answer");
    }

    private sealed class NoReplyProvider : ILlmProvider
    {
        public string Name => "none";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public void Warden_given_the_tool_offers_pending_decisions()
    {
        var warden = new WardenAgent(new NoReplyProvider(), new AgentOptions { Verbose = false },
            pendingDecisionsTool: Tool());

        warden.Tools.Select(t => t.Name).Should().Contain("pending_decisions");
    }
}
