using System.Net.Http.Headers;
using Birko.Communication.OAuth;
using Birko.Communication.OAuth.Providers;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Exchanges a GitHub authorization <c>code</c> for a GitHub access token. Wraps the concrete
/// <see cref="OAuthClient"/> (which isn't DI-friendly) behind an interface so federation endpoint
/// tests can mock the token exchange and stay off github.com (TASK-033, AC #5).
/// </summary>
public interface IGitHubTokenExchanger
{
    /// <summary>Exchanges the callback <paramref name="code"/> for a GitHub access token.</summary>
    Task<string> ExchangeAsync(string code, CancellationToken ct = default);
}

/// <summary>
/// <see cref="OAuthClient"/>-backed exchanger. Builds the client with an <see cref="HttpClient"/>
/// whose <c>Accept: application/json</c> is set — GitHub's token endpoint returns form-encoded by
/// default, which <see cref="OAuthClient"/> can't parse (it expects JSON).
/// </summary>
public sealed class GitHubTokenExchanger : IGitHubTokenExchanger
{
    private readonly HttpClient _http;
    private readonly GitHubFederationConfiguration _config;

    public GitHubTokenExchanger(HttpClient http, IOptions<GitHubFederationConfiguration> config)
    {
        _http = http;
        _config = config.Value;
        // GitHub returns x-www-form-urlencoded unless we ask for JSON; OAuthClient parses JSON.
        if (!_http.DefaultRequestHeaders.Accept.Any())
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("DraCode-KoboldLair");
    }

    public async Task<string> ExchangeAsync(string code, CancellationToken ct = default)
    {
        var settings = new OAuthSettings
        {
            GrantType = OAuthGrantType.AuthorizationCode,
            ClientId = _config.ResolveClientId(),
            ClientSecret = _config.ResolveClientSecret(),
            AuthorizationEndpoint = GitHubAuthEndpoints.AuthorizationEndpoint,
            TokenEndpoint = GitHubOAuthProvider.TokenEndpoint,
            RedirectUri = _config.RedirectUri,
            Scope = _config.Scope
        };

        var client = new OAuthClient(settings, _http);
        var token = await client.ExchangeCodeAsync(code, ct: ct).ConfigureAwait(false);
        return token.AccessToken;
    }
}
