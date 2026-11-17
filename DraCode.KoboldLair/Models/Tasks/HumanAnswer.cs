namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// A resolved decision as the resumed agent hears it (TASK-022, FEATURE-028): a user message written as from a
    /// person, opening with <see cref="Tag"/>. The tag is how a resumed run tells which answers its restored conversation
    /// already holds, so no "delivered" marker is stored.
    /// </summary>
    public sealed record HumanAnswer(
        string DecisionId,
        PendingDecisionSource Source,
        string Question,
        string? EscalationType,
        string? EscalationSummary,
        string? Answer,
        bool TimedOut,
        string? AutomaticOutcome)
    {
        public string Tag => TagFor(DecisionId);

        public static string TagFor(string decisionId) => $"[Human answer — decision {decisionId}]";

        public static HumanAnswer From(PendingDecision decision)
        {
            var timedOut = decision.ResolutionKind == PendingDecisionResolution.TimedOut;
            return new HumanAnswer(
                decision.Id,
                decision.Source,
                PendingDecisionNotification.QuestionText(decision),
                decision.Escalation?.Type.ToString(),
                decision.Escalation?.Summary,
                timedOut ? null : decision.Resolution,
                timedOut,
                decision.Escalation?.Resolution);
        }

        /// <summary>All the wording the agent sees: always as a person, never as a tool.</summary>
        public string ToMessage() => $"{Tag}\n{Body()}";

        private string Body()
        {
            switch (Source)
            {
                case PendingDecisionSource.AskHuman:
                    return TimedOut
                        ? $"Nobody answered your ask_human question \"{Question}\" — no answer, timed out. " +
                          "Decide yourself, record the assumption with reflect, and continue."
                        : $"A person answered your ask_human question \"{Question}\". Their answer: {Answer}";

                case PendingDecisionSource.Escalation when EscalationType != null:
                    var escalation = string.IsNullOrWhiteSpace(EscalationSummary)
                        ? EscalationType
                        : $"{EscalationType}: {EscalationSummary}";
                    if (TimedOut)
                        return $"Nobody answered your escalation ({escalation}) — no answer, timed out. " +
                               $"It was handled automatically{Outcome()}.";
                    if (Answer == PendingDecisionNotification.HandleAutomatically)
                        return $"A person reviewed your escalation ({escalation}) and chose to have it handled automatically{Outcome()}.";
                    return $"A person reviewed your escalation ({escalation}). Their instructions: " +
                           (Answer == PendingDecisionNotification.ResumeWithInstructions
                               ? "Resume; no further instructions given."
                               : Answer);

                default:
                    return TimedOut
                        ? $"Nobody answered \"{Question}\" — no answer, timed out."
                        : $"A person answered \"{Question}\". Their answer: {Answer}";
            }
        }

        private string Outcome() => string.IsNullOrWhiteSpace(AutomaticOutcome) ? "" : $": {AutomaticOutcome}";

        /// <summary>The answers as one block of text, oldest first, separated by blank lines.</summary>
        public static string Compose(IEnumerable<HumanAnswer> answers) =>
            string.Join("\n\n", answers.Select(a => a.ToMessage()));
    }
}
