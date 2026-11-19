using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Agents;

/// <summary>
/// What a resumed agent reads for a resolved decision (TASK-022): written as from a person, tagged with the decision,
/// and never as if an answer had arrived when nobody answered.
/// </summary>
public class HumanAnswerTests
{
    private static HumanAnswer Asked(string? answer, bool timedOut = false) =>
        new("dec-1", PendingDecisionSource.AskHuman, "Which database?", null, null, answer, timedOut, null);

    private static HumanAnswer Escalated(string? answer, bool timedOut = false, string? outcome = null) =>
        new("dec-2", PendingDecisionSource.Escalation, "Wrong agent", "WrongAgentType", "Wrong agent", answer, timedOut, outcome);

    [Fact]
    public void A_timed_out_question_says_nobody_answered()
    {
        var text = Asked(null, timedOut: true).ToMessage();

        text.Should().StartWith("[Human answer — decision dec-1]").And.Contain("no answer, timed out")
            .And.Contain("reflect").And.NotContain("Their answer");
    }

    [Fact]
    public void A_bare_resume_choice_says_no_instructions_were_given()
    {
        Escalated(PendingDecisionNotification.ResumeWithInstructions).ToMessage()
            .Should().Contain("Resume; no further instructions given.");
    }

    [Fact]
    public void Handle_it_automatically_names_what_the_automatic_route_did()
    {
        Escalated(PendingDecisionNotification.HandleAutomatically, outcome: "Task reset to Unassigned").ToMessage()
            .Should().Contain("chose to have it handled automatically: Task reset to Unassigned");
    }

    [Fact]
    public void Several_answers_compose_oldest_first_each_with_its_tag()
    {
        var text = HumanAnswer.Compose(new[] { Asked("Postgres"), Escalated(null, timedOut: true) });

        text.IndexOf("decision dec-1", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("decision dec-2", StringComparison.Ordinal));
    }
}
