using System.Text;
using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Read-only: the project's decisions, answered and open, for the project's overseer (TASK-204, FEATURE-078 D25).
    /// </summary>
    public class ProjectDecisionsTool : Tool
    {
        private readonly Func<Task<IReadOnlyList<PendingDecision>>> _decisions;

        public ProjectDecisionsTool(Func<Task<IReadOnlyList<PendingDecision>>> decisions)
        {
            _decisions = decisions;
        }

        public override string Name => "project_decisions";

        public override string Description =>
            "List the project's decisions: the questions asked of people or agents above, with their answers, and the ones still open. Read only.";

        public override object? InputSchema => new { type = "object", properties = new { } };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> arguments,
            CancellationToken cancellationToken = default)
        {
            var decisions = await _decisions();
            if (decisions.Count == 0)
                return "No decisions recorded for this project.";

            var text = new StringBuilder();
            foreach (var d in decisions.OrderBy(d => d.CreatedAt))
            {
                var question = PendingDecisionNotification.QuestionText(d);
                var state = d.ResolutionKind switch
                {
                    PendingDecisionResolution.Answered => $"answered: {d.Resolution}",
                    PendingDecisionResolution.TimedOut => "timed out, no answer",
                    _ => "open"
                };
                var scope = string.IsNullOrEmpty(d.TaskId) ? "project" : $"task {d.TaskId[..Math.Min(8, d.TaskId.Length)]}";
                text.AppendLine($"- [{scope}] {question} — {state}");
            }
            return text.ToString();
        }
    }
}
