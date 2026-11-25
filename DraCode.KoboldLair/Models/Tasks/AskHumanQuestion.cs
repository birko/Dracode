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
        /// Whether <paramref name="answer"/> is acceptable (D5): an answer naming one option (<see cref="Match"/>, D9) is
        /// kept as that option's full text; anything else passes verbatim only when free text is allowed, which an open
        /// question always does. An empty answer is never accepted.
        /// </summary>
        public bool Accepts(string answer, out string stored)
        {
            stored = answer;
            if (string.IsNullOrWhiteSpace(answer))
                return false;

            if (Match(answer).Option is { } option)
            {
                stored = option;
                return true;
            }

            return AllowFreeText || Options.Length == 0;
        }

        /// <summary>The option an answer names, or none; <see cref="OptionMatch.Ambiguous"/> when it names more than one.</summary>
        public sealed record OptionMatch(string? Option, bool Ambiguous);

        /// <summary>
        /// The option a typed answer names (FEATURE-028 D9), tried in this order: its exact text; its whole text in any
        /// letter case; its number as listed, from 1; the start of its text ending at a word ("Slovak" for
        /// "Slovak (Ahoj, &lt;name&gt;!)"). An answer that names two options at one step names none, and is ambiguous.
        /// </summary>
        public OptionMatch Match(string answer)
        {
            var typed = answer.Trim();
            if (typed.Length == 0 || Options.Length == 0)
                return new OptionMatch(null, false);

            if (Options.FirstOrDefault(o => o == typed) is { } exact)
                return new OptionMatch(exact, false);

            var whole = One(Options.Where(o => string.Equals(o.Trim(), typed, StringComparison.OrdinalIgnoreCase)));
            if (whole != null)
                return whole;

            if (int.TryParse(typed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number))
                return new OptionMatch(number >= 1 && number <= Options.Length ? Options[number - 1] : null, false);

            return One(Options.Where(o => StartsAtWord(o.Trim(), typed))) ?? new OptionMatch(null, false);
        }

        /// <summary>The options as a person reads them, numbered as <see cref="Match"/> takes a number: 1. "A", 2. "B".</summary>
        public string NumberedOptions() => string.Join(", ", Options.Select((o, i) => $"{i + 1}. \"{o}\""));

        /// <summary>The single option of <paramref name="candidates"/>, ambiguous for several, null for none.</summary>
        private static OptionMatch? One(IEnumerable<string> candidates)
        {
            var found = candidates.Take(2).ToList();
            return found.Count switch
            {
                0 => null,
                1 => new OptionMatch(found[0], false),
                _ => new OptionMatch(null, true)
            };
        }

        private static bool StartsAtWord(string option, string typed) =>
            option.Length > typed.Length
            && option.StartsWith(typed, StringComparison.OrdinalIgnoreCase)
            && (!char.IsLetterOrDigit(option[typed.Length]) || !char.IsLetterOrDigit(typed[^1]));
    }

    /// <summary>What Drake did with a question: parked the task on decision <paramref name="DecisionId"/>, or refused.</summary>
    /// <param name="Answer">
    /// An agent's answer on the way up the question chain (TASK-202): the question never reached a person, the task was
    /// not parked, and the asker continues with it at once.
    /// </param>
    /// <param name="AnsweredBy">The level that gave <paramref name="Answer"/>.</param>
    public sealed record AskHumanOutcome(bool Parked, string? DecisionId, string? Refusal, string? Answer = null,
        QuestionLevel? AnsweredBy = null);

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
