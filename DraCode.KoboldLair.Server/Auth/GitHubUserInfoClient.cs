using System.Net.Http.Headers;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// The slice of a GitHub user we consume during federation. <see cref="Id"/> is GitHub's stable
/// numeric account id — the federation subject key (usernames are mutable; ids are not).
/// </summary>
public sealed record GitHubUserInfo(long Id, string Login, string? Name, string? Email);

/// <summary>
/// Reads the authenticated GitHub user (<c>GET https://api.github.com/user</c>). This is the
/// mockable seam for federation tests — fakes implement it directly so no real HTTP is needed
/// (TASK-033, AC #5).
/// </summary>
public interface IGitHubUserInfoClient
{
    Task<GitHubUserInfo> GetUserAsync(string accessToken, CancellationToken ct = default);
}

/// <summary>
/// <see cref="HttpClient"/>-backed <see cref="IGitHubUserInfoClient"/>. Registered via
/// <c>AddHttpClient</c> so the client is pooled; GitHub rejects requests without a User-Agent.
/// </summary>
public sealed class GitHubUserInfoClient : IGitHubUserInfoClient
{
    private const string UserEndpoint = "https://api.github.com/user";
    private readonly HttpClient _http;

    public GitHubUserInfoClient(HttpClient http)
    {
        _http = http;
        // GitHub 403s requests without a User-Agent and expects the versioned media type.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("DraCode-KoboldLair");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<GitHubUserInfo> GetUserAsync(string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"GitHub user lookup failed ({(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var id = root.GetProperty("id").GetInt64();
        var login = root.TryGetProperty("login", out var l) ? l.GetString() ?? "" : "";
        var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var email = root.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

        return new GitHubUserInfo(id, login, name, email);
    }
}
