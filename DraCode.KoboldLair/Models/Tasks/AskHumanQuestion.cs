using System.Text.Json;

namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// A question a Kobold asks a person through <c>ask_human</c> (TASK-021, FEATURE-028 D2), stored as camelCase JSON in
    /// <see cref="PendingDecision.Question"/>.
    /// </summary>
    /// <param name="Question">What the agent asks.</param>
    /// <param name="Options">The choices offered; empty for an open question.</param>
    /// <param name="AllowFreeText">Whether an answer other than an option is accepted.</param>
    /// <param name="Context">Why the agent is asking.</param>
    public sealed record AskHumanQuestion(string Question, string[] Options, bool AllowFreeText, string? Context)
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        /// <summary>The question an <see cref="PendingDecisionSource.AskHuman"/> decision carries; null for any other decision.</summary>
        public static AskHumanQuestion? TryParse(PendingDecision decision)
        {
            if (decision.Source != PendingDecisionSource.AskHuman || string.IsNullOrWhiteSpace(decision.Question))
                return null;
            try
            {
                return JsonSerializer.Deserialize<AskHumanQuestion>(decision.Question, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether <paramref name="answer"/> is acceptable (D5): an answer naming an option (ignoring surrounding
        /// whitespace) is kept as that option; anything else passes verbatim only when free text is allowed, which an
        /// open question always does. An empty answer is never accepted.
        /// </summary>
        public bool Accepts(string answer, out string stored)
        {
            stored = answer;
            if (string.IsNullOrWhiteSpace(answer))
                return false;

            var option = Options.FirstOrDefault(o => string.Equals(o, answer.Trim(), StringComparison.Ordinal));
            if (option != null)
            {
                stored = option;
                return true;
            }

            return AllowFreeText || Options.Length == 0;
        }
    }

    /// <summary>What Drake did with a question: parked the task on decision <paramref name="DecisionId"/>, or refused.</summary>
    public sealed record AskHumanOutcome(bool Parked, string? DecisionId, string? Refusal);

    /// <summary>
    /// How much Kobolds of a project may ask (FEATURE-028; enforcement and configuration are TASK-023's). Until then
    /// <see cref="UnrestrictedAskHumanPolicy"/> allows every question with no cap.
    /// </summary>
    public interface IAskHumanPolicy
    {
        AskHumanLimits For(string projectId);
    }

    /// <param name="Allow">Whether Kobolds may ask at all.</param>
    /// <param name="MaxQuestionsPerTask">How many questions one task may ever ask; null for no cap.</param>
    public sealed record AskHumanLimits(bool Allow = true, int? MaxQuestionsPerTask = null);

    public sealed class UnrestrictedAskHumanPolicy : IAskHumanPolicy
    {
        public static readonly UnrestrictedAskHumanPolicy Instance = new();

        public AskHumanLimits For(string projectId) => new();
    }
}
