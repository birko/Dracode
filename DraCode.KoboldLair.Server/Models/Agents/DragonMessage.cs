namespace DraCode.KoboldLair.Server.Models.Agents
{
    /// <summary>
    /// Message format from Dragon frontend
    /// </summary>
    public class DragonMessage
    {
        public string? Type { get; set; }
        public string Message { get; set; } = "";
        public string? SessionId { get; set; }
        public string? Provider { get; set; }
    }
}
