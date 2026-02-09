namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Simplified project information for Dragon to display
    /// </summary>
    public class ProjectInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string ExecutionState { get; set; } = "Running";
        public int FeatureCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool HasGitRepository { get; set; }
    }
}
