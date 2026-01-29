namespace DraCode.KoboldLair.Server.Models
{
    /// <summary>
    /// Root configuration containing all provider settings
    /// </summary>
    public class KoboldLairProviderConfiguration
    {
        /// <summary>
        /// List of available providers
        /// </summary>
        public List<ProviderConfig> Providers { get; set; } = new();

        /// <summary>
        /// Active agent-to-provider mappings
        /// </summary>
        public AgentProviderSettings AgentProviders { get; set; } = new();
    }
}
