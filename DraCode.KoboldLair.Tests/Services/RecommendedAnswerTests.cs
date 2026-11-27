using System.Text.Json;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Every question that reaches the person carries a recommended answer and its one-line reason, so agreeing is one action
/// (TASK-209, FEATURE-078 D22): the asker must give one the question accepts, the card carries it, and the person (or
/// Warden on their word) can accept one recommendation or every open one at once.
/// </summary>
public class RecommendedAnswerTests : IAsyncLifetime
{
    private static readonly DecisionDesk.ProjectRef Mine = new("proj-mine", "Shop");
    private string _dir = null!;
    private SqlPendingDecisionRepository _decisions = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_recommend_{Guid.NewGuid():N}");
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

    [Theory]
    [InlineData(null, "simplest", "recommended is required")]
    [InlineData("SQLite", null, "recommended_because is required")]
    [InlineData("MySQL", "simplest", "recommended must be one of the options")]
    public void A_question_without_an_acceptable_recommendation_and_a_reason_is_refused(string? recommended, string? because, string problem)
    {
        AskHumanTool.Problem("Which database?", ["SQLite", "Postgres"], false, null, recommended, because, out _)
            .Should().StartWith(problem);
    }

    [Fact]
    public void A_recommendation_naming_an_option_by_its_number_or_start_is_stored_as_the_option()
    {
        AskHumanTool.Problem("Which database?", ["SQLite", "Postgres"], false, null, "2", "it scales", out var stored).Should().BeNull();
        stored.Should().Be("Postgres");
        AskHumanTool.Problem("Which name?", [], true, null, "Greeter", "short", out stored).Should().BeNull();
        stored.Should().Be("Greeter");
    }

    private async Task<PendingDecision> Asked(string question, string[] options, string? recommended, string? because)
    {
        var decision = new PendingDecision
        {
            ProjectId = Mine.Id, TaskId = Guid.NewGuid().ToString(), Source = PendingDecisionSource.AskHuman,
            Question = new AskHumanQuestion(question, options, options.Length == 0, null, recommended, because).ToJson()
        };
        await _decisions.CreateAsync(decision);
        return decision;
    }

    [Fact]
    public async Task The_card_carries_the_recommendation_and_its_reason()
    {
        var decision = await Asked("Which database?", ["SQLite", "Postgres"], "SQLite", "one file to back up");

        var metadata = PendingDecisionNotification.ToMetadata(decision);

        metadata[PendingDecisionNotification.Recommended].Should().Be("SQLite");
        metadata[PendingDecisionNotification.RecommendedBecause].Should().Be("one file to back up");
    }

    [Fact]
    public void An_agents_answer_offered_for_review_recommends_keeping_it_for_the_agents_quoted_reason()
    {
        var decision = new PendingDecision
        {
            ProjectId = Mine.Id, TaskId = "t", Source = PendingDecisionSource.AskHuman,
            Question = new AskHumanQuestion("Which database?", ["SQLite", "Postgres"], false, null, "SQLite", "simplest").ToJson(),
            Resolution = "Postgres", ResolutionKind = PendingDecisionResolution.Answered, AnsweredBy = QuestionLevel.Dragon,
            AgentAnswer = "Postgres", AnswerReason = "The specification says: data lives in Postgres."
        };

        var metadata = PendingDecisionNotification.ToMetadata(decision);

        metadata[PendingDecisionNotification.Recommended].Should().Be(AgentAnswerReview.Keep);
        metadata[PendingDecisionNotification.RecommendedBecause].Should().Be("Dragon quoted: The specification says: data lives in Postgres.");
    }

    [Fact]
    public void An_escalation_and_a_question_asked_before_recommendations_carry_none()
    {
        var escalation = new PendingDecision { ProjectId = Mine.Id, TaskId = "t", Source = PendingDecisionSource.Escalation };
        var older = new PendingDecision
        {
            ProjectId = Mine.Id, TaskId = "t", Source = PendingDecisionSource.AskHuman,
            Question = """{"question":"Which?","options":["a","b"],"allowFreeText":false}"""
        };

        PendingDecisionNotification.Recommendation(escalation).Should().BeNull();
        PendingDecisionNotification.Recommendation(older).Should().BeNull();
        PendingDecisionNotification.ToMetadata(older)[PendingDecisionNotification.Recommended].Should().BeEmpty();
    }

    private DecisionDesk Desk() => new(_decisions, async (_, id, answer) =>
    {
        var decision = (await _decisions.GetByIdAsync(id))!;
        if (decision.IsResolved)
            return DecisionAnswerResult.AlreadyResolved;
        if (!PendingDecisionNotification.AcceptsAnswer(decision, answer, out var stored))
            return DecisionAnswerResult.InvalidAnswer;
        await _decisions.ResolveAsync(id, PendingDecisionResolution.Answered, stored, DateTime.UtcNow);
        return DecisionAnswerResult.Resolved;
    });

    [Fact]
    public async Task Accepting_every_recommendation_answers_each_open_question_with_its_own_and_leaves_the_rest()
    {
        var database = await Asked("Which database?", ["SQLite", "Postgres"], "Postgres", "it scales");
        var name = await Asked("What is the app called?", [], "Notes", "the spec's title");
        var older = await Asked("Which theme?", ["dark", "light"], null, null);

        var replies = await Desk().AcceptRecommendedAsync([Mine]);

        replies.Should().HaveCount(2).And.OnlyContain(r => r.Result == DecisionAnswerResult.Resolved);
        (await _decisions.GetByIdAsync(database.Id))!.Resolution.Should().Be("Postgres");
        (await _decisions.GetByIdAsync(name.Id))!.Resolution.Should().Be("Notes");
        (await _decisions.GetByIdAsync(older.Id))!.IsResolved.Should().BeFalse("a question with no recommendation is left to the person");
    }

    [Fact]
    public async Task Accepting_one_recommendation_answers_only_that_question()
    {
        var database = await Asked("Which database?", ["SQLite", "Postgres"], "SQLite", "simplest");
        var name = await Asked("What is the app called?", [], "Notes", "the spec's title");

        var reply = (await Desk().AcceptRecommendedAsync([Mine], database.Id)).Single();

        reply.Result.Should().Be(DecisionAnswerResult.Resolved);
        (await _decisions.GetByIdAsync(database.Id))!.Resolution.Should().Be("SQLite");
        (await _decisions.GetByIdAsync(name.Id))!.IsResolved.Should().BeFalse();
        (await Desk().AcceptRecommendedAsync([Mine], "nope")).Single().Result.Should().Be(DecisionAnswerResult.NotFound);
    }

    [Fact]
    public async Task Warden_lists_the_recommendation_and_accepts_them_on_the_persons_word()
    {
        var database = await Asked("Which database?", ["SQLite", "Postgres"], "SQLite", "one file to back up");
        var tool = new PendingDecisionsTool(Desk(), () => [Mine]);

        var list = await tool.ExecuteAsync(_dir, new Dictionary<string, object> { ["action"] = "list" });
        list.Should().Contain("Recommended: \"SQLite\" — one file to back up");

        var accepted = await tool.ExecuteAsync(_dir, new Dictionary<string, object> { ["action"] = "accept_recommended" });
        accepted.Should().Contain("Answered \"Which database?\"");
        (await _decisions.GetByIdAsync(database.Id))!.Resolution.Should().Be("SQLite");
    }

    [Fact]
    public void A_question_stored_with_its_recommendation_reads_back_with_it()
    {
        var json = new AskHumanQuestion("Q?", ["a"], false, null, "a", "why").ToJson();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("recommended").GetString().Should().Be("a");
        doc.RootElement.GetProperty("recommendationReason").GetString().Should().Be("why");
    }
}
