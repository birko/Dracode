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
        private readonly GitHubOAuthService _oauthService;
        private TokenInfo? _currentToken;

        public override string Name => "GitHub Copilot";

        public GitHubCopilotProvider(
            string clientId, 
            string model = "gpt-4o", 
            string baseUrl = "https://api.githubcopilot.com/chat/completions")
        {
            _model = model;
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _oauthService = new GitHubOAuthService(clientId);
        }

        public override async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt)
        {
            if (!await EnsureAuthenticatedAsync())
            {
                return NotConfigured();
            }

            var payload = new
            {
                model = _model,
                messages = BuildOpenAiStyleMessages(messages, systemPrompt),
                tools = BuildOpenAiStyleTools(tools)
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
            request.Headers.Add("Authorization", $"Bearer {_currentToken!.AccessToken}");
            request.Headers.Add("Editor-Version", "vscode/1.96.0");
            request.Headers.Add("Editor-Plugin-Version", "copilot-chat/0.23.2");
            request.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Token expired, try to refresh and retry
                    _currentToken = await _oauthService.GetValidTokenAsync(forceRefresh: true);
                    if (_currentToken != null)
                    {
                        request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
                        request.Headers.Add("Authorization", $"Bearer {_currentToken.AccessToken}");
                        request.Headers.Add("Editor-Version", "vscode/1.96.0");
                        request.Headers.Add("Editor-Plugin-Version", "copilot-chat/0.23.2");
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        response = await _httpClient.SendAsync(request);
                    }
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                return ParseOpenAiStyleResponse(responseJson);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error calling GitHub Copilot API: {ex.Message}");
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
    }
}
