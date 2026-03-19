using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.Agent.LLMs.Providers;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

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
        /// Optionally wraps the LLM provider with rate limiting and cost tracking.
        /// </summary>
        public static Agent.Agents.Agent Create(
            string provider,
            KoboldLairConfiguration koboldLairConfig,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null,
            string agentType = "coding",
            ProviderRateLimiter? rateLimiter = null,
            CostTrackingService? costTracker = null,
            ILogger? logger = null)
        {
            options ??= new AgentOptions();
            config ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var providers = koboldLairConfig.Providers?.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, ProviderConfig>(StringComparer.OrdinalIgnoreCase);
            var providerType = providers.ContainsKey(provider) ? providers[provider].Type : koboldLairConfig.DefaultProvider; // Use provided name if not found in config

            // Handle KoboldLair-specific agents locally
            if (agentType.Equals("wyrm", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType, rateLimiter, costTracker, logger);
                return new WyrmAgent(llmProvider, options, provider, config);
            }
            else if (agentType.Equals("dragon", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType, rateLimiter, costTracker, logger);
                return new DragonAgent(llmProvider, options);
            }
            else if (agentType.Equals("wyvern", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType, rateLimiter, costTracker, logger);
                return new WyvernAgent(llmProvider, options);
            }
            else if (agentType.Equals("kobold-planner", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType, rateLimiter, costTracker, logger);
                return new KoboldPlannerAgent(llmProvider, options);
            }
            else if (agentType.Equals("wyrm-preanalysis", StringComparison.OrdinalIgnoreCase))
            {
                var llmProvider = CreateLlmProvider(providerType, config, agentType, rateLimiter, costTracker, logger);
                return new WyrmPreAnalysisAgent(llmProvider, options);
            }

            // Delegate all other agent types to DraCode.Agent.AgentFactory
            // Wrap with tracking if services are provided
            if (rateLimiter != null || costTracker != null)
            {
                var baseProvider = AgentFactory.CreateLlmProvider(providerType, config, agentType);
                var trackedProvider = new TrackedLlmProvider(baseProvider, rateLimiter, costTracker, logger)
                {
                    AgentType = agentType
                };
                return AgentFactory.Create(trackedProvider, options, agentType);
            }

            return AgentFactory.Create(providerType, options, config, agentType);
        }

        /// <summary>
        /// Creates an LLM provider instance, optionally wrapped with rate limiting and cost tracking.
        /// </summary>
        public static ILlmProvider CreateLlmProvider(
            string provider,
            Dictionary<string, string> config,
            string? agentType = null,
            ProviderRateLimiter? rateLimiter = null,
            CostTrackingService? costTracker = null,
            ILogger? logger = null)
        {
            var baseProvider = AgentFactory.CreateLlmProvider(provider, config, agentType);

            if (rateLimiter != null || costTracker != null)
            {
                return new TrackedLlmProvider(baseProvider, rateLimiter, costTracker, logger)
                {
                    AgentType = agentType
                };
            }

            return baseProvider;
        }
    }
}
