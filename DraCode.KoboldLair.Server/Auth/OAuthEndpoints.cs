using System.Text.Json;
using System.Text.Json.Serialization;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Endpoints.Authorize;
using Birko.Security.OAuth.Server.Endpoints.ClientRegistration;
using Birko.Security.OAuth.Server.Endpoints.DeviceAuthorization;
using Birko.Security.OAuth.Server.Endpoints.Token;
using Microsoft.Extensions.Primitives;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Minimal-API surface for the Birko OAuth 2.1 authorization server. The handlers in
/// <see cref="OAuthServer"/> are ASP.NET-agnostic; this maps HTTP onto them and emits the
/// RFC-compliant snake_case JSON. Mapped only when <c>Authentication:OAuth:Enabled</c> is true.
/// </summary>
public static class OAuthEndpoints
{
    // RFC wire format is snake_case; minimal-API default would camelCase. We build explicit
    // snake_case payloads, ignore nulls, and disable any naming policy to be safe.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    public static void MapOAuthEndpoints(this WebApplication app, bool allowDynamicRegistration)
    {
        app.MapPost("/token", HandleTokenAsync);
        app.MapPost("/device_authorization", HandleDeviceAuthorizationAsync);
        app.MapPost("/device/approve", HandleDeviceApproveAsync);
        app.MapGet("/authorize", HandleAuthorizeAsync);

        if (allowDynamicRegistration)
        {
            // ⚠ TODO(TASK-032): gate behind admin auth. Only mapped when AllowDynamicRegistration=true.
            app.MapPost("/register", HandleRegisterAsync);
        }
    }

    private static async Task<IResult> HandleTokenAsync(HttpRequest req, OAuthServer server)
    {
        if (!req.HasFormContentType)
            return Error("invalid_request", "Expected application/x-www-form-urlencoded.");

        var form = await req.ReadFormAsync();
        var (basicId, basicSecret) = TryParseBasicAuth(req);

        var request = new TokenRequest
        {
            GrantType = Val(form["grant_type"]) ?? string.Empty,
            ClientId = Val(form["client_id"]) ?? basicId ?? string.Empty,
            ClientSecret = Val(form["client_secret"]) ?? basicSecret,
            Code = Val(form["code"]),
            RedirectUri = Val(form["redirect_uri"]),
            CodeVerifier = Val(form["code_verifier"]),
            RefreshToken = Val(form["refresh_token"]),
            DeviceCode = Val(form["device_code"]),
            Scope = Val(form["scope"])
        };

        try
        {
            var r = await server.Token.HandleAsync(request);
            return Results.Json(new
            {
                access_token = r.AccessToken,
                token_type = r.TokenType,
                expires_in = r.ExpiresIn,
                refresh_token = r.RefreshToken,
                scope = r.Scope
            }, JsonOpts);
        }
        catch (OAuthServerException ex) { return Error(ex); }
    }

    private static async Task<IResult> HandleDeviceAuthorizationAsync(HttpRequest req, OAuthServer server)
    {
        if (!req.HasFormContentType)
            return Error("invalid_request", "Expected application/x-www-form-urlencoded.");

        var form = await req.ReadFormAsync();
        var request = new DeviceAuthorizationRequest
        {
            ClientId = Val(form["client_id"]) ?? string.Empty,
            Scope = Val(form["scope"])
        };

        try
        {
            var r = await server.DeviceAuthorization.HandleAsync(request);
            return Results.Json(new
            {
                device_code = r.DeviceCode,
                user_code = r.UserCode,
                verification_uri = r.VerificationUri,
                verification_uri_complete = r.VerificationUriComplete,
                expires_in = r.ExpiresIn,
                interval = r.Interval
            }, JsonOpts);
        }
        catch (OAuthServerException ex) { return Error(ex); }
    }

    // ⚠ TODO(TASK-032): require auth + derive userId from the authenticated principal instead of
    // the body field, and drop UserId from DeviceApproveRequest. Unguarded for now (OAuth is
    // Enabled:false by default; local-dev only). See TASK-030 resolved note (2).
    private static async Task<IResult> HandleDeviceApproveAsync(DeviceApproveRequest body, OAuthServer server)
    {
        try
        {
            await server.DeviceAuthorization.ApproveAsync(body.UserCode, body.UserId, body.Approved);
            return Results.Json(new { status = "ok" }, JsonOpts);
        }
        catch (OAuthServerException ex) { return Error(ex); }
    }

    private static async Task<IResult> HandleAuthorizeAsync(HttpRequest req, OAuthServer server)
    {
        var q = req.Query;
        var request = new AuthorizeRequest
        {
            ResponseType = Val(q["response_type"]) ?? "code",
            ClientId = Val(q["client_id"]) ?? string.Empty,
            RedirectUri = Val(q["redirect_uri"]) ?? string.Empty,
            Scope = Val(q["scope"]),
            State = Val(q["state"]),
            CodeChallenge = Val(q["code_challenge"]),
            CodeChallengeMethod = Val(q["code_challenge_method"])
        };

        // ⚠ TODO(TASK-032): once JWT middleware is enabled, derive userId from the principal only.
        // No consent UI yet — returns JSON describing the outcome rather than rendering HTML.
        var userId = req.HttpContext.User?.FindFirst("sub")?.Value ?? Val(q["user_id"]) ?? string.Empty;

        try
        {
            var r = await server.Authorize.HandleAuthorizeAsync(request, userId);
            return Results.Json(new
            {
                requires_consent = r.RequiresConsent,
                requested_scope = r.RequestedScope,
                code = r.Code,
                redirect_uri = r.RedirectUri,
                state = r.State
            }, JsonOpts);
        }
        catch (OAuthServerException ex) { return Error(ex); }
    }

    private static async Task<IResult> HandleRegisterAsync(ClientRegistrationRequest body, OAuthServer server)
    {
        try
        {
            var r = await server.ClientRegistration.RegisterAsync(body);
            return Results.Json(new
            {
                client_id = r.ClientId,
                client_secret = r.ClientSecret,
                client_type = r.ClientType.ToString(),
                name = r.Name,
                redirect_uris = r.RedirectUris,
                allowed_grant_types = r.AllowedGrantTypes,
                allowed_scopes = r.AllowedScopes,
                created_at = r.CreatedAt
            }, JsonOpts, statusCode: 201);
        }
        catch (OAuthServerException ex) { return Error(ex); }
    }

    private static IResult Error(OAuthServerException ex) =>
        Results.Json(new { error = ex.ErrorCode, error_description = ex.ErrorDescription, error_uri = ex.ErrorUri }, JsonOpts, statusCode: 400);

    private static IResult Error(string code, string description) =>
        Results.Json(new { error = code, error_description = description }, JsonOpts, statusCode: 400);

    private static string? Val(StringValues sv) => StringValues.IsNullOrEmpty(sv) ? null : sv.ToString();

    private static (string? clientId, string? secret) TryParseBasicAuth(HttpRequest req)
    {
        var header = req.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return (null, null);
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var sep = decoded.IndexOf(':');
            if (sep < 0) return (null, null);
            return (Uri.UnescapeDataString(decoded[..sep]), Uri.UnescapeDataString(decoded[(sep + 1)..]));
        }
        catch { return (null, null); }
    }
}

/// <summary>Body of the unguarded <c>/device/approve</c> route (see TASK-032 TODO).</summary>
public record DeviceApproveRequest(
    [property: JsonPropertyName("user_code")] string UserCode,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("approved")] bool Approved);
