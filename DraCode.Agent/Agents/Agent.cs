using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using System.Text.Json;

namespace DraCode.Agent.Agents
{
    public abstract class Agent
    {
        private readonly ILlmProvider _llmProvider;
        private readonly string _workingDirectory;
        private readonly List<Tool> _tools;
        private readonly bool _verbose;

        protected Agent(ILlmProvider llmProvider, string workingDirectory, bool verbose = true)
        {
            _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
            _workingDirectory = workingDirectory;
            _tools = CreateTools();
            _verbose = verbose;
        }

        // Abstract property that derived classes must implement
        protected abstract string SystemPrompt { get; }

        // Virtual method that derived classes can override to customize tools
        protected virtual List<Tool> CreateTools()
        {
            return
            [
                new ListFiles(),
                new ReadFile(),
                new WriteFile(),
                new SearchCode(),
                new RunCommand(),
                new DisplayText(),
                new AskUser()
            ];
        }

        public ILlmProvider Provider => _llmProvider;
        public IReadOnlyList<Tool> Tools => _tools;
        public string ProviderName => _llmProvider.Name;
        public string WorkingDirectory => _workingDirectory;
        public bool Verbose => _verbose;

        public async Task<List<Message>> RunAsync(string task, int maxIterations = 10)
        {
            var conversation = new List<Message>
            {
                new() { Role = "user", Content = task }
            };

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                if (_verbose)
                {
                    Console.WriteLine($"\n{'='.ToString().PadRight(60, '=')}");
                    Console.WriteLine($"ITERATION {iteration}");
                    Console.WriteLine($"{'='.ToString().PadRight(60, '=')}");
                }

                var response = await _llmProvider.SendMessageAsync(conversation, _tools, SystemPrompt);

                if (_verbose)
                {
                    Console.WriteLine($"\nStop reason: {response.StopReason}");
                }

                // Add assistant response to conversation
                conversation.Add(new Message
                {
                    Role = "assistant",
                    Content = response.Content
                });

                // Handle different stop reasons
                switch (response.StopReason)
                {
                    case "tool_use":
                        var hasTextContent = (response.Content ?? []).Any(b => b.Type == "text" && !string.IsNullOrWhiteSpace(b.Text));
                        var toolResults = new List<object>();
                        var hasErrors = false;

                        foreach (var block in (response.Content ?? Enumerable.Empty<ContentBlock>()).Where(b => b.Type == "tool_use"))
                        {
                            if (_verbose)
                            {
                                Console.WriteLine($"\n🔧 Tool: {block.Name}");
                                Console.WriteLine($"Input: {JsonSerializer.Serialize(block.Input)}");
                            }

                            var tool = _tools.FirstOrDefault(t => t.Name == block.Name);
                            var result = tool != null
                                ? tool.Execute(_workingDirectory, block.Input ?? [])
                                : $"Error: Unknown tool '{block.Name}'";

                            // Check if tool execution resulted in an error
                            if (result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                            {
                                hasErrors = true;
                            }

                            if (_verbose)
                            {
                                var preview = result.Length > 200 ? string.Concat(result.AsSpan(0, 200), "...") : result;
                                Console.WriteLine($"Result: {preview}");
                            }

                            toolResults.Add(new
                            {
                                type = "tool_result",
                                tool_use_id = block.Id,
                                content = result
                            });
                        }

                        conversation.Add(new Message
                        {
                            Role = "user",
                            Content = toolResults
                        });

                        // If agent included text with tool calls, print it
                        if (hasTextContent && _verbose)
                        {
                            foreach (var block in (response.Content ?? []).Where(b => b.Type == "text"))
                            {
                                Console.WriteLine($"\n💬 {_llmProvider.Name}: {block.Text}");
                            }
                        }

                        // If we hit max iterations after tool calls, warn and stop
                        if (iteration >= maxIterations)
                        {
                            Console.WriteLine($"\n⚠️ Maximum iterations ({maxIterations}) reached. Task may be incomplete.");
                            return conversation;
                        }

                        // If all tools failed, no point continuing - let agent know and give one more chance
                        if (hasErrors && toolResults.Count > 0 && toolResults.All(r => 
                            r.GetType().GetProperty("content")?.GetValue(r)?.ToString()?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ?? false))
                        {
                            if (_verbose)
                            {
                                Console.WriteLine("\n⚠️ All tool executions failed. Giving agent one final response...");
                            }
                        }
                        break;

                    case "end_turn":
                        // Agent finished - print response and exit
                        foreach (var block in (response.Content ?? Enumerable.Empty<ContentBlock>()).Where(b => b.Type == "text"))
                        {
                            Console.WriteLine($"\n💬 {_llmProvider.Name}: {block.Text}");
                        }
                        return conversation;

                    case "error":
                        // Error occurred - stop immediately
                        if (_verbose)
                        {
                            Console.WriteLine("\n❌ Error occurred during LLM request. Stopping.");
                        }
                        return conversation;

                    case "NotConfigured":
                        // Provider not configured - stop immediately
                        Console.Error.WriteLine($"\n❌ Provider '{_llmProvider.Name}' is not properly configured.");
                        return conversation;

                    default:
                        // Unexpected stop reason - stop to be safe
                        if (_verbose)
                        {
                            Console.WriteLine($"\n⚠️ Unexpected stop reason: {response.StopReason}. Stopping.");
                        }
                        return conversation;
                }
            }

            // If we exit the loop naturally, we hit max iterations
            if (_verbose)
            {
                Console.WriteLine($"\n⚠️ Maximum iterations ({maxIterations}) reached.");
            }

            return conversation;
        }
    }
}