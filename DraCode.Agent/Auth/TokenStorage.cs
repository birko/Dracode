using System.Text.Json;

namespace DraCode.Agent.Auth
{
    public class TokenInfo
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }

    public class TokenStorage
    {
        private readonly string _tokenFilePath;

        public TokenStorage(string? customPath = null)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var tokenDir = Path.Combine(appDataPath, ".dracode");
            Directory.CreateDirectory(tokenDir);
            _tokenFilePath = customPath ?? Path.Combine(tokenDir, "github_token.json");
        }

        public async Task SaveTokenAsync(TokenInfo token)
        {
            var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_tokenFilePath, json);
        }

        public async Task<TokenInfo?> LoadTokenAsync()
        {
            if (!File.Exists(_tokenFilePath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(_tokenFilePath);
                return JsonSerializer.Deserialize<TokenInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        public void DeleteToken()
        {
            if (File.Exists(_tokenFilePath))
                File.Delete(_tokenFilePath);
        }

        public bool HasToken() => File.Exists(_tokenFilePath);
    }
}
