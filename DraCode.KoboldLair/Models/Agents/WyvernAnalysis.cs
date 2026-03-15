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
    }
}
