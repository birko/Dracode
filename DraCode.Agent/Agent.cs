using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using System.Text.Json;

namespace DraCode.Agent
{
    public class Agent(ILlmProvider llmProvider, string workingDirectory, bool verbose = true)
    {
        private readonly ILlmProvider _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        private readonly string _workingDirectory = workingDirectory;
        private readonly List<Tool> _tools =
            [
                new ListFiles(),
                new ReadFile(),
                new WriteFile(),
                new SearchCode(),
                new RunCommand()
            ];
        private readonly bool _verbose = verbose;

        public ILlmProvider Provider => _llmProvider;
        public IReadOnlyList<Tool> Tools => _tools;
        public string ProviderName => _llmProvider.Name;
        public string WorkingDirectory => _workingDirectory;
        public bool Verbose => _verbose;

        public async Task<List<Message>> RunAsync(string task, int maxIterations = 10)
        {
            var systemPrompt = $@"You are a helpful coding assistant working in a sandboxed workspace at {_workingDirectory}.

You have access to tools that let you read, write, and execute code. When given a task:
1. Think step-by-step about what you need to do
2. Use tools to explore the workspace, read files, make changes
3. Test your changes by running code
4. Continue iterating until the task is complete

Important guidelines:
- Always explore the workspace first with list_files before making assumptions
- Read existing files before modifying them
- Test your code after making changes
- If something fails, analyze the error and try a different approach
- Be methodical and thorough

Complete the task efficiently and let me know when you're done.";

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

                var response = await _llmProvider.SendMessageAsync(conversation, _tools, systemPrompt);

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

                if (response.StopReason == "tool_use")
                {
                    var toolResults = new List<object>();

                    foreach (var block in (response.Content ?? Enumerable.Empty<ContentBlock>()).Where(b => b.Type == "tool_use"))
                    {
                        if (_verbose)
                        {
                            Console.WriteLine($"\n🔧 Tool: {block.Name}");
                            Console.WriteLine($"Input: {JsonSerializer.Serialize(block.Input)}");
                        }

                        var tool = _tools.FirstOrDefault(t => t.Name == block.Name);
                        // Fix CS8604 by ensuring block.Input is not null when calling Execute
                        var result = tool != null
                            ? tool.Execute(_workingDirectory, block.Input ?? [])
                            : $"Error: Unknown tool '{block.Name}'";

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
                }
                else if (response.StopReason == "end_turn")
                {
                    foreach (var block in (response.Content ?? Enumerable.Empty<ContentBlock>()).Where(b => b.Type == "text"))
                    {
                        Console.WriteLine($"\n💬 {_llmProvider.Name}: {block.Text}");
                    }
                    break;
                }
                else
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"\nUnexpected stop reason: {response.StopReason}");
                    }
                    break;
                }
            }

            return conversation;
        }
    }
}