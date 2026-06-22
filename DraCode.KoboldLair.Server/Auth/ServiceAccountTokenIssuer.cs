using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Birko.Security;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Stores;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Result of a service-account <c>client_credentials</c> token issuance — the snake_case fields the
/// <c>/token</c> endpoint serializes.
/// </summary>
public sealed record ServiceTokenResult(string AccessToken, int ExpiresIn, string Scope);

/// <summary>
/// Issues DraCode JWTs for service accounts (non-human <c>client_credentials</c> callers, TASK-035 /
/// FEATURE-019 D14, D15). The stock OAuth token endpoint stamps <c>sub = client.ClientId</c> and
/// space-joins scopes, which the comma-splitting <see cref="Birko.Security.AspNetCore"/> permission
/// pipeline can't enforce. This consumer-side issuer instead stamps <c>sub = "service:&lt;name&gt;"</c>
/// and a comma-joined permission scope via the same <see cref="ITokenProvider"/> the human paths use,
/// leaving the framework OAuth server generic.
/// </summary>
public sealed class ServiceAccountTokenIssuer(IOAuthClientStore clients, ITokenProvider tokenProvider)
{
    /// <summary>
    /// Authenticates the confidential client, narrows the requested scopes against the client's
    /// allowed set, and mints a DraCode JWT. Throws <see cref="OAuthServerException"/> (mapped to the
    /// RFC error JSON by the endpoint) for unknown/disabled clients, bad secrets, the wrong grant, or
    /// disallowed scopes.
    /// </summary>
    public async Task<ServiceTokenResult> IssueAsync(
        string? clientId, string? clientSecret, string? requestedScope, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(clientId))
            throw new OAuthServerException(OAuthErrorCodes.InvalidClient, "client_id is required.");

        var client = await clients.GetByClientIdAsync(clientId, ct).ConfigureAwait(false);
        if (client == null || !client.IsEnabled)
            throw new OAuthServerException(OAuthErrorCodes.InvalidClient, "Unknown or disabled client.");

        // Confidential clients must present a valid secret. Mirrors the framework's internal
        // ClientSecretHasher (hex SHA-256) — re-implemented here because that type is internal.
        if (string.IsNullOrEmpty(clientSecret) ||
            string.IsNullOrEmpty(client.ClientSecretHash) ||
            !VerifySecret(clientSecret, client.ClientSecretHash))
        {
            throw new OAuthServerException(OAuthErrorCodes.InvalidClient, "Invalid client credentials.");
        }

        if (!client.AllowedGrantTypes.Contains(OAuthGrantTypes.ClientCredentials))
            throw new OAuthServerException(OAuthErrorCodes.UnauthorizedClient,
                "Client is not authorized to use grant type 'client_credentials'.");

        var granted = NarrowScopes(requestedScope, client.AllowedScopes);
        var scopeClaim = string.Join(",", granted); // comma-joined so ClaimsCurrentUser enforces it (D15)
        var serviceName = string.IsNullOrEmpty(client.Name) ? client.ClientId : client.Name;

        var claims = new Dictionary<string, string>
        {
            [JwtRegisteredClaimNames.Sub] = $"service:{serviceName}",
            ["name"] = serviceName,
            ["scope"] = scopeClaim
        };

        var token = tokenProvider.GenerateToken(claims);
        var expiresIn = Math.Max(0, (int)(token.ExpiresAt - DateTime.UtcNow).TotalSeconds);
        return new ServiceTokenResult(token.Token, expiresIn, scopeClaim);
    }

    /// <summary>
    /// Intersects the space-separated requested scopes (OAuth wire format) with the client's allowed
    /// set; an empty request grants all allowed. Throws <c>invalid_scope</c> if a non-empty request
    /// shares nothing with the allowed set. Mirrors the framework's <c>NarrowScope</c>.
    /// </summary>
    private static List<string> NarrowScopes(string? requested, IEnumerable<string> allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(requested))
            return allowedSet.ToList();

        var granted = requested.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(allowedSet.Contains).ToList();
        if (granted.Count == 0 && allowedSet.Count > 0)
            throw new OAuthServerException(OAuthErrorCodes.InvalidScope,
                "None of the requested scopes are allowed for this client.");
        return granted;
    }

    // Hex SHA-256, fixed-time compare — matches Birko.Security.OAuth.Server.Internal.ClientSecretHasher.
    private static bool VerifySecret(string secret, string storedHash)
    {
        var presented = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(presented), Encoding.ASCII.GetBytes(storedHash));
    }
}
