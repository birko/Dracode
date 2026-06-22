using Birko.Communication.OAuth;
using Birko.Communication.OAuth.Providers;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Minimal-API surface for GitHub federation (TASK-033). <c>/auth/github/login</c> redirects the
/// browser to GitHub; <c>/auth/github/callback</c> validates the CSRF <c>state</c>, exchanges the
/// code, federates to a DraCode token, and hands it to the SPA via the URL fragment. Mapped only
/// when <c>Authentication:GitHub:Enabled</c> is true (and JWT is enabled — see Program.cs).
/// </summary>
public static class GitHubAuthEndpoints
{
    /// <summary>
    /// GitHub's authorization-code endpoint. Kept local rather than added to the framework's
    /// device-flow-only <see cref="GitHubOAuthProvider"/> (FEATURE-019: local for this task;
    /// follow-up to upstream a web-flow helper).
    /// </summary>
    public const string AuthorizationEndpoint = "https://github.com/login/oauth/authorize";

    public static void MapGitHubAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/github/login", HandleLogin);
        app.MapGet("/auth/github/callback", HandleCallback);
    }

    private static IResult HandleLogin(
        IOptions<GitHubFederationConfiguration> config,
        OAuthStateStore stateStore)
    {
        var cfg = config.Value;
        var state = stateStore.Issue();

        var settings = new OAuthSettings
        {
            GrantType = OAuthGrantType.AuthorizationCode,
            ClientId = cfg.ResolveClientId(),
            AuthorizationEndpoint = AuthorizationEndpoint,
            TokenEndpoint = GitHubOAuthProvider.TokenEndpoint,
            RedirectUri = cfg.RedirectUri,
            Scope = cfg.Scope
        };

        var authUrl = new OAuthClient(settings).BuildAuthorizationUrl(state);
        return Results.Redirect(authUrl);
    }

    private static async Task<IResult> HandleCallback(
        HttpContext ctx,
        IOptions<GitHubFederationConfiguration> config,
        OAuthStateStore stateStore,
        IGitHubTokenExchanger exchanger,
        GitHubFederationService federation)
    {
        var query = ctx.Request.Query;
        var code = query["code"].ToString();
        var state = query["state"].ToString();

        // CSRF guard: state must be one we issued, unexpired, single-use. Never reflect it back.
        if (!stateStore.TryConsume(state))
            return Results.BadRequest(new { error = "invalid_state" });

        if (string.IsNullOrEmpty(code))
            return Results.BadRequest(new { error = "missing_code" });

        string gitHubToken;
        try
        {
            gitHubToken = await exchanger.ExchangeAsync(code, ctx.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = "code_exchange_failed", detail = ex.Message });
        }

        LoginResponse login;
        try
        {
            login = await federation.FederateAsync(gitHubToken, ctx.RequestAborted).ConfigureAwait(false);
        }
        catch (FederationDeniedException ex)
        {
            return Results.Json(new { error = "forbidden", detail = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }

        // Hand the tokens to the SPA via the URL fragment — it never reaches the server/logs; the
        // SPA reads location.hash and stores it as authToken (TASK-056). PostLoginRedirectUri is a
        // fixed config value, never request-reflected (open-redirect guard).
        var redirect = $"{config.Value.PostLoginRedirectUri}#access_token={Uri.EscapeDataString(login.Token)}" +
                       $"&refresh_token={Uri.EscapeDataString(login.RefreshToken)}" +
                       $"&expires_at={Uri.EscapeDataString(login.ExpiresAt.ToString("o"))}";
        return Results.Redirect(redirect);
    }
}
