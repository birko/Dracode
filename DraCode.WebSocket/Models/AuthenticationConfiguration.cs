namespace DraCode.WebSocket.Models
{
    public class AuthenticationConfiguration
    {
        public bool Enabled { get; set; } = false;
        public List<string> Tokens { get; set; } = new();
        public List<TokenIpBinding> TokenBindings { get; set; } = new();
    }

    public class TokenIpBinding
    {
        public string Token { get; set; } = string.Empty;
        public List<string> AllowedIps { get; set; } = new();
    }
}
