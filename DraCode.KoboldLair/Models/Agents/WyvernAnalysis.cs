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
    }
}
