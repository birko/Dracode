namespace DraCode.WebSocket.Models
{
    public class WebSocketMessage
    {
        public string? Command { get; set; }
        public string? Data { get; set; }
        public Dictionary<string, string>? Config { get; set; }
        public string? AgentId { get; set; }  // Added for multi-agent support
    }

    public class WebSocketResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? Data { get; set; }
        public string? Error { get; set; }
        public string? AgentId { get; set; }  // Added for multi-agent support
    }
}
