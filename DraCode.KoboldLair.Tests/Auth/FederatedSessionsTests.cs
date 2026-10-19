using System.IdentityModel.Tokens.Jwt;
using Birko.Security;
using Birko.Security.Jwt;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Stores;
using Birko.Time;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>Builds <see cref="FederatedSessions"/> on the production OAuth store registration, as Program.cs does.</summary>
internal static class FederatedSessionsHarness
{
    public const string Secret = "test-secret-key-at-least-32-characters-long-xyz";

    public sealed class MutableClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
        public DateTimeOffset OffsetUtcNow => new(UtcNow);
        public DateOnly Today => DateOnly.FromDateTime(UtcNow);
    }

    /// <param name="dbPath">A SQLite file, or null for the in-memory stores.</param>
    public static (FederatedSessions sessions, IRefreshTokenStore refreshTokens) Build(
        string? dbPath = null, IDateTimeProvider? clock = null, int refreshLifetimeSeconds = 1209600)
    {
        var services = new ServiceCollection();
        services.AddOAuthServerStores(useSqlite: dbPath != null, dbPath: dbPath);
        var sp = services.BuildServiceProvider();

        var tokenOptions = new TokenOptions { Secret = Secret, Issuer = "KoboldLair", Audience = "KoboldLair" };
        var server = new OAuthServer(
            new OAuthServerSettings
            {
                Issuer = "KoboldLair",
                AccessTokenLifetimeSeconds = 3600,
                RefreshTokenLifetimeSeconds = refreshLifetimeSeconds,
                RotateRefreshTokens = true
            },
            new JwtTokenProvider(tokenOptions), tokenOptions,
            sp.GetRequiredService<IOAuthClientStore>(),
            sp.GetRequiredService<IAuthorizationCodeStore>(),
            sp.GetRequiredService<IRefreshTokenStore>(),
            sp.GetRequiredService<IDeviceCodeStore>(),
            sp.GetRequiredService<IConsentStore>(),
            deviceVerificationUri: "https://localhost/device",
            clock: clock ?? new SystemDateTimeProvider());
        var refreshTokens = sp.GetRequiredService<IRefreshTokenStore>();
        return (new FederatedSessions(server, sp.GetRequiredService<IOAuthClientStore>(), refreshTokens), refreshTokens);
    }
}

/// <summary>
/// TASK-108: GitHub sign-in sessions live on the Birko OAuth server — they survive a restart, rotate on refresh, revoke
/// the family on reuse, expire, and end on logout.
/// </summary>
public class FederatedSessionsTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_sessions_test_{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private static IReadOnlyDictionary<string, string> ClaimsOf(string jwt) =>
        new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims
            .GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.First().Value);

    [Fact]
    public async Task A_refresh_token_issued_before_a_restart_still_renews_after_it()
    {
        var (beforeRestart, _) = FederatedSessionsHarness.Build(_dbPath);
        var login = await beforeRestart.IssueAsync("github:42", "The Octocat", ["user"], "octo@example.com");

        var (afterRestart, _) = FederatedSessionsHarness.Build(_dbPath);
        var renewed = await afterRestart.RefreshAsync(login.RefreshToken);

        renewed.Should().NotBeNull();
        var claims = ClaimsOf(renewed!.Token);
        claims["sub"].Should().Be("github:42");
        claims["name"].Should().Be("The Octocat");
        claims["email"].Should().Be("octo@example.com");
        claims["scope"].Split(' ').Should().BeEquivalentTo(
            KoboldLairPermissionChecker.ExpandRolesToPermissions(["user"]));
        renewed.Roles.Should().Equal("user");
        renewed.Username.Should().Be("The Octocat");
    }

    [Fact]
    public async Task Refresh_rotates_and_a_reused_token_revokes_the_whole_family()
    {
        var (sessions, _) = FederatedSessionsHarness.Build(_dbPath);
        var login = await sessions.IssueAsync("github:42", "octocat", ["user"], null);

        var first = await sessions.RefreshAsync(login.RefreshToken);
        first.Should().NotBeNull();
        first!.RefreshToken.Should().NotBe(login.RefreshToken);

        (await sessions.RefreshAsync(login.RefreshToken)).Should().BeNull("a used refresh token is revoked");
        (await sessions.RefreshAsync(first.RefreshToken)).Should().BeNull("reuse revokes every token of the family");
    }

    [Fact]
    public async Task An_expired_refresh_token_no_longer_renews()
    {
        var clock = new FederatedSessionsHarness.MutableClock();
        var (sessions, _) = FederatedSessionsHarness.Build(_dbPath, clock, refreshLifetimeSeconds: 60);
        var login = await sessions.IssueAsync("github:42", "octocat", ["user"], null);

        clock.UtcNow = clock.UtcNow.AddSeconds(61);

        (await sessions.RefreshAsync(login.RefreshToken)).Should().BeNull();
    }

    [Fact]
    public async Task Logout_ends_the_session_and_leaves_other_devices_signed_in()
    {
        var (sessions, _) = FederatedSessionsHarness.Build(_dbPath);
        var phone = await sessions.IssueAsync("github:42", "octocat", ["user"], null);
        var laptop = await sessions.IssueAsync("github:42", "octocat", ["user"], null);

        await sessions.RevokeAsync(phone.RefreshToken);

        (await sessions.RefreshAsync(laptop.RefreshToken)).Should().NotBeNull();
        // Presenting the logged-out token afterwards reads as reuse, which revokes the user's whole family (Birko).
        (await sessions.RefreshAsync(phone.RefreshToken)).Should().BeNull();
    }

    [Fact]
    public async Task The_local_hash_matches_the_record_the_server_stored()
    {
        // Logout hashes the token itself (Birko has no public single-token revoke); this pins that format.
        var (sessions, refreshTokens) = FederatedSessionsHarness.Build();
        var login = await sessions.IssueAsync("github:42", "octocat", ["user"], null);

        var record = await refreshTokens.GetByHashAsync(FederatedSessions.HashRefreshToken(login.RefreshToken));

        record.Should().NotBeNull();
        record!.UserId.Should().Be("github:42");
        record.ClientId.Should().Be(FederatedSessions.ClientId);
    }

    [Fact]
    public async Task Roles_that_grant_no_permission_are_refused()
    {
        // An empty scope would be widened to everything the client allows.
        var (sessions, _) = FederatedSessionsHarness.Build();

        var act = () => sessions.IssueAsync("github:42", "octocat", ["no-such-role"], null);

        await act.Should().ThrowAsync<FederationDeniedException>();
    }
}
