namespace DraCode.KoboldLair.Server.Models
{
    /// <summary>
    /// Statistics about Kobold instances
    /// </summary>
    public class KoboldStatistics
    {
        public int Total { get; init; }
        public int Unassigned { get; init; }
        public int Assigned { get; init; }
        public int Working { get; init; }
        public int Done { get; init; }
        public Dictionary<string, int> ByAgentType { get; init; } = new();

        public override string ToString()
        {
            return $"Total: {Total}, Unassigned: {Unassigned}, Assigned: {Assigned}, Working: {Working}, Done: {Done}";
        }
    }
}
