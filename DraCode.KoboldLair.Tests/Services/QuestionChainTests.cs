using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The question chain (TASK-201, FEATURE-078 D18, D24, D27): a question climbs from the level above its asker, only up,
/// each level once, the person at the end; the first level that answers stops it, and the answer goes back down the
/// path it climbed. With no level registered, every question reaches the person.
/// </summary>
public class QuestionChainTests
{
    private static readonly AskHumanQuestion Asked = new("Which storage?", ["sqlite", "files"], false, null);

    /// <summary>A level that answers or passes, and records every question and answer it was given.</summary>
    private sealed class TestLevel(QuestionLevel level, string? answer = null, bool fails = false, List<QuestionLevel>? order = null) : IQuestionLevel
    {
        public QuestionLevel Level { get; } = level;
        public readonly List<ChainQuestion> Asked = new();
        public readonly List<(string Answer, QuestionLevel By)> Heard = new();

        public Task<LevelAnswer?> TryAnswerAsync(ChainQuestion question, CancellationToken cancellationToken)
        {
            Asked.Add(question);
            if (fails)
                throw new InvalidOperationException("this level is broken");
            return Task.FromResult(answer is null ? null : new LevelAnswer(answer, $"{Level} knows"));
        }

        public Task OnAnsweredAsync(ChainQuestion question, string answer, QuestionLevel answeredBy, CancellationToken cancellationToken)
        {
            Heard.Add((answer, answeredBy));
            order?.Add(Level);
            return Task.CompletedTask;
        }
    }

    private static ChainQuestion From(QuestionLevel askedBy) => new("p-1", askedBy == QuestionLevel.Overseer ? null : "t-1", askedBy, Asked);

    [Theory]
    [InlineData(QuestionLevel.Worker, new[] { QuestionLevel.Supervisor, QuestionLevel.Overseer, QuestionLevel.Dragon, QuestionLevel.Person })]
    [InlineData(QuestionLevel.Overseer, new[] { QuestionLevel.Dragon, QuestionLevel.Person })]
    [InlineData(QuestionLevel.Dragon, new[] { QuestionLevel.Person })]
    public async Task With_no_level_registered_a_question_climbs_every_level_above_its_asker_to_the_person(
        QuestionLevel askedBy, QuestionLevel[] path)
    {
        var outcome = await new QuestionChain().AskAsync(From(askedBy));

        outcome.ReachesPerson.Should().BeTrue();
        outcome.Answer.Should().BeNull();
        outcome.Path.Should().Equal(path);
    }

    [Fact]
    public async Task The_first_level_that_answers_stops_the_climb_and_no_level_above_it_is_asked()
    {
        var supervisor = new TestLevel(QuestionLevel.Supervisor);
        var overseer = new TestLevel(QuestionLevel.Overseer, "sqlite");
        var dragon = new TestLevel(QuestionLevel.Dragon, "files");
        var chain = new QuestionChain().Register(supervisor).Register(overseer).Register(dragon);

        var outcome = await chain.AskAsync(From(QuestionLevel.Worker));

        outcome.AnsweredBy.Should().Be(QuestionLevel.Overseer);
        outcome.Answer!.Answer.Should().Be("sqlite");
        outcome.Path.Should().Equal(QuestionLevel.Supervisor, QuestionLevel.Overseer);
        supervisor.Asked.Should().ContainSingle("each level gets the question once");
        dragon.Asked.Should().BeEmpty("a question never goes past the level that answered it");
    }

    [Fact]
    public async Task A_question_never_moves_down_to_or_below_its_asker()
    {
        var supervisor = new TestLevel(QuestionLevel.Supervisor, "from below");
        var chain = new QuestionChain().Register(supervisor);

        var outcome = await chain.AskAsync(From(QuestionLevel.Overseer));

        supervisor.Asked.Should().BeEmpty();
        outcome.ReachesPerson.Should().BeTrue();
    }

    [Fact]
    public async Task A_level_that_fails_passes_the_question_up()
    {
        var chain = new QuestionChain().Register(new TestLevel(QuestionLevel.Dragon, fails: true));

        var outcome = await chain.AskAsync(From(QuestionLevel.Overseer));

        outcome.ReachesPerson.Should().BeTrue();
    }

    [Fact]
    public async Task The_answer_goes_back_down_the_path_nearest_level_first_and_only_to_levels_below_the_answerer()
    {
        var order = new List<QuestionLevel>();
        var supervisor = new TestLevel(QuestionLevel.Supervisor, order: order);
        var overseer = new TestLevel(QuestionLevel.Overseer, order: order);
        var dragon = new TestLevel(QuestionLevel.Dragon, order: order);
        var chain = new QuestionChain().Register(supervisor).Register(overseer).Register(dragon);
        var outcome = await chain.AskAsync(From(QuestionLevel.Worker));

        await chain.DeliverAnswerAsync(From(QuestionLevel.Worker), outcome.Path, "files", QuestionLevel.Person);

        foreach (var level in new[] { supervisor, overseer, dragon })
            level.Heard.Should().ContainSingle().Which.Should().Be(("files", QuestionLevel.Person));
        order.Should().Equal(QuestionLevel.Dragon, QuestionLevel.Overseer, QuestionLevel.Supervisor);
    }

    [Fact]
    public async Task An_answer_given_on_the_way_goes_down_only_to_the_levels_it_passed_below_the_answerer()
    {
        var supervisor = new TestLevel(QuestionLevel.Supervisor);
        var overseer = new TestLevel(QuestionLevel.Overseer, "sqlite");
        var dragon = new TestLevel(QuestionLevel.Dragon);
        var chain = new QuestionChain().Register(supervisor).Register(overseer).Register(dragon);
        var outcome = await chain.AskAsync(From(QuestionLevel.Worker));

        await chain.DeliverAnswerAsync(From(QuestionLevel.Worker), outcome.Path, "sqlite", outcome.AnsweredBy);

        supervisor.Heard.Should().ContainSingle().Which.Should().Be(("sqlite", QuestionLevel.Overseer));
        overseer.Heard.Should().BeEmpty("the answerer already knows");
        dragon.Heard.Should().BeEmpty("the question never reached Dragon");
    }

    [Fact]
    public void The_person_cannot_be_registered_as_a_level()
    {
        var register = () => new QuestionChain().Register(new TestLevel(QuestionLevel.Person, "me"));

        register.Should().Throw<ArgumentException>();
    }
}
