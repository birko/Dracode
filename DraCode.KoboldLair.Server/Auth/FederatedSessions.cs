using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Endpoints.Token;
using Birko.Security.OAuth.Server.Models;
using Birko.Security.OAuth.Server.Stores;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Sign-in sessions for people who authenticated elsewhere (GitHub, TASK-033), kept by the Birko OAuth server
/// (TASK-108): its refresh tokens are hashed, rotated, reuse-checked and persisted with the OAuth stores, so a session
/// survives a server restart. Sessions belong to the public client <see cref="ClientId"/>, created on first use.
/// </summary>
public sealed class FederatedSessions(OAuthServer server, IOAuthClientStore clients, IRefreshTokenStore refreshTokens)
{
    /// <summary>The OAuth client federated sessions are issued under. Only the <c>refresh_token</c> grant.</summary>
    public const string ClientId = "koboldlair-sign-in";

    private static readonly IReadOnlyList<string> AllPermissions =
    [
        KoboldLairPermissionChecker.ManageProjects,
        KoboldLairPermissionChecker.ExecuteAgents,
        KoboldLairPermissionChecker.ViewAll,
        KoboldLairPermissionChecker.ViewOwn,
        KoboldLairPermissionChecker.ManageUsers,
        KoboldLairPermissionChecker.ManageConfig
    ];

    private readonly SemaphoreSlim _clientLock = new(1, 1);

    /// <summary>Starts a session for <paramref name="sub"/> with the permissions its roles grant.</summary>
    /// <exception cref="FederationDeniedException">The roles grant no permission.</exception>
    public async Task<LoginResponse> IssueAsync(
        string sub, string displayName, IReadOnlyList<string> roles, string? email, CancellationToken ct = default)
    {
        var permissions = KoboldLairPermissionChecker.ExpandRolesToPermissions(roles);
        // An empty scope would be widened to everything the client allows, so a role set with no permission is refused.
        if (permissions.Count == 0)
            throw new FederationDeniedException($"The roles [{string.Join(", ", roles)}] grant no permission.");

        await EnsureClientAsync(ct).ConfigureAwait(false);

        var claims = new Dictionary<string, string>
        {
            ["name"] = displayName,
            ["roles"] = string.Join(",", roles)
        };
        if (!string.IsNullOrEmpty(email))
            claims["email"] = email;

        var response = await server.Token
            .IssueForSubjectAsync(ClientId, sub, string.Join(' ', permissions), claims, ct)
            .ConfigureAwait(false);
        return ToLoginResponse(response);
    }

    /// <summary>Renews a session through the server's <c>refresh_token</c> grant; null when the token is not valid.</summary>
    public async Task<LoginResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var response = await server.Token.HandleAsync(new TokenRequest
            {
                GrantType = OAuthGrantTypes.RefreshToken,
                ClientId = ClientId,
                RefreshToken = refreshToken
            }, ct).ConfigureAwait(false);
            return ToLoginResponse(response);
        }
        catch (OAuthServerException)
        {
            return null;
        }
    }

    /// <summary>Ends the session that <paramref name="refreshToken"/> belongs to. Unknown tokens are ignored.</summary>
    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return;
        var record = await refreshTokens.GetByHashAsync(HashRefreshToken(refreshToken), ct).ConfigureAwait(false);
        if (record is { Revoked: false } && record.ClientId == ClientId)
            await refreshTokens.TryRevokeAsync(record, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The server's stored form of a refresh token: hex SHA-256 (Birko's internal ClientSecretHasher). Birko has no
    /// public single-token revoke yet; <c>FederatedSessionsTests</c> fails if the format changes.
    /// </summary>
    public static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private async Task EnsureClientAsync(CancellationToken ct)
    {
        await _clientLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var client = await clients.GetByClientIdAsync(ClientId, ct).ConfigureAwait(false);
            if (client == null)
            {
                await clients.CreateAsync(new OAuthClient
                {
                    ClientId = ClientId,
                    ClientType = OAuthClientType.Public,
                    Name = "KoboldLair sign-in sessions",
                    AllowedGrantTypes = [OAuthGrantTypes.RefreshToken],
                    AllowedScopes = [.. AllPermissions]
                }, ct: ct).ConfigureAwait(false);
            }
            else if (!client.AllowedScopes.ToHashSet().SetEquals(AllPermissions))
            {
                client.AllowedScopes = [.. AllPermissions];
                await clients.UpdateAsync(client, ct: ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private static LoginResponse ToLoginResponse(TokenResponse response)
    {
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken).Claims.ToList();
        var roles = claims.FirstOrDefault(c => c.Type == "roles")?.Value;
        return new LoginResponse
        {
            Token = response.AccessToken,
            RefreshToken = response.RefreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(response.ExpiresIn),
            Roles = string.IsNullOrEmpty(roles) ? [] : [.. roles.Split(',')],
            Username = claims.FirstOrDefault(c => c.Type == "name")?.Value ?? string.Empty
        };
    }
}
