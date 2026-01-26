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
        
        // Agent options that can be specified per provider
        public bool? Interactive { get; set; }
        public int? MaxIterations { get; set; }
        public bool? Verbose { get; set; }
        public int? PromptTimeout { get; set; }
        public string? DefaultPromptResponse { get; set; }
        public int? ModelDepth { get; set; }
    }

    public class AgentConfiguration
    {
        public string WorkingDirectory { get; set; } = "./";
        public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
        
        // Default agent options
        public bool Interactive { get; set; } = true;
        public int MaxIterations { get; set; } = 10;
        public bool Verbose { get; set; } = true;
        public int PromptTimeout { get; set; } = 300;
        public string? DefaultPromptResponse { get; set; }
        public int ModelDepth { get; set; } = 5;
    }
}
