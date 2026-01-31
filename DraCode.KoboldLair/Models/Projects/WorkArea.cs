using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Models.Projects
{
    public class WorkArea
    {
        public string Name { get; set; } = "";
        public List<WyvernTask> Tasks { get; set; } = new();
    }
}
