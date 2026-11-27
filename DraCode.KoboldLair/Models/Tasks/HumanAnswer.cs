namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// A resolved decision as the resumed agent hears it (TASK-022, FEATURE-028): a user message written as from a
    /// person, opening with <see cref="Tag"/>. The tag is how a resumed run tells which answers its restored conversation
    /// already holds, so no "delivered" marker is stored. <see cref="FromTask"/> names the task that asked when the answer
    /// reaches a task depending on it (TASK-175, FEATURE-028 D8); the wording then says it was another task's.
    /// </summary>
    public sealed record HumanAnswer(
        string DecisionId,
        PendingDecisionSource Source,
        string Question,
        string? EscalationType,
        string? EscalationSummary,
        string? Answer,
        bool TimedOut,
        string? AutomaticOutcome,
        string? FromTask = null,
        QuestionLevel? AnsweredBy = null,
        string? AgentAnswer = null)
    {
        public string Tag => TagFor(DecisionId);

        public static string TagFor(string decisionId) => $"[Human answer — decision {decisionId}]";

        public static HumanAnswer From(PendingDecision decision, string? fromTask = null)
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
                decision.Escalation?.Resolution,
                fromTask,
                decision.AnsweredBy,
                decision.IsOverturned ? decision.AgentAnswer : null);
        }

        /// <summary>An agent's answer the person has not overturned (TASK-208): words an agent wrote, not a person.</summary>
        private bool IsAgentsAnswer => AnsweredBy is { } level && level != QuestionLevel.Person && AgentAnswer is null;

        /// <summary>
        /// Whether a resolved decision carries words a person wrote, and so is passed on to the tasks that depend on the
        /// task that asked (FEATURE-028 D8): an answer, typed instructions, an approval's remark or a rejection's reason.
        /// A timeout, "handle it automatically", a bare resume or a bare approval tell another task nothing.
        /// </summary>
        public static bool CarriesPersonsWords(PendingDecision decision)
        {
            if (decision.ResolutionKind != PendingDecisionResolution.Answered || string.IsNullOrWhiteSpace(decision.Resolution))
                return false;
            // An agent's answer is not a person's words, until the person overturns it (TASK-208)
            if (decision.AnsweredBy is { } level && level != QuestionLevel.Person && !decision.IsOverturned)
                return false;
            return decision.Source switch
            {
                PendingDecisionSource.Escalation => decision.Resolution is not (PendingDecisionNotification.HandleAutomatically
                    or PendingDecisionNotification.ResumeWithInstructions),
                PendingDecisionSource.Approval => !AcceptanceReview.IsApproval(decision.Resolution)
                    || AcceptanceReview.Note(decision.Resolution).Length > 0,
                _ => true
            };
        }

        /// <summary>All the wording the agent sees: always as a person, never as a tool.</summary>
        public string ToMessage() => $"{Tag}\n{Body()}";

        private string Body()
        {
            if (FromTask != null)
                return OtherTasksBody();
            switch (Source)
            {
                case PendingDecisionSource.AskHuman:
                    return TimedOut
                        ? $"Nobody answered your ask_human question \"{Question}\" — no answer, timed out. " +
                          "Decide yourself, record the assumption with reflect, and continue."
                        : AgentAnswer != null
                            ? $"A person overturned the answer {AgentAnswerReview.LevelName(AnsweredBy!.Value)} gave to your " +
                              $"ask_human question \"{Question}\" (\"{AgentAnswer}\"). Their answer: {Answer}. Work from theirs, " +
                              "and check what you already did against it."
                            : IsAgentsAnswer
                                ? $"{AgentAnswerReview.LevelName(AnsweredBy!.Value)} answered your ask_human question \"{Question}\": {Answer}"
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

                case PendingDecisionSource.Approval when !TimedOut && Answer != null:
                    if (AcceptanceReview.IsApproval(Answer))
                        return "A person reviewed your finished work and approved it." +
                               (AcceptanceReview.Note(Answer) is { Length: > 0 } remark ? $" Remark: {remark}" : "");
                    return $"A person reviewed your finished work and rejected it: {AcceptanceReview.Note(Answer)}";

                default:
                    return TimedOut
                        ? $"Nobody answered \"{Question}\" — no answer, timed out."
                        : $"A person answered \"{Question}\". Their answer: {Answer}";
            }
        }

        /// <summary>Another task's decision, as the tasks depending on it hear it: only what a person wrote reaches them.</summary>
        private string OtherTasksBody()
        {
            var task = $"another task, \"{FromTask}\"";
            switch (Source)
            {
                case PendingDecisionSource.AskHuman:
                    return $"A person answered a question asked by {task}, which this task depends on: \"{Question}\". " +
                           $"Their answer: {Answer}. It holds for this task too; do not ask it again.";
                case PendingDecisionSource.Escalation:
                    var escalation = string.IsNullOrWhiteSpace(EscalationSummary) ? EscalationType : $"{EscalationType}: {EscalationSummary}";
                    return $"A person reviewed an escalation of {task}, which this task depends on ({escalation}). " +
                           $"Their instructions: {Answer}";
                case PendingDecisionSource.Approval when AcceptanceReview.IsApproval(Answer):
                    return $"A person approved the finished work of {task}, which this task depends on. Remark: {AcceptanceReview.Note(Answer)}";
                case PendingDecisionSource.Approval:
                    return $"A person rejected the finished work of {task}, which this task depends on: {AcceptanceReview.Note(Answer)}";
                default:
                    return $"A person answered \"{Question}\" for {task}, which this task depends on. Their answer: {Answer}";
            }
        }

        private string Outcome() => string.IsNullOrWhiteSpace(AutomaticOutcome) ? "" : $": {AutomaticOutcome}";

        /// <summary>The answers as one block of text, oldest first, separated by blank lines.</summary>
        public static string Compose(IEnumerable<HumanAnswer> answers) =>
            string.Join("\n\n", answers.Select(a => a.ToMessage()));
    }
}
