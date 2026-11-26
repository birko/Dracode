using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>A question on its way up the hierarchy: who asked it, for which project and task, and what it asks.</summary>
    /// <param name="TaskId">The task it is about; null for a question about the whole project (the analyzer's, TASK-170).</param>
    public sealed record ChainQuestion(string ProjectId, string? TaskId, QuestionLevel AskedBy, AskHumanQuestion Question);

    /// <summary>A level's answer, and what settled it (FEATURE-078 D18: each level answers only from what it owns).</summary>
    public sealed record LevelAnswer(string Answer, string Reason);

    /// <summary>
    /// Where a question ended: the level that answered it and the levels it passed, in order. A question no agent
    /// answered ends at <see cref="QuestionLevel.Person"/> with no answer yet: it becomes a decision for the person.
    /// </summary>
    /// <param name="PersonNote">For a question that reaches the person: what the levels it passed tell them (e.g. Dragon:
    /// the specification does not cover it, so the answer defines it); null when none had anything to say.</param>
    public sealed record ChainOutcome(QuestionLevel AnsweredBy, LevelAnswer? Answer, IReadOnlyList<QuestionLevel> Path,
        string? PersonNote = null)
    {
        public bool ReachesPerson => AnsweredBy == QuestionLevel.Person;
    }

    /// <summary>
    /// One level of the hierarchy that may answer questions from below (FEATURE-078 D18). A level that cannot settle a
    /// question from what it owns returns null, and the question goes up.
    /// </summary>
    public interface IQuestionLevel
    {
        QuestionLevel Level { get; }

        Task<LevelAnswer?> TryAnswerAsync(ChainQuestion question, CancellationToken cancellationToken);

        /// <summary>
        /// What this level, having passed the question on, tells the person it reaches (TASK-206: Dragon asks them to define
        /// it); null for nothing.
        /// </summary>
        string? NoteForPerson(ChainQuestion question) => null;

        /// <summary>The answer coming back down a path this level passed the question along (D24).</summary>
        Task OnAnsweredAsync(ChainQuestion question, string answer, QuestionLevel answeredBy, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// The question chain (TASK-201, FEATURE-078 D18, D24, D27): a question climbs from the level above its asker
    /// towards the person. A question only moves up, each level gets it once, and the person is the end of the line; the
    /// answer travels back down the same path. A level with nothing registered passes the question on, so with no
    /// levels registered every question reaches the person, as before the chain. The chain has no per-project switch.
    /// </summary>
    public sealed class QuestionChain
    {
        private readonly Dictionary<QuestionLevel, IQuestionLevel> _levels = new();
        private readonly ILogger<QuestionChain>? _logger;

        public QuestionChain(ILogger<QuestionChain>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Registers the level that answers at <see cref="IQuestionLevel.Level"/>, replacing an earlier one.</summary>
        public QuestionChain Register(IQuestionLevel level)
        {
            if (level.Level == QuestionLevel.Person)
                throw new ArgumentException("The person is the end of the line, not a level that answers in code.", nameof(level));
            lock (_levels)
                _levels[level.Level] = level;
            return this;
        }

        /// <summary>
        /// Takes the question up from the level above its asker. Returns the first level that answers and the path it
        /// took; a level that fails is logged and passes the question on, as one with no answer does.
        /// </summary>
        public async Task<ChainOutcome> AskAsync(ChainQuestion question, CancellationToken cancellationToken = default)
        {
            var path = new List<QuestionLevel>();
            var notes = new List<string>();
            for (var level = question.AskedBy + 1; level < QuestionLevel.Person; level++)
            {
                path.Add(level);
                IQuestionLevel? answerer;
                lock (_levels)
                    _levels.TryGetValue(level, out answerer);
                if (answerer == null)
                    continue;

                LevelAnswer? answer;
                try
                {
                    answer = await answerer.TryAnswerAsync(question, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogWarning(ex, "The {Level} level failed on a question for project {ProjectId}; passing it up",
                        level, question.ProjectId);
                    answer = null;
                }

                if (answer != null && !string.IsNullOrWhiteSpace(answer.Answer))
                {
                    _logger?.LogInformation("The {Level} level answered a question for project {ProjectId}: {Reason}",
                        level, question.ProjectId, answer.Reason);
                    return new ChainOutcome(level, answer, path);
                }
                if (answerer.NoteForPerson(question) is { Length: > 0 } note)
                    notes.Add(note);
            }

            path.Add(QuestionLevel.Person);
            return new ChainOutcome(QuestionLevel.Person, null, path, notes.Count == 0 ? null : string.Join(" ", notes));
        }

        /// <summary>
        /// Takes an answer back down the path the question climbed (D24): every level on it below the one that answered
        /// hears the answer, nearest that level first. A level that fails is logged; the rest still hear it.
        /// </summary>
        public async Task DeliverAnswerAsync(ChainQuestion question, IReadOnlyList<QuestionLevel> path, string answer,
            QuestionLevel answeredBy, CancellationToken cancellationToken = default)
        {
            foreach (var level in path.Where(l => l < answeredBy).OrderByDescending(l => l))
            {
                IQuestionLevel? passed;
                lock (_levels)
                    _levels.TryGetValue(level, out passed);
                if (passed == null)
                    continue;
                try
                {
                    await passed.OnAnsweredAsync(question, answer, answeredBy, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogWarning(ex, "The {Level} level failed to take an answer for project {ProjectId}", level, question.ProjectId);
                }
            }
        }
    }
}
