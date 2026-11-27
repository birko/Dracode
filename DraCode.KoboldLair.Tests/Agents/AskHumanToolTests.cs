using System.Text.Json;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Agents;

/// <summary>
/// The Kobold's <c>ask_human</c> tool (TASK-021, FEATURE-028): it validates the question, hands it to Drake, records
/// the call in the plan log, and asks the run to stop after this turn only when the task was parked.
/// </summary>
public class AskHumanToolTests
{
    private readonly KoboldImplementationPlan _plan = new() { ProjectId = "p", TaskId = Guid.NewGuid().ToString() };
    private readonly List<AskHumanQuestion> _asked = new();
    private int _stops;

    private AskHumanTool Tool(AskHumanOutcome outcome) => new(
        _plan, planService: null, logger: null, projectId: "p", taskId: _plan.TaskId, koboldId: Guid.NewGuid(),
        ask: (q, _) => { _asked.Add(q); return Task.FromResult(outcome); },
        stopAfterTurn: () => _stops++);

    /// <summary>The tool's arguments; a question with no recommendation gets one (its first option), as every asker must give (TASK-209).</summary>
    private static Dictionary<string, object> Args(string json)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
        if (!args.ContainsKey("recommended"))
        {
            var first = args.TryGetValue("options", out var o) && o is JsonElement { ValueKind: JsonValueKind.Array } arr && arr.GetArrayLength() > 0
                ? arr[0].GetString()! : "my own guess";
            args["recommended"] = first;
            args["recommended_because"] = "the safest default";
        }
        return args;
    }

    [Fact]
    public async Task A_question_reaches_Drake_parks_the_task_and_is_logged()
    {
        var tool = Tool(new AskHumanOutcome(true, "dec-1", null));

        var result = await tool.ExecuteAsync(".", Args(
            """{"question":"Which database?","options":["SQLite","Postgres"],"allow_free_text":true,"context":"the spec names neither"}"""));

        var q = _asked.Should().ContainSingle().Subject;
        q.Question.Should().Be("Which database?");
        q.Options.Should().Equal("SQLite", "Postgres");
        q.AllowFreeText.Should().BeTrue();
        q.Context.Should().Be("the spec names neither");
        result.Should().Contain("dec-1");
        _stops.Should().Be(1, "a parked task's run stops after this turn");
        _plan.ExecutionLog.Select(e => e.Message).Should().Contain(m => m.StartsWith("ask_human: \"Which database?\""))
            .And.Contain(m => m.Contains("decision dec-1"));
    }

    [Fact]
    public async Task A_question_an_agent_above_answers_returns_the_answer_and_does_not_stop_the_run()
    {
        var tool = Tool(new AskHumanOutcome(false, "dec-2", null, "SQLite", QuestionLevel.Supervisor));

        var result = await tool.ExecuteAsync(".", Args("""{"question":"Which database?","options":["SQLite","Postgres"]}"""));

        result.Should().StartWith("Answered").And.Contain("supervisor").And.Contain("SQLite").And.Contain("Continue");
        _stops.Should().Be(0, "nobody is waited on");
        _plan.ExecutionLog.Select(e => e.Message).Should().Contain(m => m.Contains("answered by the Supervisor level") && m.Contains("SQLite"));
    }

    [Fact]
    public async Task A_refused_question_tells_the_agent_to_decide_and_does_not_stop_the_run()
    {
        var tool = Tool(new AskHumanOutcome(false, null, "the question limit for this task is reached"));

        var result = await tool.ExecuteAsync(".", Args("""{"question":"Which database?"}"""));

        result.Should().StartWith("Not asked").And.Contain("reflect");
        _stops.Should().Be(0);
        _plan.ExecutionLog.Select(e => e.Message).Should().Contain(m => m.Contains("not asked"));
    }

    [Theory]
    [InlineData("""{"question":"  "}""")]
    [InlineData("""{"question":"Pick","allow_free_text":false}""")]
    [InlineData("""{"question":"Pick","options":["a","a"]}""")]
    public async Task An_invalid_question_is_an_error_and_reaches_no_one(string json)
    {
        var tool = Tool(new AskHumanOutcome(true, "dec-1", null));

        var result = await tool.ExecuteAsync(".", Args(json));

        result.Should().StartWith("Error:");
        _asked.Should().BeEmpty();
        _stops.Should().Be(0);
    }

    [Fact]
    public async Task Free_text_is_allowed_by_default_only_without_options()
    {
        var tool = Tool(new AskHumanOutcome(true, "dec-1", null));

        await tool.ExecuteAsync(".", Args("""{"question":"Why?"}"""));
        await tool.ExecuteAsync(".", Args("""{"question":"Pick","options":["a","b"]}"""));

        _asked[0].AllowFreeText.Should().BeTrue();
        _asked[1].AllowFreeText.Should().BeFalse();
    }

    [Theory]
    [InlineData(" SQLite ", true, "SQLite")]
    [InlineData("sqlite", true, "SQLite")]
    [InlineData("MySQL", false, null)]
    [InlineData("", false, null)]
    public void A_choice_answer_must_be_one_of_the_options(string answer, bool accepted, string? stored)
    {
        var q = new AskHumanQuestion("Which database?", new[] { "SQLite", "Postgres" }, AllowFreeText: false, Context: null);

        q.Accepts(answer, out var kept).Should().Be(accepted);
        if (accepted) kept.Should().Be(stored);
    }

    // FEATURE-028 D9 (TASK-176): an answer naming exactly one option resolves to it, stored as the option's full text
    [Theory]
    [InlineData("Slovak", "Slovak (Ahoj, <name>!)")]
    [InlineData("  slovak  ", "Slovak (Ahoj, <name>!)")]
    [InlineData("slovak (ahoj", "Slovak (Ahoj, <name>!)")]
    [InlineData("SLOVAK (AHOJ, <NAME>!)", "Slovak (Ahoj, <name>!)")]
    [InlineData("2", "Slovak (Ahoj, <name>!)")]
    [InlineData(" 1 ", "English (Hello, <name>!)")]
    public void An_answer_naming_one_option_resolves_to_its_full_text(string answer, string stored)
    {
        var q = new AskHumanQuestion("Which language?", ["English (Hello, <name>!)", "Slovak (Ahoj, <name>!)"], AllowFreeText: false, Context: null);

        q.Accepts(answer, out var kept).Should().BeTrue();
        kept.Should().Be(stored);
    }

    [Theory]
    [InlineData("Slov")]            // not ending at a word
    [InlineData("Slovak please")]   // not the start of the option
    [InlineData("3")]               // no third option
    [InlineData("0")]
    public void An_answer_naming_no_option_is_refused(string answer)
    {
        var q = new AskHumanQuestion("Which language?", ["English (Hello, <name>!)", "Slovak (Ahoj, <name>!)"], AllowFreeText: false, Context: null);

        q.Accepts(answer, out _).Should().BeFalse();
        q.Match(answer).Should().Be(new AskHumanQuestion.OptionMatch(null, Ambiguous: false));
    }

    [Fact]
    public void An_answer_naming_two_options_is_refused_as_ambiguous()
    {
        var q = new AskHumanQuestion("Which build?", ["Release (fast)", "Release (debug symbols)", "Debug"], AllowFreeText: false, Context: null);

        q.Accepts("release", out _).Should().BeFalse();
        q.Match("release").Should().Be(new AskHumanQuestion.OptionMatch(null, Ambiguous: true));
        q.Accepts("Debug", out var kept).Should().BeTrue("an option's whole text wins over another option it starts");
        kept.Should().Be("Debug");
    }

    [Fact]
    public void A_label_that_is_a_number_is_matched_as_text_before_as_a_position()
    {
        var q = new AskHumanQuestion("How many workers?", ["2", "4", "8"], AllowFreeText: false, Context: null);

        q.Accepts("2", out var kept).Should().BeTrue();
        kept.Should().Be("2", "the option whose text is \"2\", not the second option");
    }

    [Fact]
    public void On_a_free_text_question_a_matching_answer_resolves_and_anything_else_is_kept_as_typed()
    {
        var q = new AskHumanQuestion("Which language?", ["English (Hello, <name>!)", "Slovak (Ahoj, <name>!)"], AllowFreeText: true, Context: null);

        q.Accepts("slovak", out var option).Should().BeTrue();
        option.Should().Be("Slovak (Ahoj, <name>!)");
        q.Accepts("Slovak, but formal: Dobrý deň", out var typed).Should().BeTrue();
        typed.Should().Be("Slovak, but formal: Dobrý deň");
    }

    [Fact]
    public void Free_text_passes_through_verbatim()
    {
        var q = new AskHumanQuestion("Which database?", new[] { "SQLite" }, AllowFreeText: true, Context: null);

        q.Accepts("  whatever is in the monorepo  ", out var kept).Should().BeTrue();
        kept.Should().Be("  whatever is in the monorepo  ");
        q.Accepts(" SQLite", out var option).Should().BeTrue();
        option.Should().Be("SQLite", "an answer naming an option is stored as that option");
    }

    [Theory]
    [InlineData("question")]
    [InlineData("context")]
    [InlineData("options")]
    public async Task An_oversized_question_is_an_error(string field)
    {
        var tool = Tool(new AskHumanOutcome(true, "dec-1", null));
        var args = new Dictionary<string, object> { ["question"] = "Pick one", ["recommended"] = "x", ["recommended_because"] = "y" };
        if (field == "question") args["question"] = new string('q', 1001);
        if (field == "context") args["context"] = new string('c', 2001);
        if (field == "options") args["options"] = Enumerable.Range(0, 11).Select(i => $"o{i}").ToArray();

        (await tool.ExecuteAsync(".", args)).Should().StartWith("Error:");
        _asked.Should().BeEmpty();
    }

    [Fact]
    public async Task The_plan_log_entry_is_one_line()
    {
        var tool = Tool(new AskHumanOutcome(true, "dec-1", null));

        await tool.ExecuteAsync(".", Args("""{"question":"Which\ndatabase?","context":"line one\nline two"}"""));

        _plan.ExecutionLog.Select(e => e.Message).Should().OnlyContain(m => !m.Contains('\n'));
    }
}
