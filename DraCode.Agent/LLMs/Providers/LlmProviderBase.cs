using DraCode.Agent.Tools;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public abstract class LlmProviderBase : ILlmProvider
    {
        public abstract string Name { get; }
        public Action<string, string>? MessageCallback { get; set; }

        public abstract Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt);

        protected void SendMessage(string type, string message)
        {
            MessageCallback?.Invoke(type, message);
        }

        // Abstract method that each provider implements based on their configuration needs
        protected abstract bool IsConfigured();

        protected static List<object> BuildOpenAiStyleMessages(IEnumerable<Message> messages, string systemPrompt)
        {
            var list = new List<object> { new { role = "system", content = systemPrompt } };
            foreach (var m in messages)
            {
                object? content = m.Content ?? "";
                
                // If content is a list of ContentBlocks, convert to OpenAI format
                if (m.Content is IEnumerable<ContentBlock> blocks)
                {
                    var blocksList = blocks.ToList();
                    var textBlocks = blocksList.Where(b => b.Type?.ToLowerInvariant() == "text").ToList();
                    var toolUseBlocks = blocksList.Where(b => b.Type?.ToLowerInvariant() == "tool_use").ToList();
                    
                    // For assistant messages with tool_use blocks, convert to OpenAI tool_calls format
                    if (m.Role == "assistant" && toolUseBlocks.Any())
                    {
                        var textContent = textBlocks.Any() && !string.IsNullOrEmpty(textBlocks[0].Text) 
                            ? textBlocks[0].Text 
                            : null;
                        
                        var toolCalls = toolUseBlocks.Select(b => new
                        {
                            id = b.Id,
                            type = "function",
                            function = new
                            {
                                name = b.Name,
                                arguments = System.Text.Json.JsonSerializer.Serialize(b.Input ?? new Dictionary<string, object>())
                            }
                        }).ToList();
                        
                        list.Add(new { role = m.Role, content = textContent, tool_calls = toolCalls });
                        continue;
                    }
                    
                    // For text-only blocks, extract text
                    if (textBlocks.Any())
                    {
                        content = textBlocks.Count == 1 ? textBlocks[0].Text : 
                            string.Join("\n", textBlocks.Select(b => b.Text));
                    }
                    else
                    {
                        content = "";
                    }
                }
                // If content is a list of objects (tool results from user), convert to OpenAI format
                else if (m.Content is IEnumerable<object> objs && objs.Any())
                {
                    var objsList = objs.ToList();
                    var firstObj = objsList.First();
                    var firstObjType = firstObj.GetType();
                    
                    // Check if these are tool_result objects
                    if (firstObjType.GetProperty("type") != null)
                    {
                        // For OpenAI, tool results should be individual messages with role="tool"
                        foreach (var obj in objsList)
                        {
                            var objType = obj.GetType();
                            var toolCallIdProp = objType.GetProperty("tool_use_id");
                            var contentProp = objType.GetProperty("content");
                            
                            if (toolCallIdProp != null && contentProp != null)
                            {
                                var toolCallId = toolCallIdProp.GetValue(obj)?.ToString();
                                var toolContent = contentProp.GetValue(obj)?.ToString() ?? "";
                                
                                list.Add(new { role = "tool", tool_call_id = toolCallId, content = toolContent });
                            }
                        }
                        continue;
                    }
                }
                
                list.Add(new { role = m.Role, content });
            }
            return list;
        }

        protected static object BuildOpenAiStyleTools(IEnumerable<Tool> tools) => tools.Select(t => new
        {
            type = "function",
            function = new { name = t.Name, description = t.Description, parameters = t.InputSchema }
        }).ToList();

        protected static LlmResponse ParseOpenAiStyleResponse(string responseJson, Action<string, string>? messageCallback = null)
        {
            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(responseJson);
                
                // Check for API errors
                if (result.TryGetProperty("error", out var error))
                {
                    var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                    var errorType = error.TryGetProperty("type", out var type) ? type.GetString() : "unknown";
                    messageCallback?.Invoke("error", $"API returned error: {errorType} - {errorMessage}");
                    return new LlmResponse { StopReason = "error", Content = [] };
                }
                
                var choice = result.GetProperty("choices")[0];
                var message = choice.GetProperty("message");

                var llmResponse = new LlmResponse { Content = [] };

                if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
                {
                    llmResponse.StopReason = "tool_use";
                    foreach (var toolCall in toolCalls.EnumerateArray())
                    {
                        var function = toolCall.GetProperty("function");
                        var argumentsJson = function.GetProperty("arguments").GetString();
                        var args = argumentsJson is not null ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson) : [];
                        llmResponse.Content.Add(new ContentBlock
                        {
                            Type = "tool_use",
                            Id = toolCall.GetProperty("id").GetString(),
                            Name = function.GetProperty("name").GetString(),
                            Input = args
                        });
                    }
                }
                else
                {
                    llmResponse.StopReason = "end_turn";
                    if (message.TryGetProperty("content", out var textContent))
                    {
                        llmResponse.Content.Add(new ContentBlock { Type = "text", Text = textContent.GetString() });
                    }
                }

                return llmResponse;
            }
            catch (Exception ex)
            {
                messageCallback?.Invoke("error", $"Error parsing OpenAI-style response: {ex.Message}");
                messageCallback?.Invoke("error", $"Response: {responseJson}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        protected static LlmResponse NotConfigured() => new() { StopReason = "NotConfigured", Content = [] };
    }
}
