namespace DraCode.KoboldLair.Server.Models
{
    public class WebSocketRequest
    {
        public string? Action { get; set; }
        public string? Command { get; set; }
        public string? Task { get; set; }
        public string? TaskId { get; set; }
    }
}
