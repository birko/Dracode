using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public class LlamaCppProvider : LlmProviderBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly string _baseUrl;

        public override string Name => $"llama.cpp ({_model})";

        public LlamaCppProvider(string model = "default", string baseUrl = "http://localhost:8080")
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
                    tools = BuildOpenAiStyleTools(tools),
                    temperature = 0.7,
                    max_tokens = -1 // llama.cpp uses -1 for unlimited
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    SendMessage("error", $"llama.cpp API Error: {response.StatusCode}");
                    SendMessage("error", $"Response: {responseJson}");
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                return ParseResponse(responseJson);
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error calling llama.cpp API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        protected override bool IsConfigured() => !string.IsNullOrWhiteSpace(_baseUrl);

        private LlmResponse ParseResponse(string responseJson)
        {
            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var llmResponse = new LlmResponse { Content = [] };

            // Check for errors
            if (result.TryGetProperty("error", out var error))
            {
                var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                SendMessage("error", $"llama.cpp returned error: {errorMessage}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }

            // llama.cpp uses OpenAI-compatible format with choices array
            if (!result.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                SendMessage("error", "Invalid response format: missing choices array");
                return new LlmResponse { StopReason = "error", Content = [] };
            }

            var choice = choices[0];
            var message = choice.GetProperty("message");

            // Check for tool calls
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
                        Id = toolCall.TryGetProperty("id", out var idProp) ? idProp.GetString() : Guid.NewGuid().ToString(),
                        Name = function.GetProperty("name").GetString(),
                        Input = args
                    });
                }
            }
            else
            {
                // Regular text response
                llmResponse.StopReason = "end_turn";
                if (message.TryGetProperty("content", out var textContent))
                {
                    var text = textContent.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        llmResponse.Content.Add(new ContentBlock { Type = "text", Text = text });
                    }
                }
            }

            return llmResponse;
        }
    }
}
