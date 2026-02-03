using DraCode.Agent.Tools;
using System.Net;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    /// <summary>
    /// Configuration for retry behavior on transient failures
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// Maximum number of retry attempts (default: 3)
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Initial delay before first retry in milliseconds (default: 1000ms)
        /// </summary>
        public int InitialDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum delay between retries in milliseconds (default: 30000ms)
        /// </summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// Multiplier for exponential backoff (default: 2.0)
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Whether to add jitter to delays to prevent thundering herd (default: true)
        /// </summary>
        public bool AddJitter { get; set; } = true;

        /// <summary>
        /// Default retry policy with sensible defaults
        /// </summary>
        public static RetryPolicy Default => new();

        /// <summary>
        /// No retry policy - fails immediately on first error
        /// </summary>
        public static RetryPolicy None => new() { MaxRetries = 0 };
    }

    public abstract class LlmProviderBase : ILlmProvider
    {
        public abstract string Name { get; }
        public Action<string, string>? MessageCallback { get; set; }

        /// <summary>
        /// Retry policy for transient failures. Can be overridden by derived classes.
        /// </summary>
        protected virtual RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

        private static readonly Random _jitterRandom = new();

        public abstract Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt);

        protected void SendMessage(string type, string message)
        {
            MessageCallback?.Invoke(type, message);
        }

        // Abstract method that each provider implements based on their configuration needs
        protected abstract bool IsConfigured();

        /// <summary>
        /// Sends an HTTP request with retry logic for transient failures.
        /// Implements exponential backoff with optional jitter.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use</param>
        /// <param name="requestFactory">Factory function that creates the request (called on each retry)</param>
        /// <param name="providerName">Name of the provider for logging</param>
        /// <returns>The HTTP response, or null if all retries failed</returns>
        protected async Task<(HttpResponseMessage? Response, string? ResponseBody)> SendWithRetryAsync(
            HttpClient httpClient,
            Func<HttpRequestMessage> requestFactory,
            string providerName)
        {
            var policy = RetryPolicy;
            var attempt = 0;
            var delay = policy.InitialDelayMs;
            Exception? lastException = null;
            HttpResponseMessage? lastResponse = null;
            string? lastResponseBody = null;

            while (attempt <= policy.MaxRetries)
            {
                try
                {
                    using var request = requestFactory();
                    var response = await httpClient.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    // Success - return immediately
                    if (response.IsSuccessStatusCode)
                    {
                        return (response, responseBody);
                    }

                    lastResponse = response;
                    lastResponseBody = responseBody;

                    // Check if we should retry based on status code
                    if (!IsRetryableStatusCode(response.StatusCode))
                    {
                        // Non-retryable error (4xx except 429)
                        SendMessage("error", $"{providerName} API Error: {response.StatusCode}");
                        return (response, responseBody);
                    }

                    // Retryable error - check if we have retries left
                    if (attempt >= policy.MaxRetries)
                    {
                        SendMessage("error", $"{providerName} API Error after {attempt + 1} attempts: {response.StatusCode}");
                        return (response, responseBody);
                    }

                    // Handle rate limiting with Retry-After header
                    var retryAfter = GetRetryAfterDelay(response);
                    var actualDelay = retryAfter ?? delay;

                    SendMessage("warning", $"{providerName}: {response.StatusCode}, retrying in {actualDelay}ms (attempt {attempt + 1}/{policy.MaxRetries + 1})");

                    await Task.Delay(actualDelay);

                    // Calculate next delay with exponential backoff
                    delay = CalculateNextDelay(delay, policy);
                    attempt++;
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;

                    if (attempt >= policy.MaxRetries)
                    {
                        SendMessage("error", $"{providerName}: Network error after {attempt + 1} attempts: {ex.Message}");
                        throw;
                    }

                    SendMessage("warning", $"{providerName}: Network error, retrying in {delay}ms (attempt {attempt + 1}/{policy.MaxRetries + 1}): {ex.Message}");

                    await Task.Delay(delay);
                    delay = CalculateNextDelay(delay, policy);
                    attempt++;
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    lastException = ex;

                    if (attempt >= policy.MaxRetries)
                    {
                        SendMessage("error", $"{providerName}: Timeout after {attempt + 1} attempts");
                        throw;
                    }

                    SendMessage("warning", $"{providerName}: Timeout, retrying in {delay}ms (attempt {attempt + 1}/{policy.MaxRetries + 1})");

                    await Task.Delay(delay);
                    delay = CalculateNextDelay(delay, policy);
                    attempt++;
                }
            }

            // Should not reach here, but handle gracefully
            if (lastException != null)
            {
                throw lastException;
            }

            return (lastResponse, lastResponseBody);
        }

        /// <summary>
        /// Determines if a status code is retryable
        /// </summary>
        private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.TooManyRequests => true,           // 429 - Rate limited
                HttpStatusCode.InternalServerError => true,       // 500
                HttpStatusCode.BadGateway => true,                // 502
                HttpStatusCode.ServiceUnavailable => true,        // 503
                HttpStatusCode.GatewayTimeout => true,            // 504
                HttpStatusCode.RequestTimeout => true,            // 408
                _ => (int)statusCode >= 500                       // Any other 5xx
            };
        }

        /// <summary>
        /// Gets retry delay from Retry-After header if present
        /// </summary>
        private static int? GetRetryAfterDelay(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Retry-After", out var values))
            {
                var retryAfter = values.FirstOrDefault();
                if (retryAfter != null)
                {
                    // Try parsing as seconds
                    if (int.TryParse(retryAfter, out var seconds))
                    {
                        return seconds * 1000;
                    }

                    // Try parsing as HTTP date
                    if (DateTimeOffset.TryParse(retryAfter, out var date))
                    {
                        var delay = date - DateTimeOffset.UtcNow;
                        if (delay.TotalMilliseconds > 0)
                        {
                            return (int)delay.TotalMilliseconds;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Calculates next delay with exponential backoff and optional jitter
        /// </summary>
        private static int CalculateNextDelay(int currentDelay, RetryPolicy policy)
        {
            var nextDelay = (int)(currentDelay * policy.BackoffMultiplier);

            // Add jitter (±25% of delay)
            if (policy.AddJitter)
            {
                var jitter = (int)(nextDelay * 0.25);
                nextDelay += _jitterRandom.Next(-jitter, jitter);
            }

            // Clamp to max delay
            return Math.Min(nextDelay, policy.MaxDelayMs);
        }

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
