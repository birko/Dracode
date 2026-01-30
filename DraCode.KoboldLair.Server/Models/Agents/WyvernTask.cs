namespace DraCode.KoboldLair.Server.Models.Agents
{
    public class WyvernTask
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string AgentType { get; set; } = "coding";
        public string Complexity { get; set; } = "medium";
        public List<string> Dependencies { get; set; } = new();
        public int DependencyLevel { get; set; }
        public string Priority { get; set; } = "medium";
        public string? FeatureId { get; set; }
    }
}
