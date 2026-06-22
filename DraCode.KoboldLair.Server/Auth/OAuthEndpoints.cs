using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Birko.Security.AspNetCore;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Endpoints.Authorize;
using Birko.Security.OAuth.Server.Endpoints.ClientRegistration;
using Birko.Security.OAuth.Server.Endpoints.DeviceAuthorization;
using Birko.Security.OAuth.Server.Endpoints.Token;
using Birko.Security.OAuth.Server.Stores;
using Microsoft.Extensions.Primitives;
// Birko.Security.AspNetCore also defines a TokenRequest (login DTO); this file means the OAuth one.
using TokenRequest = Birko.Security.OAuth.Server.Endpoints.Token.TokenRequest;

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
        // /token and /device_authorization are unauthenticated by spec (clients authenticate via
        // client credentials / device code in the body, not a bearer token).
        app.MapPost("/token", HandleTokenAsync);
        app.MapPost("/device_authorization", HandleDeviceAuthorizationAsync);

        // /device/approve and /authorize act on behalf of a logged-in human — require auth so the
        // user identity comes from the authenticated principal, not a spoofable body/query field.
        app.MapPost("/device/approve", HandleDeviceApproveAsync).RequireAuthorization();
        app.MapGet("/authorize", HandleAuthorizeAsync).RequireAuthorization();

        if (allowDynamicRegistration)
        {
            // RFC 7591 dynamic client registration — gated behind admin auth (TASK-032).
            app.MapPost("/register", HandleRegisterAsync)
                .RequireAuthorization()
                .RequirePermission(KoboldLairPermissionChecker.ManageUsers);

            // Disable a registered client (TASK-035). Per-client; the token path already rejects
            // disabled clients on the next request. Admin-gated like /register.
            app.MapPost("/register/{clientId}/disable", HandleDisableClientAsync)
                .RequireAuthorization()
                .RequirePermission(KoboldLairPermissionChecker.ManageUsers);
        }
    }

    private static async Task<IResult> HandleTokenAsync(HttpRequest req, OAuthServer server, ServiceAccountTokenIssuer serviceIssuer)
    {
        if (!req.HasFormContentType)
            return Error("invalid_request", "Expected application/x-www-form-urlencoded.");

        var form = await req.ReadFormAsync();
        var (basicId, basicSecret) = TryParseBasicAuth(req);

        var grantType = Val(form["grant_type"]) ?? string.Empty;
        var clientId = Val(form["client_id"]) ?? basicId ?? string.Empty;
        var clientSecret = Val(form["client_secret"]) ?? basicSecret;
        var scope = Val(form["scope"]);

        // Service-account client_credentials tokens are minted by the consumer issuer so they carry
        // sub="service:<name>" + a comma-joined permission scope the auth pipeline enforces (TASK-035 /
        // FEATURE-019 D14, D15). All other grants stay on the framework OAuth server.
        if (grantType == OAuthGrantTypes.ClientCredentials)
        {
            try
            {
                var s = await serviceIssuer.IssueAsync(clientId, clientSecret, scope);
                return Results.Json(new
                {
                    access_token = s.AccessToken,
                    token_type = "Bearer",
                    expires_in = s.ExpiresIn,
                    scope = s.Scope
                }, JsonOpts);
            }
            catch (OAuthServerException ex) { return Error(ex); }
        }

        var request = new TokenRequest
        {
            GrantType = grantType,
            ClientId = clientId,
            ClientSecret = clientSecret,
            Code = Val(form["code"]),
            RedirectUri = Val(form["redirect_uri"]),
            CodeVerifier = Val(form["code_verifier"]),
            RefreshToken = Val(form["refresh_token"]),
            DeviceCode = Val(form["device_code"]),
            Scope = scope
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

    // Disables a registered client (TASK-035). Admin-gated (.RequirePermission above).
    private static async Task<IResult> HandleDisableClientAsync(string clientId, IOAuthClientStore clients)
    {
        var client = await clients.GetByClientIdAsync(clientId);
        if (client == null)
            return Results.NotFound(new { error = "unknown_client" });

        if (client.IsEnabled)
        {
            client.IsEnabled = false;
            await clients.UpdateAsync(client);
        }
        return Results.Json(new { status = "disabled", client_id = clientId }, JsonOpts);
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

    // Requires auth (.RequireAuthorization above). The approving user is taken from the authenticated
    // principal's subject claim — never a request-body field (TASK-032).
    private static async Task<IResult> HandleDeviceApproveAsync(DeviceApproveRequest body, OAuthServer server, HttpContext ctx)
    {
        var userId = ctx.User.FindFirst("sub")?.Value
                     ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await server.DeviceAuthorization.ApproveAsync(body.UserCode, userId, body.Approved);
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

        // Auth is required (.RequireAuthorization above); the user identity comes from the principal
        // only — the legacy ?user_id= fallback is gone (TASK-032). No consent UI yet — returns JSON
        // describing the outcome rather than rendering HTML.
        var userId = req.HttpContext.User.FindFirst("sub")?.Value
                     ?? req.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

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

/// <summary>
/// Body of the <c>/device/approve</c> route. The approving user is derived from the authenticated
/// principal (TASK-032), so no user id is accepted from the body.
/// </summary>
public record DeviceApproveRequest(
    [property: JsonPropertyName("user_code")] string UserCode,
    [property: JsonPropertyName("approved")] bool Approved);
