using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public class ClaudeProvider : LlmProviderBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string? _baseUrl;

        public override string Name => "Claude";

        public ClaudeProvider(string apiKey, string model, string baseUrl = "https://api.anthropic.com/v1/messages")
        {
            _apiKey = apiKey;
            _model = model;
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        public override async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured()) return NotConfigured();

            try
            {
                var payload = BuildRequestPayload(messages, tools, systemPrompt, _model);
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_baseUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    SendMessage("error", $"Claude API Error: {response.StatusCode}");
                    SendMessage("error", $"Response: {responseJson}");
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                return ParseResponse(responseJson, MessageCallback);
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error calling Claude API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        protected override bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_model);

        private static object BuildRequestPayload(IEnumerable<Message> messages, IEnumerable<Tool> tools, string systemPrompt, string model)
        {
            var claudeMessages = messages.Select(m =>
            {
                object content = m.Content ?? "";
                
                // If content is a list of ContentBlocks, convert to Claude format
                if (m.Content is IEnumerable<ContentBlock> blocks)
                {
                    var contentList = new List<Dictionary<string, object?>>();
                    foreach (var b in blocks)
                    {
                        var dict = new Dictionary<string, object?> { { "type", b.Type } };
                        
                        if (b.Type == "text")
                        {
                            dict["text"] = b.Text;
                        }
                        else if (b.Type == "tool_use")
                        {
                            dict["id"] = b.Id;
                            dict["name"] = b.Name;
                            dict["input"] = b.Input;
                        }
                        
                        contentList.Add(dict);
                    }
                    content = contentList;
                }
                
                return new { role = m.Role, content };
            }).ToList();
            
            return new
            {
                model,
                max_tokens = 4096,
                system = systemPrompt,
                messages = claudeMessages,
                tools = tools.Select(t => new { name = t.Name, description = t.Description, input_schema = t.InputSchema }).ToList()
            };
        }

        private static LlmResponse ParseResponse(string responseJson, Action<string, string>? messageCallback = null)
        {
            try
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                
                // Check for API errors
                if (result.TryGetProperty("error", out var error))
                {
                    var errorType = error.TryGetProperty("type", out var type) ? type.GetString() : "unknown";
                    var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                    messageCallback?.Invoke("error", $"Claude API returned error: {errorType} - {errorMessage}");
                    return new LlmResponse { StopReason = "error", Content = [] };
                }
                
                var llmResponse = new LlmResponse 
                { 
                    StopReason = result.GetProperty("stop_reason").GetString(), 
                    Content = [] 
                };

                foreach (var block in result.GetProperty("content").EnumerateArray())
                {
                    var blockType = block.GetProperty("type").GetString();
                    if (blockType == "text")
                    {
                        llmResponse.Content.Add(new ContentBlock { Type = "text", Text = block.GetProperty("text").GetString() });
                    }
                    else if (blockType == "tool_use")
                    {
                        var inputDict = new Dictionary<string, object>();
                        foreach (var prop in block.GetProperty("input").EnumerateObject()) 
                        {
                            inputDict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String 
                                ? prop.Value.GetString() ?? string.Empty
                                : prop.Value.ToString();
                        }
                        llmResponse.Content.Add(new ContentBlock
                        {
                            Type = "tool_use",
                            Id = block.GetProperty("id").GetString(),
                            Name = block.GetProperty("name").GetString(),
                            Input = inputDict
                        });
                    }
                }

                return llmResponse;
            }
            catch (Exception ex)
            {
                messageCallback?.Invoke("error", $"Error parsing Claude response: {ex.Message}");
                messageCallback?.Invoke("error", $"Response: {responseJson}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }
    }
}
