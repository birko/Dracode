using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.Agent.LLMs.Providers;
using DraCode.KoboldLair.Models.Configuration;

namespace DraCode.KoboldLair.Agents
{
    /// <summary>
    /// Factory for creating agents in KoboldLair context.
    /// Handles orchestrator agents locally and delegates to DraCode.Agent.AgentFactory for specialized agents.
    /// </summary>
    public static class KoboldLairAgentFactory
    {
        /// <summary>
        /// Create an Agent with a specific provider name and configuration using AgentOptions.
        /// </summary>
        /// <param name="provider">LLM provider (see AgentFactory.SupportedProviders)</param>
        /// <param name="koboldLairConfig">KoboldLair configuration for provider settings</param>
        /// <param name="options">Agent options (working directory, verbose, etc.)</param>
        /// <param name="config">Provider configuration (API keys, models, etc.)</param>
        /// <param name="agentType">Type of agent: "dragon", "wyvern", "wyrm", or any DraCode.Agent type</param>
        /// <returns>Agent instance</returns>
        public static Agent.Agents.Agent Create(
            string provider,
            KoboldLairConfiguration koboldLairConfig,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null,
            string agentType = "coding")
        {
            options ??= new AgentOptions();
            config ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var providers = koboldLairConfig.Providers?.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, ProviderConfig>(StringComparer.OrdinalIgnoreCase);
            var providerType = providers.ContainsKey(provider) ? providers[provider].Type : koboldLairConfig.DefaultProvider; // Use provided name if not found in config

            // Handle KoboldLair-specific agents locally
            if (agentType.Equals("wyrm", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType);
                return new WyrmAgent(llmProvider, options, provider, config);
            }
            else if (agentType.Equals("dragon", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType);
                return new DragonAgent(llmProvider, options);
            }
            else if (agentType.Equals("wyvern", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType);
                return new WyvernAgent(llmProvider, options);
            }
            else if (agentType.Equals("kobold-planner", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType);
                return new KoboldPlannerAgent(llmProvider, options);
            }

            // Delegate all other agent types to DraCode.Agent.AgentFactory
            return AgentFactory.Create(providerType, options, config, agentType);
        }

        /// <summary>
        /// Creates an LLM provider instance. Delegates to DraCode.Agent.AgentFactory.CreateLlmProvider.
        /// </summary>
        public static ILlmProvider CreateLlmProvider(string provider, Dictionary<string, string> config, string? agentType = null)
            => AgentFactory.CreateLlmProvider(provider, config, agentType);
    }
}
