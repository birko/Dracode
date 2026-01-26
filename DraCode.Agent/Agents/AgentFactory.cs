using DraCode.Agent.LLMs.Providers;

namespace DraCode.Agent.Agents
{
    public static class AgentFactory
    {
        // Create an Agent with a specific provider name and configuration using AgentOptions
        // provider: "openai", "azureopenai", "claude", "gemini", "ollama", "githubcopilot"
        // agentType: "coding", "csharp", "cpp", "assembler", "javascript", "css", "html", "react", "angular", "php", "python", "diagramming", "media", "image", "svg", "bitmap"
        public static Agents.Agent Create(
            string provider,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null,
            string agentType = "coding")
        {
            options ??= new AgentOptions();
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

            return agentType.ToLowerInvariant() switch
            {
                "coding" => new CodingAgent(llm, options),
                "csharp" => new CSharpCodingAgent(llm, options),
                "cpp" => new CppCodingAgent(llm, options),
                "assembler" => new AssemblerCodingAgent(llm, options),
                "javascript" or "typescript" => new JavaScriptTypeScriptCodingAgent(llm, options),
                "css" => new CssCodingAgent(llm, options),
                "html" => new HtmlCodingAgent(llm, options),
                "react" => new ReactCodingAgent(llm, options),
                "angular" => new AngularCodingAgent(llm, options),
                "php" => new PhpCodingAgent(llm, options),
                "python" => new PythonCodingAgent(llm, options),
                "diagramming" or "diagram" => new DiagrammingAgent(llm, options),
                "media" => new MediaAgent(llm, options),
                "image" => new ImageAgent(llm, options),
                "svg" => new SvgAgent(llm, options),
                "bitmap" => new BitmapAgent(llm, options),
                _ => throw new ArgumentException($"Unknown agent type '{agentType}'. Supported: 'coding', 'csharp', 'cpp', 'assembler', 'javascript', 'typescript', 'css', 'html', 'react', 'angular', 'php', 'python', 'diagramming', 'media', 'image', 'svg', 'bitmap'")
            };
        }

        // Legacy overload for backward compatibility
        [Obsolete("Use Create method with AgentOptions instead")]
        public static Agents.Agent Create(
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
    }
}
