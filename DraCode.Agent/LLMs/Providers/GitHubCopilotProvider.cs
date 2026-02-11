using DraCode.Agent.Auth;
using DraCode.Agent.Tools;
using System.Text;
using System.Text.Json;

namespace DraCode.Agent.LLMs.Providers
{
    public class GitHubCopilotProvider : LlmProviderBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly string _baseUrl;
        private readonly string _clientId;
        private GitHubOAuthService _oauthService;
        private TokenInfo? _currentToken;

        public override string Name => "GitHub Copilot";

        public GitHubCopilotProvider(
            string clientId, 
            string model = "gpt-4o", 
            string baseUrl = "https://api.githubcopilot.com/chat/completions")
        {
            _clientId = clientId;
            _model = model;
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _oauthService = new GitHubOAuthService(clientId, messageCallback: MessageCallback);
        }

        private GitHubOAuthService GetOAuthService()
        {
            // Recreate the OAuth service with current callback if it has changed
            return new GitHubOAuthService(_clientId, messageCallback: MessageCallback);
        }

        public override async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            // Update OAuth service with current callback
            _oauthService = GetOAuthService();

            if (!await EnsureAuthenticatedAsync())
            {
                return NotConfigured();
            }

            var payload = new
            {
                model = _model,
                messages = BuildOpenAiStyleMessages(messages, systemPrompt),
                tools = BuildOpenAiStyleTools(tools),
                max_tokens = 16384  // GPT-4o max output tokens
            };

            var json = JsonSerializer.Serialize(payload);

            HttpRequestMessage CreateRequest()
            {
                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
                request.Headers.Add("Authorization", $"Bearer {_currentToken!.AccessToken}");
                request.Headers.Add("Editor-Version", "vscode/1.96.0");
                request.Headers.Add("Editor-Plugin-Version", "copilot-chat/0.23.2");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return request;
            }

            try
            {
                // Use retry logic for transient failures
                var (response, responseJson) = await SendWithRetryAsync(
                    _httpClient,
                    CreateRequest,
                    Name);

                if (response == null || responseJson == null)
                {
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                // Handle 401 separately - refresh token and retry once
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _currentToken = await _oauthService.GetValidTokenAsync(forceRefresh: true);
                    if (_currentToken != null)
                    {
                        (response, responseJson) = await SendWithRetryAsync(
                            _httpClient,
                            CreateRequest,
                            Name);
                    }
                }

                if (response == null || responseJson == null || !response.IsSuccessStatusCode)
                {
                    if (responseJson != null)
                    {
                        SendMessage("error", $"Response: {responseJson}");
                    }
                    return new LlmResponse { StopReason = "error", Content = [] };
                }

                return ParseOpenAiStyleResponse(responseJson, MessageCallback);
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error calling GitHub Copilot API: {ex.Message}");
                return new LlmResponse { StopReason = "error", Content = [] };
            }
        }

        private async Task<bool> EnsureAuthenticatedAsync()
        {
            if (_currentToken == null || DateTime.UtcNow >= _currentToken.ExpiresAt)
            {
                _currentToken = await _oauthService.GetValidTokenAsync();
            }

            return _currentToken != null;
        }

        protected override bool IsConfigured()
        {
            // For OAuth-based providers, we check if we can get a valid token
            // This is a synchronous check, actual authentication happens in SendMessageAsync
            return !string.IsNullOrWhiteSpace(_model);
        }

        public override async Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!IsConfigured())
            {
                return new LlmStreamingResponse
                {
                    GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(
                        new InvalidOperationException("GitHub Copilot provider is not configured")),
                    Error = "Not configured"
                };
            }

            if (!await EnsureAuthenticatedAsync())
            {
                return new LlmStreamingResponse
                {
                    GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(
                        new InvalidOperationException("Failed to authenticate with GitHub Copilot")),
                    Error = "Authentication failed"
                };
            }

            try
            {
                var payload = new
                {
                    model = _model,
                    messages = BuildOpenAiStyleMessages(messages, systemPrompt),
                    tools = BuildOpenAiStyleTools(tools),
                    max_tokens = 4096,
                    stream = true
                };
                var json = JsonSerializer.Serialize(payload);

                var response = await SendStreamingWithRetryAsync(
                    _httpClient,
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentToken!.AccessToken);
                        return request;
                    },
                    Name);

                if (response == null)
                {
                    return new LlmStreamingResponse
                    {
                        GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(
                            new HttpRequestException("Failed to connect to GitHub Copilot API after retries")),
                        Error = "Connection failed"
                    };
                }

                // Create response object that will be populated during streaming
                var streamingResponse = new LlmStreamingResponse
                {
                    IsComplete = false,
                    GetStreamAsync = null! // Will be set below
                };

                streamingResponse.GetStreamAsync = async () =>
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var sseStream = ParseSseStream(stream);
                    return ParseOpenAiStreamChunksWithToolCapture(sseStream, streamingResponse);
                };

                return streamingResponse;
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Error calling GitHub Copilot streaming API: {ex.Message}");
                return new LlmStreamingResponse
                {
                    GetStreamAsync = () => Task.FromException<IAsyncEnumerable<string>>(ex),
                    Error = ex.Message
                };
            }
        }
    }
}
