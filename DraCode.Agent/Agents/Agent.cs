using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using System.Text.Json;

namespace DraCode.Agent.Agents
{
    public abstract class Agent
    {
        private readonly ILlmProvider _llmProvider;
        private readonly AgentOptions _options;
        private List<Tool> _tools;
        private Action<string, string>? _messageCallback;

        protected Agent(ILlmProvider llmProvider, AgentOptions? options = null, Action<string, string>? messageCallback = null)
        {
            _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
            _options = options ?? new AgentOptions();
            _tools = CreateTools();
            _messageCallback = messageCallback;
            
            // Set callback on provider
            _llmProvider.MessageCallback = messageCallback;
            
            // Set callback and options on all tools
            foreach (var tool in _tools)
            {
                tool.MessageCallback = messageCallback;
                tool.Options = _options;
            }
        }

        // Legacy constructor for backward compatibility
        [Obsolete("Use constructor with AgentOptions instead")]
        protected Agent(ILlmProvider llmProvider, string workingDirectory, bool verbose = true, Action<string, string>? messageCallback = null)
            : this(llmProvider, new AgentOptions { WorkingDirectory = workingDirectory, Verbose = verbose }, messageCallback)
        {
        }

        public void SetMessageCallback(Action<string, string>? callback)
        {
            _messageCallback = callback;
            
            // Update callback on provider
            _llmProvider.MessageCallback = callback;
            
            // Update callback on all tools
            foreach (var tool in _tools)
            {
                tool.MessageCallback = callback;
                tool.Options = _options;
            }
        }

        protected void SendMessage(string type, string content)
        {
            _messageCallback?.Invoke(type, content);
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
                new EditFile(),
                new AppendToFile(),
                new SearchCode(),
                new RunCommand(),
                new DisplayText(),
                new AskUser()
            ];
        }

        /// <summary>
        /// Rebuilds the tools list. Call this from derived class constructors
        /// after setting fields that CreateTools() depends on.
        /// </summary>
        protected void RebuildTools()
        {
            _tools = CreateTools();
            foreach (var tool in _tools)
            {
                tool.MessageCallback = _messageCallback;
                tool.Options = _options;
            }
        }

        public ILlmProvider Provider => _llmProvider;
        public IReadOnlyList<Tool> Tools => _tools;
        public string ProviderName => _llmProvider.Name;
        public AgentOptions Options => _options;

        /// <summary>
        /// Adds a tool to the agent's tool list. Use this to inject context-specific tools
        /// that need to be added after agent construction.
        /// </summary>
        /// <param name="tool">The tool to add</param>
        public void AddTool(Tool tool)
        {
            tool.MessageCallback = _messageCallback;
            tool.Options = _options;
            _tools.Add(tool);
        }

        /// <summary>
        /// Removes a tool from the agent's tool list by name.
        /// </summary>
        /// <param name="toolName">Name of the tool to remove</param>
        /// <returns>True if the tool was found and removed, false otherwise</returns>
        public bool RemoveTool(string toolName)
        {
            var tool = _tools.FirstOrDefault(t => t.Name == toolName);
            if (tool != null)
            {
                _tools.Remove(tool);
                return true;
            }
            return false;
        }
        
        // Legacy properties for backward compatibility
        public string WorkingDirectory => _options.WorkingDirectory;
        public bool Verbose => _options.Verbose;

        /// <summary>
        /// Returns common file operation guidelines for agents.
        /// Use this in system prompts to reduce duplication.
        /// </summary>
        protected static string GetFileOperationGuidelines()
        {
            return @"- Always explore the workspace first with list_files before making assumptions
- Read existing files before modifying them with read_file
- Use edit_file for surgical changes to existing files (preserves other content)
- Use write_file only for creating new files or completely replacing file content
- Use append_to_file to add content to the end of a file
- When making multiple changes to the same file, use edit_file for each change or read the file first, make all changes, then write once";
        }

        /// <summary>
        /// Returns common best practices for agents.
        /// Use this in system prompts to reduce duplication.
        /// </summary>
        protected static string GetCommonBestPractices()
        {
            return @"- Test your code after making changes
- If something fails, analyze the error and try a different approach
- Be methodical and thorough";
        }

        public async Task<List<Message>> RunAsync(string task, int? maxIterations = null)
        {
            var conversation = new List<Message>
            {
                new() { Role = "user", Content = task }
            };
            return await RunWithHistoryAsync(conversation, maxIterations);
        }

        /// <summary>
        /// Continues a conversation with existing message history.
        /// Use this for multi-turn conversations where context must be preserved.
        /// </summary>
        /// <param name="conversationHistory">Existing conversation messages</param>
        /// <param name="newUserMessage">New user message to add</param>
        /// <param name="maxIterations">Maximum iterations for this turn</param>
        /// <returns>Updated conversation including new messages</returns>
        public async Task<List<Message>> ContinueAsync(List<Message> conversationHistory, string newUserMessage, int? maxIterations = null)
        {
            var conversation = new List<Message>(conversationHistory);
            conversation.Add(new Message { Role = "user", Content = newUserMessage });
            return await RunWithHistoryAsync(conversation, maxIterations);
        }

        /// <summary>
        /// Runs the agent loop with an existing conversation history.
        /// </summary>
        private async Task<List<Message>> RunWithHistoryAsync(List<Message> conversation, int? maxIterations = null)
        {
            // Use streaming if enabled
            if (_options.EnableStreaming)
            {
                try
                {
                    return await RunWithHistoryStreamingAsync(conversation, maxIterations);
                }
                catch (Exception ex) when (_options.StreamingFallbackToSync)
                {
                    SendMessage("warning", $"Streaming failed: {ex.Message}. Falling back to synchronous mode.");
                    // Fall through to synchronous execution
                }
            }

            return await RunWithHistorySyncAsync(conversation, maxIterations);
        }

        /// <summary>
        /// Runs the agent loop with streaming responses.
        /// Note: Streaming mode currently only supports text responses. Tool calls fall back to sync mode.
        /// </summary>
        private async Task<List<Message>> RunWithHistoryStreamingAsync(List<Message> conversation, int? maxIterations = null)
        {
            var maxIter = maxIterations ?? _options.MaxIterations;

            // Streaming currently only supports single-turn text responses
            // Multi-turn with tool calls requires sync mode
            if (_options.Verbose)
            {
                SendMessage("info", "ITERATION 1 (streaming)");
            }

            var streamingResponse = await _llmProvider.SendMessageStreamingAsync(conversation, _tools, SystemPrompt);

            if (!string.IsNullOrEmpty(streamingResponse.Error))
            {
                SendMessage("error", $"Streaming error: {streamingResponse.Error}");
                throw new InvalidOperationException($"Streaming failed: {streamingResponse.Error}");
            }

            // Collect the full response from stream
            var fullText = new System.Text.StringBuilder();
            var stream = await streamingResponse.GetStreamAsync();

            await foreach (var chunk in stream)
            {
                fullText.Append(chunk);
                // Send streaming chunks to callback for real-time display
                SendMessage("assistant_stream", chunk);
            }

            var responseText = fullText.ToString();

            // For streaming, we only get text content (tool calls not yet supported in streaming mode)
            var response = new LlmResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock>
                {
                    new ContentBlock { Type = "text", Text = responseText }
                }
            };

            if (_options.Verbose)
            {
                SendMessage("info", $"Stop reason: {response.StopReason}");
            }

            // Add assistant response to conversation
            conversation.Add(new Message
            {
                Role = "assistant",
                Content = response.Content
            });

            // Streaming mode currently only supports text responses (no tool calls)
            SendMessage("assistant_final", responseText);
            return conversation;
        }

        /// <summary>
        /// Runs the agent loop with synchronous (non-streaming) responses.
        /// </summary>
        private async Task<List<Message>> RunWithHistorySyncAsync(List<Message> conversation, int? maxIterations = null)
        {
            var maxIter = maxIterations ?? _options.MaxIterations;

            for (int iteration = 1; iteration <= maxIter; iteration++)
            {
                if (_options.Verbose)
                {
                    SendMessage("info", $"ITERATION {iteration}");
                }

                var response = await _llmProvider.SendMessageAsync(conversation, _tools, SystemPrompt);

                if (_options.Verbose)
                {
                    SendMessage("info", $"Stop reason: {response.StopReason}");
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
                            var toolCallMsg = $"Tool: {block.Name}\nInput: {JsonSerializer.Serialize(block.Input)}";
                            SendMessage("tool_call", toolCallMsg);

                            var tool = _tools.FirstOrDefault(t => t.Name == block.Name);
                            var result = tool != null
                                ? tool.Execute(_options.WorkingDirectory, block.Input ?? [])
                                : $"Error: Unknown tool '{block.Name}'";

                            // Check if tool execution resulted in an error
                            if (result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                            {
                                hasErrors = true;
                            }

                            var preview = result.Length > 500 ? string.Concat(result.AsSpan(0, 500), "...") : result;
                            SendMessage("tool_result", $"Result from {block.Name}:\n{preview}");

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
                        if (hasTextContent && _options.Verbose)
                        {
                            foreach (var block in (response.Content ?? []).Where(b => b.Type == "text"))
                            {
                                SendMessage("assistant", block.Text ?? "");
                            }
                        }

                        // If we hit max iterations after tool calls, warn and stop
                        if (iteration >= maxIter)
                        {
                            SendMessage("warning", $"Maximum iterations ({maxIter}) reached. Task may be incomplete.");
                            return conversation;
                        }

                        // If all tools failed, no point continuing - let agent know and give one more chance
                        if (hasErrors && toolResults.Count > 0 && toolResults.All(r => 
                            r.GetType().GetProperty("content")?.GetValue(r)?.ToString()?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ?? false))
                        {
                            if (_options.Verbose)
                            {
                                SendMessage("warning", "All tool executions failed. Giving agent one final response...");
                            }
                        }
                        break;

                    case "end_turn":
                        // Agent finished - send response
                        foreach (var block in (response.Content ?? Enumerable.Empty<ContentBlock>()).Where(b => b.Type == "text"))
                        {
                            SendMessage("assistant_final", block.Text ?? "");
                        }
                        return conversation;

                    case "error":
                        // Error occurred - stop immediately
                        SendMessage("error", "Error occurred during LLM request. Stopping.");
                        
                        // Add error message to content so it can be detected by error handlers
                        if (response.Content == null || response.Content.Count == 0 || !response.Content.Any(b => b.Type == "text"))
                        {
                            if (response.Content == null)
                            {
                                response.Content = new List<ContentBlock>();
                            }
                            response.Content.Add(new ContentBlock 
                            { 
                                Type = "text", 
                                Text = "Error: An error occurred during LLM request."
                            });
                            // Update the assistant message with the error text
                            conversation[^1] = new Message
                            {
                                Role = "assistant",
                                Content = response.Content
                            };
                        }
                        return conversation;

                    case "NotConfigured":
                        // Provider not configured - stop immediately
                        SendMessage("error", $"Provider '{_llmProvider.Name}' is not properly configured.");
                        
                        // Add error message to content so it can be detected by error handlers
                        if (response.Content == null || response.Content.Count == 0 || !response.Content.Any(b => b.Type == "text"))
                        {
                            if (response.Content == null)
                            {
                                response.Content = new List<ContentBlock>();
                            }
                            response.Content.Add(new ContentBlock 
                            { 
                                Type = "text", 
                                Text = $"Error: Provider '{_llmProvider.Name}' is not properly configured."
                            });
                            // Update the assistant message with the error text
                            conversation[^1] = new Message
                            {
                                Role = "assistant",
                                Content = response.Content
                            };
                        }
                        return conversation;

                    default:
                        // Unexpected stop reason - stop to be safe
                        if (_options.Verbose)
                        {
                            SendMessage("warning", $"Unexpected stop reason: {response.StopReason ?? "unknown"}. Stopping.");
                        }
                        return conversation;
                }
            }

            // If we exit the loop naturally, we hit max iterations
            if (_options.Verbose)
            {
                SendMessage("warning", $"Maximum iterations ({maxIter}) reached.");
            }

            return conversation;
        }
    }
}