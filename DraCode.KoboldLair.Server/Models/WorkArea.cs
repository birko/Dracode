namespace DraCode.KoboldLair.Server.Models
{
    public class WorkArea
    {
        public string Name { get; set; } = "";
        public List<WyvernTask> Tasks { get; set; } = new();
    }
}
