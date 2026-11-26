using Birko.AI.Providers;
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
    /// from the task's description, its plan, the project's constraints and earlier answers (<see cref="Drake.SupervisorSourcesAsync"/>).
    /// A question about the whole project is not a task's, so it is never answered here.
    /// </summary>
    public sealed class SupervisorLevel : QuotedAnswerLevel
    {
        private readonly Func<string, string, Drake?> _findDrake;

        /// <param name="findDrake">The Drake holding a task: (projectId, taskId) → Drake, or null.</param>
        /// <param name="providerFor">The model the supervisor answers with for a project; null leaves only earlier answers.</param>
        public SupervisorLevel(Func<string, string, Drake?> findDrake, Func<string, ILlmProvider?> providerFor, ILogger? logger = null)
            : base(providerFor, logger)
        {
            _findDrake = findDrake;
        }

        public override QuestionLevel Level => QuestionLevel.Supervisor;

        protected override (string Role, string Sources, string Otherwise) Prompt => (
            "Drake 🐉, the supervisor of one task",
            "the task's description, its plan, the project's constraints, earlier answers",
            "the project's overseer, and maybe a person");

        protected override string SourceLabel => "the task's sources";

        protected override async Task<LevelSources?> SourcesAsync(ChainQuestion question)
        {
            if (string.IsNullOrEmpty(question.TaskId))
                return null;
            var drake = _findDrake(question.ProjectId, question.TaskId);
            var sources = drake == null ? null : await drake.SupervisorSourcesAsync(question.TaskId);
            return sources == null ? null : new LevelSources(sources.Render(), sources.EarlierAnswers);
        }
    }
}
