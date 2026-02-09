namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Represents Wyrm's pre-analysis recommendations for a project.
    /// Used to guide Wyvern's analysis with suggested agent types.
    /// </summary>
    public class WyrmRecommendation
    {
        /// <summary>
        /// Project ID this recommendation is for
        /// </summary>
        public string ProjectId { get; set; } = "";

        /// <summary>
        /// Project name
        /// </summary>
        public string ProjectName { get; set; } = "";

        /// <summary>
        /// When the recommendation was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Wyrm's overall analysis summary
        /// </summary>
        public string AnalysisSummary { get; set; } = "";

        /// <summary>
        /// Recommended primary programming language(s)
        /// </summary>
        public List<string> RecommendedLanguages { get; set; } = new();

        /// <summary>
        /// Recommended specialized agents for different task areas.
        /// Key: Area name (e.g., "backend", "frontend", "database")
        /// Value: Agent type (e.g., "csharp", "typescript", "documentation")
        /// </summary>
        public Dictionary<string, string> RecommendedAgentTypes { get; set; } = new();

        /// <summary>
        /// Detected technical stack components
        /// </summary>
        public List<string> TechnicalStack { get; set; } = new();

        /// <summary>
        /// Suggested task breakdown areas
        /// </summary>
        public List<string> SuggestedAreas { get; set; } = new();

        /// <summary>
        /// Estimated complexity (Low, Medium, High)
        /// </summary>
        public string Complexity { get; set; } = "Medium";

        /// <summary>
        /// Additional notes from Wyrm's analysis
        /// </summary>
        public string Notes { get; set; } = "";
    }
}
