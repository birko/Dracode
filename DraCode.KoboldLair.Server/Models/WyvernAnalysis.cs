namespace DraCode.KoboldLair.Server.Models
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
    }
}
