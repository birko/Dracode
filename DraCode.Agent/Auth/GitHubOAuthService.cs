using System.Text;
using System.Text.Json;

namespace DraCode.Agent.Auth
{
    public class GitHubOAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly TokenStorage _tokenStorage;
        private readonly Action<string, string>? _messageCallback;

        public GitHubOAuthService(string clientId, TokenStorage? tokenStorage = null, Action<string, string>? messageCallback = null)
        {
            _clientId = clientId;
            _httpClient = new HttpClient();
            _tokenStorage = tokenStorage ?? new TokenStorage();
            _messageCallback = messageCallback;
        }

        private void SendMessage(string type, string message)
        {
            _messageCallback?.Invoke(type, message);
        }

        public async Task<TokenInfo?> GetValidTokenAsync(bool forceRefresh = false)
        {
            var token = await _tokenStorage.LoadTokenAsync();
            
            if (token == null || forceRefresh)
            {
                return await AuthenticateAsync();
            }

            // Check if token is expired or about to expire (within 5 minutes)
            if (DateTime.UtcNow.AddMinutes(5) >= token.ExpiresAt)
            {
                // Try to refresh
                var refreshed = await RefreshTokenAsync(token.RefreshToken);
                return refreshed ?? await AuthenticateAsync();
            }

            return token;
        }

        public async Task<TokenInfo?> AuthenticateAsync()
        {
            SendMessage("display", "Initiating GitHub OAuth Device Flow...");

            // Step 1: Request device code
            var deviceCodeResponse = await RequestDeviceCodeAsync();
            if (deviceCodeResponse == null)
            {
                SendMessage("error", "Failed to initiate device flow.");
                return null;
            }

            // Step 2: Display user code and prompt user to authorize
            SendMessage("display", $"\nPlease visit: {deviceCodeResponse.VerificationUri}");
            SendMessage("display", $"And enter code: {deviceCodeResponse.UserCode}\n");
            SendMessage("display", "Waiting for authorization...");

            // Step 3: Poll for token
            var token = await PollForTokenAsync(deviceCodeResponse.DeviceCode, deviceCodeResponse.Interval);
            
            if (token != null)
            {
                await _tokenStorage.SaveTokenAsync(token);
                SendMessage("display", "✓ Authentication successful!");
            }

            return token;
        }

        private async Task<DeviceCodeResponse?> RequestDeviceCodeAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code");
            request.Headers.Add("Accept", "application/json");
            
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("scope", "read:user")
            });
            request.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    SendMessage("error", $"GitHub API Error: {response.StatusCode}");
                    SendMessage("error", $"Response: {json}");
                    return null;
                }
                
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                // Check for error in response
                if (data.TryGetProperty("error", out var error))
                {
                    SendMessage("error", $"GitHub OAuth Error: {error.GetString()}");
                    if (data.TryGetProperty("error_description", out var desc))
                    {
                        SendMessage("error", $"Description: {desc.GetString()}");
                    }
                    return null;
                }

                return new DeviceCodeResponse
                {
                    DeviceCode = data.GetProperty("device_code").GetString() ?? "",
                    UserCode = data.GetProperty("user_code").GetString() ?? "",
                    VerificationUri = data.GetProperty("verification_uri").GetString() ?? "",
                    Interval = data.TryGetProperty("interval", out var interval) ? interval.GetInt32() : 5
                };
            }
            catch (Exception ex)
            {
                SendMessage("error", $"Exception requesting device code: {ex.Message}");
                return null;
            }
        }

        private async Task<TokenInfo?> PollForTokenAsync(string deviceCode, int intervalSeconds)
        {
            var maxAttempts = 120; // 10 minutes max
            var attempts = 0;

            while (attempts < maxAttempts)
            {
                await Task.Delay(intervalSeconds * 1000);
                attempts++;

                var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
                request.Headers.Add("Accept", "application/json");
                
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("device_code", deviceCode),
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                });
                request.Content = content;

                try
                {
                    var response = await _httpClient.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(json);

                    if (data.TryGetProperty("error", out var error))
                    {
                        var errorType = error.GetString();
                        if (errorType == "authorization_pending")
                        {
                            // Don't spam with dots - just continue waiting
                            continue;
                        }
                        else if (errorType == "slow_down")
                        {
                            intervalSeconds += 5;
                            continue;
                        }
                        else
                        {
                            SendMessage("error", $"Error: {errorType}");
                            return null;
                        }
                    }

                    if (data.TryGetProperty("access_token", out var accessToken))
                    {
                        var expiresIn = data.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 28800; // 8 hours default
                        var refreshToken = data.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null;

                        return new TokenInfo
                        {
                            AccessToken = accessToken.GetString() ?? "",
                            RefreshToken = refreshToken ?? "",
                            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
                            TokenType = "Bearer"
                        };
                    }
                }
                catch
                {
                    // Continue polling
                }
            }

            SendMessage("error", "Timeout waiting for authorization.");
            return null;
        }

        private async Task<TokenInfo?> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
            request.Headers.Add("Accept", "application/json");
            
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            });
            request.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.TryGetProperty("access_token", out var accessToken))
                {
                    var expiresIn = data.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 28800;
                    var newRefreshToken = data.TryGetProperty("refresh_token", out var newRefresh) ? newRefresh.GetString() : refreshToken;

                    var token = new TokenInfo
                    {
                        AccessToken = accessToken.GetString() ?? "",
                        RefreshToken = newRefreshToken ?? "",
                        ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
                        TokenType = "Bearer"
                    };

                    await _tokenStorage.SaveTokenAsync(token);
                    return token;
                }
            }
            catch
            {
                // Refresh failed
            }

            return null;
        }

        public void Logout()
        {
            _tokenStorage.DeleteToken();
            SendMessage("display", "Logged out successfully.");
        }

        private class DeviceCodeResponse
        {
            public string DeviceCode { get; set; } = string.Empty;
            public string UserCode { get; set; } = string.Empty;
            public string VerificationUri { get; set; } = string.Empty;
            public int Interval { get; set; }
        }
    }
}
