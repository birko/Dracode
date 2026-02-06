using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    /// <summary>
    /// Provider for Z.AI (formerly Zhipu AI) GLM models.
    /// Z.AI offers OpenAI-compatible API with GLM-4.5, GLM-4.6, GLM-4.7 models.
    /// API endpoint: https://api.z.ai/api/paas/v4/chat/completions
    /// China endpoint: https://open.bigmodel.cn/api/paas/v4/chat/completions
    /// </summary>
    public class ZAiProvider : LlmProviderBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _baseUrl;
        private readonly bool _enableDeepThinking;

        /// <summary>
        /// International API endpoint
        /// </summary>
        public const string InternationalEndpoint = "https://api.z.ai/api/paas/v4";

        /// <summary>
        /// China mainland API endpoint
        /// </summary>
        public const string ChinaEndpoint = "https://open.bigmodel.cn/api/paas/v4";

        public override string Name => $"Z.AI ({_model})";

        /// <summary>
        /// Creates a new Z.AI provider
        /// </summary>
        /// <param name="apiKey">Z.AI API key (ZHIPU_API_KEY)</param>
        /// <param name="model">Model name (glm-4.5-flash, glm-4.6-flash, glm-4.7, etc.)</param>
        /// <param name="baseUrl">API base URL (defaults to international endpoint)</param>
        /// <param name="enableDeepThinking">Enable Deep Thinking mode for supported models</param>
        public ZAiProvider(
            string apiKey,
            string model = "glm-4.5-flash",
            string? baseUrl = null,
            bool enableDeepThinking = false)
        {
            _apiKey = apiKey;
            _model = model;
            _baseUrl = (baseUrl ?? InternationalEndpoint).TrimEnd('/');
            _enableDeepThinking = enableDeepThinking;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public override async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured()) return NotConfigured();

            try
            {
                var payload = BuildPayload(messages, tools, systemPrompt);
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                var url = $"{_baseUrl}/chat/completions";

                SendMessage("debug", $"Z.AI request to {url}");

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
                SendMessage("error", $"Error calling Z.AI API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        protected override bool IsConfigured() => !string.IsNullOrWhiteSpace(_apiKey);

        private object BuildPayload(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            var openAiMessages = BuildOpenAiStyleMessages(messages, systemPrompt);
            var openAiTools = BuildOpenAiStyleTools(tools);

            // Base payload
            var payload = new Dictionary<string, object?>
            {
                ["model"] = _model,
                ["messages"] = openAiMessages,
                ["stream"] = false,
                ["max_tokens"] = 8192,  // GLM models max output tokens
                ["temperature"] = 0.7
            };

            // Add tools if any
            if (tools.Any())
            {
                payload["tools"] = openAiTools;
            }

            // Add Deep Thinking mode if enabled (for supported models like GLM-4.5)
            if (_enableDeepThinking)
            {
                payload["thinking"] = new { type = "enabled" };
            }

            return payload;
        }

        private LlmResponse ParseResponse(string responseJson)
        {
            try
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                var llmResponse = new LlmResponse { Content = [] };

                // Check for errors
                if (result.TryGetProperty("error", out var error))
                {
                    var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                    SendMessage("error", $"Z.AI returned error: {errorMessage}");
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                // Standard OpenAI-compatible format
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
                        var args = argumentsJson is not null
                            ? JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson)
                            : [];
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

                    // Check for thinking content (Deep Thinking mode)
                    if (message.TryGetProperty("thinking_content", out var thinkingContent))
                    {
                        var thinking = thinkingContent.GetString();
                        if (!string.IsNullOrEmpty(thinking))
                        {
                            SendMessage("thinking", thinking);
                        }
                    }

                    if (message.TryGetProperty("content", out var textContent))
                    {
                        var text = textContent.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            llmResponse.Content.Add(new ContentBlock { Type = "text", Text = text });
                        }
                    }
                }

                // Log usage if available
                if (result.TryGetProperty("usage", out var usage))
                {
                    var promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                    var completionTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                    SendMessage("usage", $"Tokens - Prompt: {promptTokens}, Completion: {completionTokens}, Total: {promptTokens + completionTokens}");
                }

                return llmResponse;
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error parsing Z.AI response: {ex.Message}");
                SendMessage("error", $"Response: {responseJson}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        /// <summary>
        /// Available Z.AI models
        /// </summary>
        public static class Models
        {
            // GLM-4.5 series
            public const string Glm45Flash = "glm-4.5-flash";
            public const string Glm45Air = "glm-4.5-air";
            public const string Glm45 = "glm-4.5";

            // GLM-4.6 series
            public const string Glm46Flash = "glm-4.6-flash";

            // GLM-4.7 series (200K context)
            public const string Glm47 = "glm-4.7";

            // Vision models
            public const string Glm4VPlus = "glm-4v-plus";
            public const string Glm4V = "glm-4v";

            // Code models
            public const string CodeGeeX4 = "codegeex-4";

            // Legacy models
            public const string Glm4Plus = "glm-4-plus";
            public const string Glm4 = "glm-4";
            public const string Glm4Air = "glm-4-air";
            public const string Glm4Flash = "glm-4-flash";
        }

        public override async Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured())
            {
                return new LlmStreamingResponse
                {
                    GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(
                        new InvalidOperationException("Z.AI provider is not configured")),
                    Error = "Not configured"
                };
            }

            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["model"] = _model,
                    ["messages"] = BuildOpenAiStyleMessages(messages, systemPrompt),
                    ["stream"] = true
                };

                if (tools != null && tools.Count > 0)
                {
                    payload["tools"] = BuildOpenAiStyleTools(tools);
                }

                var json = JsonSerializer.Serialize(payload);

                var response = await SendStreamingWithRetryAsync(
                    _httpClient,
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        return request;
                    },
                    Name);

                if (response == null)
                {
                    return new LlmStreamingResponse
                    {
                        GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(
                            new HttpRequestException("Failed to connect to Z.AI API after retries")),
                        Error = "Connection failed"
                    };
                }

                return new LlmStreamingResponse
                {
                    GetStreamAsync = async () =>
                    {
                        var stream = await response.Content.ReadAsStreamAsync();
                        var sseStream = ParseSseStream(stream);
                        return ParseOpenAiStreamChunks(sseStream);
                    },
                    IsComplete = false
                };
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error calling Z.AI streaming API: {ex.Message}");
                return new LlmStreamingResponse
                {
                    GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(ex),
                    Error = ex.Message
                };
            }
        }
    }
}
