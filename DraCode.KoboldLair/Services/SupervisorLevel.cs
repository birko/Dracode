using System.Text.RegularExpressions;
using Birko.AI;
using Birko.AI.Providers;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>What a supervisor owns for one task (TASK-203, FEATURE-078 D18): the sources it may answer from.</summary>
    public sealed record SupervisorSources(
        string TaskDescription,
        string? Plan,
        IReadOnlyList<string> Constraints,
        IReadOnlyList<(string Question, string Answer)> EarlierAnswers)
    {
        /// <summary>The sources as the answering agent reads them, and as a quote is checked against.</summary>
        public string Render()
        {
            var text = $"### Task\n{TaskDescription}\n";
            if (!string.IsNullOrWhiteSpace(Plan))
                text += $"\n### Plan\n{Plan}\n";
            if (Constraints.Count > 0)
                text += "\n### Project constraints\n" + string.Join("\n", Constraints.Select(c => $"- {c}")) + "\n";
            if (EarlierAnswers.Count > 0)
                text += "\n### Earlier answers\n" + string.Join("\n", EarlierAnswers.Select(a => $"- Q: {a.Question}\n  A: {a.Answer}")) + "\n";
            return text;
        }
    }

    /// <summary>
    /// The supervisor's level of the question chain (TASK-203, FEATURE-078 D18): Drake answers a worker's question only
    /// from the task's description, its plan, the project's constraints and earlier answers. A question this task (or one
    /// it depends on) already had answered is answered from that record without a model; otherwise a
    /// <see cref="SupervisorAnswerAgent"/> answers, and its answer is kept only when it is acceptable for the question and
    /// its quote is found in the sources. Anything else goes up. A question about the whole project is not a task's, so it
    /// is never answered here.
    /// </summary>
    public sealed class SupervisorLevel : IQuestionLevel
    {
        private readonly Func<string, string, Drake?> _findDrake;
        private readonly Func<string, ILlmProvider?> _providerFor;
        private readonly ILogger? _logger;

        /// <param name="findDrake">The Drake holding a task: (projectId, taskId) → Drake, or null.</param>
        /// <param name="providerFor">The model the supervisor answers with for a project; null leaves only earlier answers.</param>
        public SupervisorLevel(Func<string, string, Drake?> findDrake, Func<string, ILlmProvider?> providerFor, ILogger? logger = null)
        {
            _findDrake = findDrake;
            _providerFor = providerFor;
            _logger = logger;
        }

        public QuestionLevel Level => QuestionLevel.Supervisor;

        public async Task<LevelAnswer?> TryAnswerAsync(ChainQuestion question, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(question.TaskId))
                return null;
            var drake = _findDrake(question.ProjectId, question.TaskId);
            var sources = drake == null ? null : await drake.SupervisorSourcesAsync(question.TaskId);
            if (sources == null)
                return null;

            var asked = question.Question;
            var earlier = sources.EarlierAnswers.FirstOrDefault(a => Same(a.Question, asked.Question));
            if (earlier != default && asked.Accepts(earlier.Answer, out var repeated))
                return new LevelAnswer(repeated, $"the same question was answered before: \"{earlier.Answer}\"");

            var provider = _providerFor(question.ProjectId);
            if (provider == null)
                return null;
            var agent = new SupervisorAnswerAgent(provider, new AgentOptions { Verbose = false });
            var rendered = sources.Render();
            var reply = await agent.AnswerAsync(Describe(asked), rendered, cancellationToken);
            if (reply is not { } r)
                return null;
            if (!Normalize(rendered).Contains(Normalize(r.Quote)))
            {
                _logger?.LogInformation("Supervisor answer for task {TaskId} dropped: its quote is not in the sources", question.TaskId);
                return null;
            }
            if (!asked.Accepts(r.Answer, out var stored))
            {
                _logger?.LogInformation("Supervisor answer for task {TaskId} dropped: \"{Answer}\" is not an option", question.TaskId, r.Answer);
                return null;
            }
            return new LevelAnswer(stored, $"the task's sources say: \"{r.Quote}\"");
        }

        private static string Describe(AskHumanQuestion q) =>
            q.Question
            + (q.Options.Length > 0 ? $"\nOptions: {string.Join(" | ", q.Options)}" + (q.AllowFreeText ? " (or another answer)" : " (one of these only)") : "")
            + (string.IsNullOrWhiteSpace(q.Context) ? "" : $"\nWhy the worker asks: {q.Context}");

        private static bool Same(string a, string b) => Normalize(a) == Normalize(b);

        private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim().Trim('"', '\'', '.', ' ').ToLowerInvariant();
    }
}
