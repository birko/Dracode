using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public class OllamaProvider : LlmProviderBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly string _baseUrl;

        public override string Name => $"Ollama ({_model})";

        public OllamaProvider(string model = "llama3.2", string baseUrl = "http://localhost:11434")
        {
            _model = model;
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // Local models can be slow
            };
        }

        public override async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured()) return NotConfigured();

            try
            {
                var payload = new
                {
                    model = _model,
                    messages = BuildOpenAiStyleMessages(messages, systemPrompt),
                    stream = false,
                    tools = BuildOpenAiStyleTools(tools)
                };
                var json = JsonSerializer.Serialize(payload);
                var url = $"{_baseUrl}/api/chat";

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

                return ParseResponse(responseJson);
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error calling Ollama API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        protected override bool IsConfigured() => !string.IsNullOrWhiteSpace(_model);

        private static List<object> BuildMessages(IEnumerable<Message> messages, string systemPrompt)
        {
            var list = new List<object> { new { role = "system", content = systemPrompt } };
            list.AddRange(messages.Select(m => new { role = m.Role, content = m.Content }));
            return list;
        }

        private static object BuildRequestPayload(IEnumerable<Message> messages, IEnumerable<Tool> tools, string systemPrompt) => new
        {
            model = "" /* replaced below */,
            messages = BuildMessages(messages, systemPrompt),
            stream = false,
            tools = tools.Select(t => new { type = "function", function = new { name = t.Name, description = t.Description, parameters = t.InputSchema } }).ToList()
        };

        private static LlmResponse ParseResponse(string responseJson)
        {
            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var message = result.GetProperty("message");
            var llmResponse = new LlmResponse { Content = [] };

            if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
            {
                llmResponse.StopReason = "tool_use";
                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    var argumentsJson = function.GetProperty("arguments").GetString();
                    var args = argumentsJson is not null ? JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson) : [];
                    llmResponse.Content.Add(new ContentBlock
                    {
                        Type = "tool_use",
                        Id = Guid.NewGuid().ToString(),
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
    }
}
