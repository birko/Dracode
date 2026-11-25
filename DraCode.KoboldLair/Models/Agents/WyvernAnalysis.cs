using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Models.Agents
{
    public class WyvernAnalysis
    {
        public string ProjectName { get; set; } = "";
        public List<WorkArea> Areas { get; set; } = new();
        public int TotalTasks { get; set; }
        public string EstimatedComplexity { get; set; } = "medium";
        public DateTime AnalyzedAt { get; set; }
        public string SpecificationPath { get; set; } = "";
        public List<string> ProcessedFeatures { get; set; } = new();
        public ProjectStructure? Structure { get; set; }

        /// <summary>
        /// Explicit constraints extracted from the specification (e.g., "no frameworks", "vanilla only").
        /// Passed to Kobolds to prevent spec violations.
        /// </summary>
        public List<string> Constraints { get; set; } = new();

        /// <summary>
        /// Features explicitly marked as out of scope in the specification.
        /// Kobolds must NOT implement these.
        /// </summary>
        public List<string> OutOfScope { get; set; } = new();

        /// <summary>
        /// Maps spec requirements to task IDs for traceability.
        /// Ensures every requirement has at least one covering task.
        /// </summary>
        public Dictionary<string, string> RequirementsCoverage { get; set; } = new();

        /// <summary>
        /// What the analyzer could not settle from the specification and asks a person instead of guessing (TASK-170,
        /// FEATURE-078 D26): at most <see cref="MaxQuestions"/>, each checked as <c>ask_human</c>'s are. Read only when the
        /// analysis was allowed to ask; such an analysis carries no tasks, and runs again once they are answered.
        /// </summary>
        public List<WyvernQuestion> Questions { get; set; } = new();

        /// <summary>How many questions one analysis may ask, in its one round (D26).</summary>
        public const int MaxQuestions = 3;
    }

    /// <summary>A question in <see cref="WyvernAnalysis.Questions"/>, in the shape of an <c>ask_human</c> question.</summary>
    public class WyvernQuestion
    {
        public string Question { get; set; } = "";
        public List<string> Options { get; set; } = new();
        public bool? AllowFreeText { get; set; }
        public string? Context { get; set; }
    }
}
