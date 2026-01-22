namespace DraCode.WebSocket.Models
{
    public class ProviderConfig
    {
        public string? Type { get; set; }
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
        public string? BaseUrl { get; set; }
        public string? Endpoint { get; set; }
        public string? Deployment { get; set; }
        public string? ClientId { get; set; }
    }

    public class AgentConfiguration
    {
        public string WorkingDirectory { get; set; } = "./";
        public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
    }
}
