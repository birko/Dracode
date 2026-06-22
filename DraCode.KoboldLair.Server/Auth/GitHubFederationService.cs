using System.IdentityModel.Tokens.Jwt;
using Birko.Security;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Users;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Thrown when a GitHub identity is authenticated but not on the admission allowlist
/// (FEATURE-019 D13). The endpoint maps this to <c>403 Forbidden</c> — no token, no user row.
/// </summary>
public sealed class FederationDeniedException(string message) : Exception(message);

/// <summary>
/// Turns an authenticated GitHub identity into a DraCode-issued token (TASK-033). Resolves the
/// GitHub user, enforces the numeric-id allowlist, upserts the DraCode <see cref="User"/> (keyed on
/// the string <c>sub</c> = <c>github:{id}</c>, FEATURE-019 D9/D11), then mints a JWT + refresh token
/// exactly like <see cref="AuthEndpoints.HandleLogin"/>.
/// </summary>
public sealed class GitHubFederationService(
    IGitHubUserInfoClient userInfo,
    ITokenProvider tokenProvider,
    RefreshTokenStore refreshStore,
    IOptions<GitHubFederationConfiguration> gitHubConfig,
    IOptions<JwtAuthenticationConfiguration> jwtConfig,
    IUserRepository? userRepository = null)
{
    private readonly GitHubFederationConfiguration _gitHub = gitHubConfig.Value;
    private readonly JwtAuthenticationConfiguration _jwt = jwtConfig.Value;

    /// <summary>
    /// Federates a GitHub access token into a DraCode <see cref="LoginResponse"/>.
    /// </summary>
    /// <exception cref="FederationDeniedException">The GitHub id is not on the allowlist.</exception>
    public async Task<LoginResponse> FederateAsync(string gitHubAccessToken, CancellationToken ct = default)
    {
        var gh = await userInfo.GetUserAsync(gitHubAccessToken, ct).ConfigureAwait(false);

        // Admission control (D13): GitHub OAuth proves *who*, not *whether allowed*. Deny-all default.
        if (!_gitHub.AllowedGitHubIds.Contains(gh.Id))
            throw new FederationDeniedException(
                $"GitHub user {gh.Login} (id {gh.Id}) is not permitted to sign in.");

        var sub = $"github:{gh.Id}";

        // Created-on-first-login, reused-after (AC #3). Null off-SQLite — ownership still keys on `sub`.
        if (userRepository != null)
        {
            var existing = await userRepository.GetBySubAsync(sub).ConfigureAwait(false);
            await userRepository.UpsertAsync(new User
            {
                Sub = sub,
                DisplayName = gh.Name ?? gh.Login,
                Email = gh.Email,
                CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow
            }).ConfigureAwait(false);
        }

        // Mint exactly like HandleLogin: scope carries the expanded role permissions (comma-joined),
        // so ClaimsCurrentUser.Permissions + PermissionEndpointFilter enforce per-endpoint scopes.
        var roles = _gitHub.DefaultRoles;
        var permissions = KoboldLairPermissionChecker.ExpandRolesToPermissions(roles);
        var displayName = gh.Name ?? gh.Login;
        var claims = new Dictionary<string, string>
        {
            [JwtRegisteredClaimNames.Sub] = sub,
            ["name"] = displayName,
            ["scope"] = string.Join(",", permissions),
            ["roles"] = string.Join(",", roles)
        };
        if (!string.IsNullOrEmpty(gh.Email))
            claims["email"] = gh.Email;

        var tokenResult = tokenProvider.GenerateToken(claims);
        var refreshToken = tokenProvider.GenerateRefreshToken();

        var refreshExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshExpirationDays);
        refreshStore.Store(refreshToken, sub, displayName, roles, refreshExpiry);

        return new LoginResponse
        {
            Token = tokenResult.Token,
            RefreshToken = refreshToken,
            ExpiresAt = tokenResult.ExpiresAt,
            Roles = roles,
            Username = displayName
        };
    }
}
