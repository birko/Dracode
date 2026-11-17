using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Where a person lists and answers the pending decisions of the projects they can see (TASK-024, FEATURE-030 D4–D6).
    /// Reads the stored decisions, never the notification list, so an open decision is offered until it is answered or
    /// times out. A decision outside the caller's projects reads as unknown, so its existence is not disclosed. Shared by
    /// the <c>/dragon</c> socket and Warden's <c>pending_decisions</c> tool.
    /// </summary>
    public sealed class DecisionDesk
    {
        public sealed record ProjectRef(string Id, string Name);

        public sealed record OpenDecision(ProjectRef Project, PendingDecision Decision);

        /// <summary>What became of an answer, with <see cref="Message"/> saying so in words for a person.</summary>
        public sealed record Reply(string DecisionId, DecisionAnswerResult Result, string Message);

        private readonly SqlPendingDecisionRepository? _store;
        private readonly Func<string, string, string, Task<DecisionAnswerResult>> _resolve;

        /// <param name="store">The decisions; null when the backend keeps none, and then nothing is ever open.</param>
        /// <param name="resolve">Answers a decision: project id, decision id, answer (<c>DrakeFactory.ResolvePendingDecisionAsync</c>).</param>
        public DecisionDesk(SqlPendingDecisionRepository? store, Func<string, string, string, Task<DecisionAnswerResult>> resolve)
        {
            _store = store;
            _resolve = resolve;
        }

        /// <summary>The open decisions of <paramref name="visible"/>, oldest first.</summary>
        public async Task<List<OpenDecision>> OpenAsync(IEnumerable<ProjectRef> visible)
        {
            var open = new List<OpenDecision>();
            if (_store == null)
                return open;
            foreach (var project in visible)
                open.AddRange((await _store.GetUnresolvedForProjectAsync(project.Id)).Select(d => new OpenDecision(project, d)));
            return open.OrderBy(o => o.Decision.CreatedAt).ToList();
        }

        public async Task<Reply> AnswerAsync(IEnumerable<ProjectRef> visible, string decisionId, string answer)
        {
            var decision = _store == null || string.IsNullOrWhiteSpace(decisionId) ? null : await _store.GetByIdAsync(decisionId);
            if (decision == null || !visible.Any(p => p.Id == decision.ProjectId))
                return new Reply(decisionId, DecisionAnswerResult.NotFound,
                    $"There is no decision {decisionId} in your projects; nothing changed.");

            var result = await _resolve(decision.ProjectId, decisionId, answer);
            return new Reply(decisionId, result, result switch
            {
                DecisionAnswerResult.Resolved =>
                    $"Answered \"{PendingDecisionNotification.QuestionText(decision)}\"; the task will resume with your answer.",
                DecisionAnswerResult.AlreadyResolved =>
                    "This decision was already answered or timed out; nothing changed.",
                DecisionAnswerResult.InvalidAnswer =>
                    $"\"{answer}\" is not one of the options: {string.Join(", ", AskHumanQuestion.TryParse(decision)?.Options ?? [])}. " +
                    "The question is still open.",
                _ => $"There is no decision {decisionId} in your projects; nothing changed."
            });
        }
    }
}
