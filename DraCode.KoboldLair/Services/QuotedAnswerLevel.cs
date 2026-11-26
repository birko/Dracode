using System.Text.RegularExpressions;
using Birko.AI;
using Birko.AI.Providers;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// What a level answers from (FEATURE-078 D18): its sources as one text, and the questions already answered in them.
    /// </summary>
    public sealed record LevelSources(string Text, IReadOnlyList<(string Question, string Answer)> EarlierAnswers);

    /// <summary>
    /// A level of the question chain that answers only from its own sources, quoting them (Drake, TASK-203; Dragon,
    /// TASK-206). A question already answered there is answered from that record without a model; otherwise a
    /// <see cref="QuotedAnswerAgent"/> answers, kept only when its quote is in the sources and the answer is acceptable for
    /// the question. Anything else goes up.
    /// </summary>
    public abstract class QuotedAnswerLevel : IQuestionLevel
    {
        private readonly Func<string, ILlmProvider?> _providerFor;
        protected readonly ILogger? Logger;

        protected QuotedAnswerLevel(Func<string, ILlmProvider?> providerFor, ILogger? logger)
        {
            _providerFor = providerFor;
            Logger = logger;
        }

        public abstract QuestionLevel Level { get; }

        /// <summary>Who answers, what the sources are and where a declined question goes, for the agent's prompt.</summary>
        protected abstract (string Role, string Sources, string Otherwise) Prompt { get; }

        /// <summary>The level's sources for this question; null when it holds none (the question then goes up).</summary>
        protected abstract Task<LevelSources?> SourcesAsync(ChainQuestion question);

        public async Task<LevelAnswer?> TryAnswerAsync(ChainQuestion question, CancellationToken cancellationToken)
        {
            var sources = await SourcesAsync(question);
            if (sources == null)
                return null;

            var asked = question.Question;
            var earlier = sources.EarlierAnswers.FirstOrDefault(a => Same(a.Question, asked.Question));
            if (earlier != default && asked.Accepts(earlier.Answer, out var repeated))
                return new LevelAnswer(repeated, $"the same question was answered before: \"{earlier.Answer}\"");

            var provider = _providerFor(question.ProjectId);
            if (provider == null)
                return null;
            var (role, sourceNames, otherwise) = Prompt;
            var agent = new QuotedAnswerAgent(provider, role, sourceNames, otherwise, new AgentOptions { Verbose = false });
            var reply = await agent.AnswerAsync(Describe(asked), sources.Text, cancellationToken);
            if (reply is not { } r)
                return null;
            if (!Normalize(sources.Text).Contains(Normalize(r.Quote)))
            {
                Logger?.LogInformation("{Level} answer dropped for project {ProjectId}: its quote is not in the sources", Level, question.ProjectId);
                return null;
            }
            if (!asked.Accepts(r.Answer, out var stored))
            {
                Logger?.LogInformation("{Level} answer dropped for project {ProjectId}: \"{Answer}\" is not an option", Level, question.ProjectId, r.Answer);
                return null;
            }
            return new LevelAnswer(stored, $"from {SourceLabel}: \"{r.Quote}\"");
        }

        /// <summary>What the person a passed question reaches is told; none unless a level says so (Dragon, TASK-206).</summary>
        public virtual string? NoteForPerson(ChainQuestion question) => null;

        /// <summary>The answer coming back down the path; nothing by default (Dragon records definitions, TASK-207).</summary>
        public virtual Task OnAnsweredAsync(ChainQuestion question, string answer, QuestionLevel answeredBy,
            CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>How an answer names where it came from, e.g. "the task's sources".</summary>
        protected abstract string SourceLabel { get; }

        private static string Describe(AskHumanQuestion q) =>
            q.Question
            + (q.Options.Length > 0 ? $"\nOptions: {string.Join(" | ", q.Options)}" + (q.AllowFreeText ? " (or another answer)" : " (one of these only)") : "")
            + (string.IsNullOrWhiteSpace(q.Context) ? "" : $"\nWhy it is asked: {q.Context}");

        private static bool Same(string a, string b) => Normalize(a) == Normalize(b);

        private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim().Trim('"', '\'', '.', ' ').ToLowerInvariant();
    }
}
