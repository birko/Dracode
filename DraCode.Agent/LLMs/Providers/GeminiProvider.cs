using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public class GeminiProvider : LlmProviderBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string? _baseUrl;

        public override string Name => "Gemini";

        public GeminiProvider(string apiKey, string model = "gemini-2.0-flash-exp", string baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/")
        {
            _apiKey = apiKey;
            _model = model;
            _httpClient = new HttpClient();
            _baseUrl = baseUrl;
        }

        public override async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured()) return NotConfigured();

            try
            {
                var payload = BuildRequestPayload(messages, tools, systemPrompt);
                var json = JsonSerializer.Serialize(payload);
                var url = $"{_baseUrl}{_model}:generateContent?key={_apiKey}";

                // Use retry logic for transient failures
                var (response, responseJson) = await SendWithRetryAsync(
                    _httpClient,
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        return request;
                    },
                    Name);

                if (response == null || responseJson == null)
                {
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                if (!response.IsSuccessStatusCode)
                {
                    SendMessage("error", $"Response: {responseJson}");
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                return ParseResponse(responseJson, MessageCallback);
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error calling Gemini API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        protected override bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_model);

        private static object BuildRequestPayload(IEnumerable<Message> messages, IEnumerable<Tool> tools, string systemPrompt)
        {
            var contents = new List<object>();
            
            foreach (var message in messages)
            {
                var role = message.Role == "assistant" ? "model" : "user";
                var parts = new List<object>();

                // Handle different content types
                if (message.Content is string textContent)
                {
                    parts.Add(new { text = textContent });
                }
                else if (message.Content is IEnumerable<ContentBlock> blocks)
                {
                    // Handle ContentBlock objects from assistant or parsed responses
                    foreach (var block in blocks)
                    {
                        if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
                        {
                            parts.Add(new { text = block.Text });
                        }
                        else if (block.Type == "tool_use" && block.Name != null)
                        {
                            // Gemini represents tool calls as functionCall parts
                            parts.Add(new
                            {
                                functionCall = new
                                {
                                    name = block.Name,
                                    args = block.Input ?? new Dictionary<string, object>()
                                }
                            });
                        }
                    }
                }
                else if (message.Content is List<object> contentList)
                {
                    // Tool results from agent
                    foreach (var item in contentList)
                    {
                        if (item is JsonElement jsonElement)
                        {
                            if (jsonElement.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "tool_result")
                            {
                                var toolUseId = jsonElement.GetProperty("tool_use_id").GetString();
                                var resultContent = jsonElement.GetProperty("content").GetString();
                                
                                parts.Add(new
                                {
                                    functionResponse = new
                                    {
                                        name = toolUseId,
                                        response = new { result = resultContent }
                                    }
                                });
                            }
                        }
                        else
                        {
                            // Check if it's an anonymous object with type property
                            var itemType = item.GetType();
                            var typeProp = itemType.GetProperty("type");
                            
                            if (typeProp != null)
                            {
                                var typeValue = typeProp.GetValue(item)?.ToString();
                                if (typeValue == "tool_result")
                                {
                                    var toolUseIdProp = itemType.GetProperty("tool_use_id");
                                    var contentProp = itemType.GetProperty("content");
                                    
                                    if (toolUseIdProp != null && contentProp != null)
                                    {
                                        var toolUseId = toolUseIdProp.GetValue(item)?.ToString();
                                        var resultContent = contentProp.GetValue(item)?.ToString();
                                        
                                        parts.Add(new
                                        {
                                            functionResponse = new
                                            {
                                                name = toolUseId,
                                                response = new { result = resultContent }
                                            }
                                        });
                                    }
                                }
                            }
                            else
                            {
                                // Fallback: convert to string
                                parts.Add(new { text = item?.ToString() ?? string.Empty });
                            }
                        }
                    }
                }
                else if (message.Content != null)
                {
                    // Fallback for unknown types
                    parts.Add(new { text = message.Content.ToString() ?? string.Empty });
                }

                if (parts.Count > 0)
                {
                    contents.Add(new { role, parts = parts.ToArray() });
                }
            }

            var payload = new
            {
                contents = contents.ToArray(),
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } }
            };

            // Only add tools if there are any
            if (tools.Any())
            {
                return new
                {
                    contents = payload.contents,
                    systemInstruction = payload.systemInstruction,
                    tools = new[]
                    {
                        new
                        {
                            functionDeclarations = tools.Select(t => new 
                            { 
                                name = t.Name, 
                                description = t.Description, 
                                parameters = t.InputSchema 
                            }).ToArray()
                        }
                    }
                };
            }

            return payload;
        }

        private static LlmResponse ParseResponse(string responseJson, Action<string, string>? messageCallback = null)
        {
            try
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                var llmResponse = new LlmResponse { Content = [] };

                // Check for API errors
                if (result.TryGetProperty("error", out var error))
                {
                    var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                    messageCallback?.Invoke("error", $"Gemini API returned error: {errorMessage}");
                    llmResponse.StopReason = "error";
                    return llmResponse;
                }

                if (result.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    
                    // Check finish reason
                    if (candidate.TryGetProperty("finishReason", out var finishReason))
                    {
                        var reason = finishReason.GetString();
                        if (reason == "SAFETY" || reason == "RECITATION")
                        {
                            llmResponse.StopReason = "error";
                            messageCallback?.Invoke("error", $"Gemini blocked response: {reason}");
                            return llmResponse;
                        }
                    }

                    if (candidate.TryGetProperty("content", out var content) && 
                        content.TryGetProperty("parts", out var parts))
                    {
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var textElement))
                            {
                                llmResponse.StopReason = "end_turn";
                                llmResponse.Content.Add(new ContentBlock { Type = "text", Text = textElement.GetString() });
                            }
                            else if (part.TryGetProperty("functionCall", out var functionCall))
                            {
                                llmResponse.StopReason = "tool_use";
                                var args = new Dictionary<string, object>();
                                if (functionCall.TryGetProperty("args", out var argsElement))
                                {
                                    foreach (var prop in argsElement.EnumerateObject())
                                    {
                                        args[prop.Name] = prop.Value.ValueKind == JsonValueKind.String 
                                            ? prop.Value.GetString() ?? string.Empty
                                            : prop.Value.ToString();
                                    }
                                }
                                llmResponse.Content.Add(new ContentBlock
                                {
                                    Type = "tool_use",
                                    Id = Guid.NewGuid().ToString(),
                                    Name = functionCall.GetProperty("name").GetString(),
                                    Input = args
                                });
                            }
                        }
                    }
                }

                // If no stop reason was set, default to end_turn
                if (string.IsNullOrEmpty(llmResponse.StopReason))
                {
                    llmResponse.StopReason = "end_turn";
                }

                return llmResponse;
            }
            catch (Exception ex)
            {
                messageCallback?.Invoke("error", $"Error parsing Gemini response: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }
    }
}
