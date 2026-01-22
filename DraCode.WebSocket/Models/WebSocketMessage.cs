namespace DraCode.WebSocket.Models
{
    public class WebSocketMessage
    {
        public string? Command { get; set; }
        public string? Data { get; set; }
        public Dictionary<string, string>? Config { get; set; }
        public string? AgentId { get; set; }  // Added for multi-agent support
        public string? PromptId { get; set; }  // For responding to prompts
    }

    public class WebSocketResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? Data { get; set; }
        public string? Error { get; set; }
        public string? AgentId { get; set; }  // Added for multi-agent support
        public string? MessageType { get; set; }  // Type of streaming message (info, tool_call, tool_result, prompt, etc.)
        public string? PromptId { get; set; }  // ID for interactive prompts
    }
}
