using Birko.AI.Providers;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// The overseer's level of the question chain (TASK-205, FEATURE-078 D18): Wyvern answers only from the project's
    /// specification, its own analysis (<c>analysis.md</c>) and the decisions answered in the project, quoting the line
    /// and naming which of them settled it. Anything else goes up to Dragon's level.
    /// </summary>
    public sealed class OverseerLevel : QuotedAnswerLevel
    {
        private readonly Func<string, string?> _projectFolderFor;
        private readonly SqlPendingDecisionRepository? _decisions;

        /// <param name="projectFolderFor">The project's folder (where <c>specification.md</c> and <c>analysis.md</c> are), or null.</param>
        /// <param name="decisions">The decision store, for the decisions answered in the project.</param>
        /// <param name="providerFor">The model Wyvern answers with for a project; null leaves only earlier answers.</param>
        public OverseerLevel(Func<string, string?> projectFolderFor, SqlPendingDecisionRepository? decisions,
            Func<string, ILlmProvider?> providerFor, ILogger? logger = null)
            : base(providerFor, logger)
        {
            _projectFolderFor = projectFolderFor;
            _decisions = decisions;
        }

        public override QuestionLevel Level => QuestionLevel.Overseer;

        protected override (string Role, string Sources, string Otherwise) Prompt => (
            "Wyvern 🐲, the overseer of the whole project",
            "the project's specification, your analysis of it, and the decisions already made in the project",
            "Dragon, and maybe the person");

        protected override string SourceLabel => "the specification, the analysis or a decision";

        protected override async Task<LevelSources?> SourcesAsync(ChainQuestion question)
        {
            var folder = _projectFolderFor(question.ProjectId);
            var specification = Read(folder, "specification.md");
            var analysis = Read(folder, "analysis.md");
            var answered = _decisions == null
                ? new List<(string Question, string Answer)>()
                : (await _decisions.GetAnsweredForProjectAsync(question.ProjectId))
                    .Select(d => (PendingDecisionNotification.QuestionText(d), d.Resolution!))
                    .ToList();
            if (specification == null && analysis == null && answered.Count == 0)
                return null;

            var text = "";
            if (specification != null)
                text += $"### Specification\n{specification}\n";
            if (analysis != null)
                text += $"\n### Analysis\n{analysis}\n";
            if (answered.Count > 0)
                text += "\n### Decisions\n" + string.Join("\n", answered.Select(a => $"- Q: {a.Item1}\n  A: {a.Item2}")) + "\n";
            return new LevelSources(text, answered);
        }

        private static string? Read(string? folder, string file)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            var path = Path.Combine(folder, file);
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
