using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    /// <summary>
    /// Base class for providers that implement the OpenAI-compatible API format.
    /// Used by local inference servers like llama.cpp, vLLM, SGLang, etc.
    /// </summary>
    public abstract class OpenAiCompatibleProviderBase : LlmProviderBase
    {
        protected readonly HttpClient HttpClient;
        protected readonly string Model;
        protected readonly string BaseUrl;

        /// <summary>
        /// Display name for this provider (e.g., "llama.cpp", "vLLM", "SGLang")
        /// </summary>
        protected abstract string ProviderName { get; }

        /// <summary>
        /// Max tokens to request. Override in derived class if needed.
        /// Default is 8192. Use -1 for unlimited (llama.cpp style).
        /// </summary>
        protected virtual int MaxTokens => 8192;

        /// <summary>
        /// Temperature for generation. Override in derived class if needed.
        /// </summary>
        protected virtual double Temperature => 0.7;

        /// <summary>
        /// HTTP timeout for requests. Local models can be slow.
        /// </summary>
        protected virtual TimeSpan RequestTimeout => TimeSpan.FromMinutes(5);

        public override string Name => $"{ProviderName} ({Model})";

        protected OpenAiCompatibleProviderBase(string model, string baseUrl, string? apiKey = null)
        {
            Model = model;
            BaseUrl = baseUrl.TrimEnd('/');
            HttpClient = new HttpClient
            {
                Timeout = RequestTimeout
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                HttpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }
        }

        public override async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured()) return NotConfigured();

            try
            {
                var payload = BuildPayload(messages, tools, systemPrompt);
                var json = JsonSerializer.Serialize(payload);
                var url = $"{BaseUrl}/v1/chat/completions";

                // Use retry logic for transient failures
                var (response, responseJson) = await SendWithRetryAsync(
                    HttpClient,
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        return request;
                    },
                    ProviderName);

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
                SendMessage("error", $"Error calling {ProviderName} API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        /// <summary>
        /// Build the request payload. Override to customize payload structure.
        /// </summary>
        protected virtual object BuildPayload(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            return new
            {
                model = Model,
                messages = BuildOpenAiStyleMessages(messages, systemPrompt),
                stream = false,
                tools = BuildOpenAiStyleTools(tools),
                temperature = Temperature,
                max_tokens = MaxTokens
            };
        }

        protected override bool IsConfigured() => !string.IsNullOrWhiteSpace(BaseUrl);

        /// <summary>
        /// Parse the OpenAI-compatible response. Handles missing tool_call IDs gracefully.
        /// </summary>
        protected virtual LlmResponse ParseResponse(string responseJson)
        {
            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var llmResponse = new LlmResponse { Content = [] };

            // Check for errors
            if (result.TryGetProperty("error", out var error))
            {
                var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                SendMessage("error", $"{ProviderName} returned error: {errorMessage}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }

            // OpenAI-compatible format uses choices array
            if (!result.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                SendMessage("error", "Invalid response format: missing choices array");
                return new LlmResponse { StopReason = "error", Content = [] };
            }

            var choice = choices[0];
            var message = choice.GetProperty("message");

            // Check for tool calls (vLLM can return tool_calls: null instead of an array)
            if (message.TryGetProperty("tool_calls", out var toolCalls)
                && toolCalls.ValueKind == JsonValueKind.Array
                && toolCalls.GetArrayLength() > 0)
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
                        // Some local servers don't return tool_call IDs, generate one if missing
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

        public override async Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured())
            {
                return new LlmStreamingResponse
                {
                    GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(
                        new InvalidOperationException($"{ProviderName} provider is not configured")),
                    Error = "Not configured"
                };
            }

            try
            {
                var payload = BuildStreamingPayload(messages, tools, systemPrompt);
                var json = JsonSerializer.Serialize(payload);
                var url = $"{BaseUrl}/v1/chat/completions";

                var response = await SendStreamingWithRetryAsync(
                    HttpClient,
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        return request;
                    },
                    ProviderName);

                if (response == null)
                {
                    return new LlmStreamingResponse
                    {
                        GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(
                            new HttpRequestException($"Failed to connect to {ProviderName} API after retries")),
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
                SendMessage("error", $"Error calling {ProviderName} streaming API: {ex.Message}");
                return new LlmStreamingResponse
                {
                    GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(ex),
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Build streaming payload. Override to customize.
        /// </summary>
        protected virtual object BuildStreamingPayload(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            return new
            {
                model = Model,
                messages = BuildOpenAiStyleMessages(messages, systemPrompt),
                stream = true,
                tools = BuildOpenAiStyleTools(tools),
                temperature = Temperature,
                max_tokens = MaxTokens
            };
        }
    }
}
