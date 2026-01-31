namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Statistics about projects in the system
    /// </summary>
    public class ProjectStatistics
    {
        public int TotalProjects { get; set; }
        public int PrototypeProjects { get; set; }
        public int NewProjects { get; set; }
        public int WyvernAssignedProjects { get; set; }
        public int AnalyzedProjects { get; set; }
        public int SpecificationModifiedProjects { get; set; }
        public int InProgressProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int FailedProjects { get; set; }

        public override string ToString()
        {
            var prototypeStr = PrototypeProjects > 0 ? $"{PrototypeProjects} prototype, " : "";
            var modifiedStr = SpecificationModifiedProjects > 0 ? $", {SpecificationModifiedProjects} modified" : "";
            return $"Projects: {TotalProjects} total, {prototypeStr}{NewProjects} new, {WyvernAssignedProjects} assigned, " +
                   $"{AnalyzedProjects} analyzed{modifiedStr}, {InProgressProjects} in progress, " +
                   $"{CompletedProjects} completed, {FailedProjects} failed";
        }
    }
}
