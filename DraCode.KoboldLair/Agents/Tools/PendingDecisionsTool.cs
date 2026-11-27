using System.Text;
using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Lists and answers the pending decisions of the session's projects (TASK-024, FEATURE-030 D4): the fallback for a
    /// decision card the person missed, and how an answer given in the Dragon chat reaches the waiting task.
    /// </summary>
    public class PendingDecisionsTool : Tool
    {
        private readonly DecisionDesk _desk;
        private readonly Func<IReadOnlyList<DecisionDesk.ProjectRef>> _visibleProjects;

        public PendingDecisionsTool(DecisionDesk desk, Func<IReadOnlyList<DecisionDesk.ProjectRef>> visibleProjects)
        {
            _desk = desk;
            _visibleProjects = visibleProjects;
        }

        public override string Name => "pending_decisions";

        public override string Description =>
            "Questions and escalations that agents are waiting on a person to answer, for the user's projects. " +
            "'list' shows each open decision with its id, question, context and the answers it accepts. " +
            "'answer' answers one (decision_id + answer): use the user's own words, or the option they picked, exactly. " +
            "'accept_recommended' answers with the recommended answer: one (decision_id) or, without it, every open " +
            "decision that has one — only when the user agreed to the recommendation(s). " +
            "The waiting task resumes with the answer. Answering a decision twice changes nothing.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", @enum = new[] { "list", "answer", "accept_recommended" },
                    description = "'list', 'answer' or 'accept_recommended'" },
                decision_id = new { type = "string", description = "The decision to answer (required for 'answer')" },
                answer = new { type = "string", description = "The person's answer: an option's text, or free text where allowed" }
            },
            required = new[] { "action" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input,
            CancellationToken cancellationToken = default)
        {
            var action = Text(input, "action")?.ToLowerInvariant();
            return action switch
            {
                "list" => await ListAsync(),
                "answer" => await AnswerAsync(Text(input, "decision_id"), Text(input, "answer")),
                "accept_recommended" => await AcceptRecommendedAsync(Text(input, "decision_id")),
                _ => "Unknown action. Use 'list', 'answer' or 'accept_recommended'."
            };
        }

        private async Task<string> ListAsync()
        {
            var open = await _desk.OpenAsync(_visibleProjects());
            if (open.Count == 0)
                return "No open decisions: no agent is waiting on an answer.";

            var text = new StringBuilder($"{open.Count} open decision(s):\n");
            foreach (var (project, decision) in open)
            {
                text.Append($"\n- [{project.Name}] decision {decision.Id}: {PendingDecisionNotification.QuestionText(decision)}\n");
                var context = Asked(decision)?.Context ?? decision.Escalation?.Summary;
                if (!string.IsNullOrWhiteSpace(context))
                    text.Append($"  Context: {context}\n");
                text.Append($"  Accepts: {Accepts(decision)}\n");
                if (PendingDecisionNotification.Recommendation(decision) is { } recommendation)
                    text.Append($"  Recommended: \"{recommendation.Answer}\" — {recommendation.Because}\n");
                text.Append(decision.TimeoutAt is { } at
                    ? $"  Times out {at:yyyy-MM-dd HH:mm} UTC, then it is handled without an answer.\n"
                    : "  Waits until answered.\n");
            }
            return text.ToString();
        }

        private static string Accepts(PendingDecision decision)
        {
            if (decision.Source == PendingDecisionSource.Escalation)
                return $"\"{PendingDecisionNotification.HandleAutomatically}\", or written instructions";
            var asked = Asked(decision);
            if (asked is not { Options.Length: > 0 })
                return "free text";
            var options = asked.NumberedOptions();
            return asked.AllowFreeText ? $"{options}, or free text" : $"one of {options} (its text, its start, or its number)";
        }

        /// <summary>The question as the person is asked it: an agent's answer offered for review asks Keep first (TASK-208).</summary>
        private static AskHumanQuestion? Asked(PendingDecision decision) =>
            decision.AwaitsReview ? AgentAnswerReview.Question(decision) : AskHumanQuestion.TryParse(decision);

        private async Task<string> AcceptRecommendedAsync(string? decisionId)
        {
            var replies = await _desk.AcceptRecommendedAsync(_visibleProjects(), string.IsNullOrWhiteSpace(decisionId) ? null : decisionId);
            return replies.Count == 0
                ? "No open decision has a recommended answer; nothing changed."
                : string.Join("\n", replies.Select(r => r.Message));
        }

        private async Task<string> AnswerAsync(string? decisionId, string? answer)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
                return "Give the decision_id to answer ('list' shows them).";
            if (string.IsNullOrWhiteSpace(answer))
                return "Give the answer: the option the person chose, or their own words.";
            return (await _desk.AnswerAsync(_visibleProjects(), decisionId, answer)).Message;
        }

        private static string? Text(Dictionary<string, object> input, string key) =>
            input.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
