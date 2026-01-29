namespace DraCode.KoboldLair.Server.Models
{
    /// <summary>
    /// Statistics about projects in the system
    /// </summary>
    public class ProjectStatistics
    {
        public int TotalProjects { get; set; }
        public int NewProjects { get; set; }
        public int WyvernAssignedProjects { get; set; }
        public int AnalyzedProjects { get; set; }
        public int SpecificationModifiedProjects { get; set; }
        public int InProgressProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int FailedProjects { get; set; }

        public override string ToString()
        {
            var modifiedStr = SpecificationModifiedProjects > 0 ? $", {SpecificationModifiedProjects} modified" : "";
            return $"Projects: {TotalProjects} total, {NewProjects} new, {WyvernAssignedProjects} assigned, " +
                   $"{AnalyzedProjects} analyzed{modifiedStr}, {InProgressProjects} in progress, " +
                   $"{CompletedProjects} completed, {FailedProjects} failed";
        }
    }
}
