using Birko.AI.Providers;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Dragon's level of the question chain, the last before the person (TASK-206, FEATURE-078 D19): Dragon answers only
    /// from the project's specification and the decisions already answered in the project, quoting them; never from its
    /// own judgement. A question neither settles reaches the person, asking them to define it.
    /// </summary>
    public sealed class DragonLevel : QuotedAnswerLevel
    {
        /// <summary>What the card tells the person about a question Dragon could not settle.</summary>
        public const string DefineNote =
            "The specification does not cover this, so your answer defines it.";

        private readonly Func<string, string?> _specificationFor;
        private readonly SqlPendingDecisionRepository? _decisions;

        /// <param name="specificationFor">The project's specification text, or null when it has none.</param>
        /// <param name="decisions">The decision store, for the decisions already answered in the project.</param>
        /// <param name="providerFor">The model Dragon answers with for a project; null leaves only earlier answers.</param>
        public DragonLevel(Func<string, string?> specificationFor, SqlPendingDecisionRepository? decisions,
            Func<string, ILlmProvider?> providerFor, ILogger? logger = null)
            : base(providerFor, logger)
        {
            _specificationFor = specificationFor;
            _decisions = decisions;
        }

        public override QuestionLevel Level => QuestionLevel.Dragon;

        protected override (string Role, string Sources, string Otherwise) Prompt => (
            "Dragon 🐉, who speaks for the person who owns the project",
            "the project's specification and the decisions already recorded in it",
            "the person, who will define it");

        protected override string SourceLabel => "the specification or a recorded decision";

        protected override async Task<LevelSources?> SourcesAsync(ChainQuestion question)
        {
            var specification = _specificationFor(question.ProjectId);
            var answered = _decisions == null
                ? new List<(string Question, string Answer)>()
                : (await _decisions.GetAnsweredForProjectAsync(question.ProjectId))
                    .Select(d => (PendingDecisionNotification.QuestionText(d), d.Resolution!))
                    .ToList();
            if (string.IsNullOrWhiteSpace(specification) && answered.Count == 0)
                return null;

            var text = $"### Specification\n{specification}\n";
            if (answered.Count > 0)
                text += "\n### Recorded decisions\n" + string.Join("\n", answered.Select(a => $"- Q: {a.Item1}\n  A: {a.Item2}")) + "\n";
            return new LevelSources(text, answered);
        }

        public override string? NoteForPerson(ChainQuestion question) => DefineNote;
    }
}
