using DraCode.KoboldLair.Server.Models.Agents;

namespace DraCode.KoboldLair.Server.Models.Projects
{
    public class WorkArea
    {
        public string Name { get; set; } = "";
        public List<WyvernTask> Tasks { get; set; } = new();
    }
}
