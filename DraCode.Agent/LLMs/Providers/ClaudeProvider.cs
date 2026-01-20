using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public class ClaudeProvider : ILlmProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string? _baseUrl;

        public string Name => "Claude";

        public ClaudeProvider(string apiKey, string model, string baseUrl = "https://api.anthropic.com/v1/messages")
        {
            _apiKey = apiKey;
            _model = model;
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        public async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
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
                    Console.Error.WriteLine($"Claude API Error: {response.StatusCode}");
                    Console.Error.WriteLine($"Response: {responseJson}");
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                return ParseResponse(responseJson);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error calling Claude API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        private bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_model);

        private static LlmResponse NotConfigured() => new()
        {
            StopReason = "NotConfigured",
            Content = []
        };

        private static object BuildRequestPayload(IEnumerable<Message> messages, IEnumerable<Tool> tools, string systemPrompt, string model) => new
        {
            model,
            max_tokens = 4096,
            system = systemPrompt,
            messages,
            tools = tools.Select(t => new { name = t.Name, description = t.Description, input_schema = t.InputSchema }).ToList()
        };

        private static LlmResponse ParseResponse(string responseJson)
        {
            try
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                
                // Check for API errors
                if (result.TryGetProperty("error", out var error))
                {
                    var errorType = error.TryGetProperty("type", out var type) ? type.GetString() : "unknown";
                    var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                    Console.Error.WriteLine($"Claude API returned error: {errorType} - {errorMessage}");
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
                Console.Error.WriteLine($"Error parsing Claude response: {ex.Message}");
                Console.Error.WriteLine($"Response: {responseJson}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }
    }
}
