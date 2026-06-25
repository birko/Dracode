using Birko.AI;
using Birko.AI.Agents;
using Birko.AI.Factories;
using Birko.AI.Providers;
using Birko.AI.Resilience.Services;
using DraCode.KoboldLair.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Agents
{
    /// <summary>
    /// Factory for creating agents in KoboldLair context.
    /// Handles orchestrator agents locally and delegates to AgentFactory/AgentRegistration for specialized agents.
    /// </summary>
    public static class KoboldLairAgentFactory
    {
        /// <summary>
        /// Create an Agent with a specific provider name and configuration using AgentOptions.
        /// Optionally wraps the LLM provider with rate limiting and cost tracking.
        /// </summary>
        public static Agent Create(
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
            // Ensure agent types + providers are registered (idempotent). Every agent-creation path runs
            // through here; without this the ad-hoc /kobold path (which skips AgentTypeValidator) has an
            // empty agent registry → "Agent type 'coding' is not registered".
            AgentRegistration.RegisterAll();
            // Translate a known appsettings provider *name* to its factory *type*. When the name isn't in
            // appsettings (e.g. DB-backed providers, where the caller already resolved + passes the type),
            // use the supplied value as-is — it's already the type. (Previously this fell back to
            // DefaultProvider, which broke once providers moved to the DB and appsettings.Providers went empty.)
            var providers = koboldLairConfig.Providers?.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, ProviderConfig>(StringComparer.OrdinalIgnoreCase);
            var providerType = providers.TryGetValue(provider, out var known) ? known.Type : provider;

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

            // Delegate all other agent types to Birko.AI factories
            if (rateLimiter != null || costTracker != null)
            {
                var baseProvider = CreateBaseProvider(providerType, config, agentType);
                var trackedProvider = new TrackedLlmProvider(baseProvider, rateLimiter, costTracker, logger)
                {
                    AgentType = agentType
                };
                return AgentFactory.Create(trackedProvider, options, agentType);
            }

            return AgentRegistration.Create(providerType, options, config, agentType);
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
            var baseProvider = CreateBaseProvider(provider, config, agentType);

            if (rateLimiter != null || costTracker != null)
            {
                return new TrackedLlmProvider(baseProvider, rateLimiter, costTracker, logger)
                {
                    AgentType = agentType
                };
            }

            return baseProvider;
        }

        /// <summary>
        /// Creates a base LLM provider using LlmProviderFactory.
        /// Injects Z.AI coding endpoint hint based on agent type.
        /// </summary>
        private static ILlmProvider CreateBaseProvider(string provider, Dictionary<string, string> config, string? agentType)
        {
            ProviderRegistration.RegisterAll();

            // Inject Z.AI coding endpoint hint if needed
            if (!string.IsNullOrEmpty(agentType)
                && (provider.Equals("zai", StringComparison.OrdinalIgnoreCase)
                    || provider.Equals("zhipu", StringComparison.OrdinalIgnoreCase)
                    || provider.Equals("zhipuai", StringComparison.OrdinalIgnoreCase))
                && AgentRegistration.IsCodingAgent(agentType)
                && !config.ContainsKey("useCodingEndpoint"))
            {
                config = new Dictionary<string, string>(config, StringComparer.OrdinalIgnoreCase)
                {
                    ["useCodingEndpoint"] = "true"
                };
            }

            return LlmProviderFactory.Create(provider, config);
        }
    }
}
