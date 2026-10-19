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
/// the string <c>sub</c> = <c>github:{id}</c>, FEATURE-019 D9/D11), then starts a session on the Birko OAuth server
/// (<see cref="FederatedSessions"/>, TASK-108) so its refresh token survives a restart.
/// </summary>
public sealed class GitHubFederationService(
    IGitHubUserInfoClient userInfo,
    FederatedSessions sessions,
    IOptions<GitHubFederationConfiguration> gitHubConfig,
    IUserRepository? userRepository = null)
{
    private readonly GitHubFederationConfiguration _gitHub = gitHubConfig.Value;

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

        // The session's scope carries the expanded role permissions, which ClaimsCurrentUser.Permissions +
        // PermissionEndpointFilter enforce per endpoint.
        return await sessions.IssueAsync(sub, gh.Name ?? gh.Login, _gitHub.DefaultRoles, gh.Email, ct).ConfigureAwait(false);
    }
}
