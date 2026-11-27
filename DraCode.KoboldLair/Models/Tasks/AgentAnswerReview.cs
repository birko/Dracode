namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>What a person's review of an agent's answer did (TASK-208).</summary>
    public enum AgentAnswerReviewOutcome
    {
        /// <summary>Nothing changed: the decision is unknown, not an agent's answer awaiting review, or the answer was refused.</summary>
        None,
        /// <summary>The person kept the agent's answer.</summary>
        Kept,
        /// <summary>Overturned on a task: the task reopened and its plan is revised with the person's answer.</summary>
        TaskReopened,
        /// <summary>Overturned on the project before any task started: the analysis runs again with the person's answer.</summary>
        Reanalyzing,
        /// <summary>Overturned on the project after work started: recorded for every later run; nothing done is redone.</summary>
        Recorded
    }

    /// <param name="Result">As for any answer to a decision: Resolved, NotFound, AlreadyResolved or InvalidAnswer.</param>
    /// <param name="StartedTasks">For <see cref="AgentAnswerReviewOutcome.Recorded"/>: how many tasks had already started.</param>
    public sealed record AgentAnswerReviewResult(DecisionAnswerResult Result, AgentAnswerReviewOutcome Outcome, int StartedTasks = 0)
    {
        public static readonly AgentAnswerReviewResult NotFound = new(DecisionAnswerResult.NotFound, AgentAnswerReviewOutcome.None);
        public static readonly AgentAnswerReviewResult AlreadyResolved = new(DecisionAnswerResult.AlreadyResolved, AgentAnswerReviewOutcome.None);
        public static readonly AgentAnswerReviewResult InvalidAnswer = new(DecisionAnswerResult.InvalidAnswer, AgentAnswerReviewOutcome.None);
    }

    /// <summary>
    /// An answer an agent gave instead of the person, offered to them for information (TASK-208, FEATURE-078 D21): a card
    /// that keeps the agent's answer or overturns it with the person's own. The card is the question's own, with "Keep
    /// the agent's answer" as its first option and the original options after it.
    /// </summary>
    public static class AgentAnswerReview
    {
        /// <summary>The answer that keeps what the agent said.</summary>
        public const string Keep = "Keep the agent's answer";

        /// <summary>The level as a person knows it.</summary>
        public static string LevelName(QuestionLevel level) => level switch
        {
            QuestionLevel.Supervisor => "Drake (the task's supervisor)",
            QuestionLevel.Overseer => "Wyvern (the project's overseer)",
            QuestionLevel.Dragon => "Dragon",
            QuestionLevel.Worker => "the Kobold",
            _ => "a person"
        };

        /// <summary>
        /// The question the review card asks: the original, with <see cref="Keep"/> first and free text allowed where the
        /// original allowed it (or had no options). Null when the decision is not an asked question.
        /// </summary>
        public static AskHumanQuestion? Question(PendingDecision decision)
        {
            if (AskHumanQuestion.TryParse(decision) is not { } asked)
                return null;
            var options = new[] { Keep }.Concat(asked.Options).ToArray();
            return new AskHumanQuestion(asked.Question, options, asked.AllowFreeText || asked.Options.Length == 0, Context(decision));
        }

        /// <summary>What the card tells the person: who answered, what, why, and what overturning it does.</summary>
        public static string Context(PendingDecision decision)
        {
            var who = LevelName(decision.AnsweredBy ?? QuestionLevel.Person);
            var answer = decision.AgentAnswer ?? decision.Resolution ?? "";
            var why = string.IsNullOrWhiteSpace(decision.AnswerReason) ? "" : $" Its reason: \"{decision.AnswerReason}\".";
            var reopens = decision.TaskId.Length == 0
                ? "the project's analysis runs again with yours if no task has started, otherwise yours is recorded for every later run"
                : "the task reopens and its plan is revised with yours";
            return $"{who} answered this for you: \"{answer}\".{why} Keep it, or give your own answer to overturn it ({reopens}).";
        }

        /// <summary>
        /// Reads the person's answer to the card: false when it is not accepted (not an option, free text not allowed, or
        /// ambiguous). Otherwise <paramref name="keep"/> says whether it keeps the agent's answer, and
        /// <paramref name="overturnWith"/> is the person's answer in its stored form.
        /// </summary>
        public static bool Read(PendingDecision decision, string answer, out bool keep, out string overturnWith)
        {
            keep = false;
            overturnWith = answer;
            var question = Question(decision);
            if (question == null)
            {
                keep = string.Equals(answer.Trim(), Keep, StringComparison.OrdinalIgnoreCase);
                return true;
            }
            if (!question.Accepts(answer, out var stored))
                return false;
            keep = stored == Keep || stored == (decision.AgentAnswer ?? decision.Resolution);
            overturnWith = stored;
            return true;
        }
    }
}
