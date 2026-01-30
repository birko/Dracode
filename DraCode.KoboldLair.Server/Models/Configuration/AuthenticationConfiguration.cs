namespace DraCode.KoboldLair.Server.Models.Configuration
{
    public class AuthenticationConfiguration
    {
        public bool Enabled { get; set; } = false;
        public List<string> Tokens { get; set; } = new();
        public List<TokenIpBinding> TokenBindings { get; set; } = new();
    }
}
