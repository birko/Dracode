using DraCode.Agent.LLMs.Providers;

namespace DraCode.Agent
{
    public static class AgentFactory
    {
        // Create an Agent with a specific provider name and configuration.
        // provider: "openai", "azureopenai", "claude", "gemini", "ollama"
        public static Agent Create(
            string provider,
            string workingDirectory,
            bool verbose = true,
            Dictionary<string, string>? config = null)
        {
            config ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string C(string key, string def = "") =>
                config.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

            ILlmProvider llm = provider.ToLowerInvariant() switch
            {
                "openai" => new OpenAiProvider(C("apiKey"), C("model", "gpt-4o"), C("baseUrl", "https://api.openai.com/v1/chat/completions")),
                "azureopenai" => new AzureOpenAiProvider(C("endpoint"), C("apiKey"), C("deployment", "gpt-4")),
                "claude" => new ClaudeProvider(C("apiKey"), C("model", "claude-3-5-sonnet-latest"), C("baseUrl", "https://api.anthropic.com/v1/messages")),
                "gemini" => new GeminiProvider(C("apiKey"), C("model", "gemini-2.0-flash-exp"), C("baseUrl", "https://generativelanguage.googleapis.com/v1beta/models/")),
                "ollama" => new OllamaProvider(C("model", "llama3.2"), C("baseUrl", "http://localhost:11434")),
                "githubcopilot" => new GitHubCopilotProvider(C("clientId"), C("model", "gpt-4o"), C("baseUrl", "https://api.githubcopilot.com/chat/completions")),
                _ => throw new ArgumentException($"Unknown provider '{provider}'.")
            };

            return new Agent(llm, workingDirectory, verbose);
        }
    }
}
