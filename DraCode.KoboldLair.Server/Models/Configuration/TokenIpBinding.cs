namespace DraCode.KoboldLair.Server.Models.Configuration
{
    public class TokenIpBinding
    {
        public string Token { get; set; } = string.Empty;
        public List<string> AllowedIps { get; set; } = new();
    }
}
