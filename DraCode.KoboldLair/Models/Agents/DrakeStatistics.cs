namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Statistics about the Drake's managed resources
    /// </summary>
    public class DrakeStatistics
    {
        public int TotalKobolds { get; init; }
        public int UnassignedKobolds { get; init; }
        public int AssignedKobolds { get; init; }
        public int WorkingKobolds { get; init; }
        public int DoneKobolds { get; init; }
        public int FailedKobolds { get; init; }
        public int TotalTasks { get; init; }
        public int UnassignedTasks { get; init; }
        public int WorkingTasks { get; init; }
        public int DoneTasks { get; init; }
        public int FailedTasks { get; init; }
        public int BlockedTasks { get; init; }
        public int ActiveAssignments { get; init; }

        public override string ToString()
        {
            return $"Kobolds: {TotalKobolds} (Working: {WorkingKobolds}, Done: {DoneKobolds}, Failed: {FailedKobolds}) | " +
                   $"Tasks: {TotalTasks} (Working: {WorkingTasks}, Done: {DoneTasks}, Failed: {FailedTasks}, Blocked: {BlockedTasks}) | " +
                   $"Active: {ActiveAssignments}";
        }
    }
}
