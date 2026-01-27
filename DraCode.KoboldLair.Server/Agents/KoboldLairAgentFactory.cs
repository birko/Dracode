using DraCode.Agent;
using DraCode.Agent.Agents;

namespace DraCode.KoboldLair.Server.Agents
{
    /// <summary>
    /// Factory for creating agents in KoboldLair context.
    /// Handles orchestrator agent locally and delegates to DraCode.Agent.AgentFactory for specialized agents.
    /// </summary>
    public static class KoboldLairAgentFactory
    {
        /// <summary>
        /// Create an Agent with a specific provider name and configuration using AgentOptions.
        /// </summary>
        /// <param name="provider">LLM provider: "openai", "azureopenai", "claude", "gemini", "ollama", "llamacpp", "githubcopilot"</param>
        /// <param name="options">Agent options (working directory, verbose, etc.)</param>
        /// <param name="config">Provider configuration (API keys, models, etc.)</param>
        /// <param name="agentType">Type of agent to create: "dragon", "wyvern", "coding", "csharp", "cpp", "php", "python", "svg", "bitmap", etc.</param>
        /// <returns>Agent instance</returns>
        public static Agent.Agents.Agent Create(
            string provider,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null,
            string agentType = "coding")
        {
            options ??= new AgentOptions();
            config ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Handle KoboldLair-specific agents locally
            if (agentType.Equals("wyvern", StringComparison.OrdinalIgnoreCase))
            {
                // Create LLM provider
                var llmProvider = CreateLlmProvider(provider, config);
                return new WyvernAgent(llmProvider, options, provider, config);
            }
            else if (agentType.Equals("dragon", StringComparison.OrdinalIgnoreCase))
            {
                // Create Dragon agent for requirements gathering
                var llmProvider = CreateLlmProvider(provider, config);
                var specificationsPath = config.TryGetValue("specificationsPath", out var path) 
                    ? path 
                    : "./specifications";
                return new DragonAgent(llmProvider, options, specificationsPath);
            }

            // Delegate all other agent types to DraCode.Agent.AgentFactory
            return AgentFactory.Create(provider, options, config, agentType);
        }

        /// <summary>
        /// Legacy overload for backward compatibility
        /// </summary>
        [Obsolete("Use Create method with AgentOptions instead")]
        public static Agent.Agents.Agent Create(
            string provider,
            string workingDirectory,
            bool verbose = true,
            Dictionary<string, string>? config = null,
            string agentType = "coding")
        {
            var options = new AgentOptions
            {
                WorkingDirectory = workingDirectory,
                Verbose = verbose
            };
            return Create(provider, options, config, agentType);
        }

        /// <summary>
        /// Creates an LLM provider instance based on provider name and configuration.
        /// </summary>
        public static Agent.LLMs.Providers.ILlmProvider CreateLlmProvider(
            string provider,
            Dictionary<string, string> config)
        {
            string C(string key, string def = "") =>
                config.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

            return provider.ToLowerInvariant() switch
            {
                "openai" => new Agent.LLMs.Providers.OpenAiProvider(
                    C("apiKey"), 
                    C("model", "gpt-4o"), 
                    C("baseUrl", "https://api.openai.com/v1/chat/completions")),
                
                "azureopenai" => new Agent.LLMs.Providers.AzureOpenAiProvider(
                    C("endpoint"), 
                    C("apiKey"), 
                    C("deployment", "gpt-4")),
                
                "claude" => new Agent.LLMs.Providers.ClaudeProvider(
                    C("apiKey"), 
                    C("model", "claude-3-5-sonnet-latest"), 
                    C("baseUrl", "https://api.anthropic.com/v1/messages")),
                
                "gemini" => new Agent.LLMs.Providers.GeminiProvider(
                    C("apiKey"), 
                    C("model", "gemini-2.0-flash-exp"), 
                    C("baseUrl", "https://generativelanguage.googleapis.com/v1beta/models/")),
                
                "ollama" => new Agent.LLMs.Providers.OllamaProvider(
                    C("model", "llama3.2"), 
                    C("baseUrl", "http://localhost:11434")),
                
                "llamacpp" => new Agent.LLMs.Providers.LlamaCppProvider(
                    C("model", "default"), 
                    C("baseUrl", "http://localhost:8080")),
                
                "githubcopilot" => new Agent.LLMs.Providers.GitHubCopilotProvider(
                    C("clientId"), 
                    C("model", "gpt-4o"), 
                    C("baseUrl", "https://api.githubcopilot.com/chat/completions")),
                
                _ => throw new ArgumentException($"Unknown provider '{provider}'. Supported providers: openai, azureopenai, claude, gemini, ollama, llamacpp, githubcopilot")
            };
        }
    }
}
