using Birko.Configuration;
using Birko.Security;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Endpoints.ClientRegistration;
using Birko.Security.OAuth.Server.Endpoints.DeviceAuthorization;
using Birko.Security.OAuth.Server.Endpoints.Token;
using Birko.Security.OAuth.Server.Models;
using Birko.Security.OAuth.Server.Stores;
using Birko.Time;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>
/// TASK-031: exercises the OAuth grant flows against the SQLite-backed stores (not in-memory),
/// over a temp db file, and verifies records survive a fresh store instance pointed at the same file.
/// </summary>
public class SqliteOAuthStoreTests : IAsyncLifetime, IDisposable
{
    private string _dbPath = null!;

    private sealed class MutableClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTimeOffset OffsetUtcNow => new(UtcNow);
        public DateOnly Today => DateOnly.FromDateTime(UtcNow);
        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }

    private sealed class FakeTokenProvider : ITokenProvider
    {
        public TokenResult GenerateToken(IDictionary<string, string> claims, TokenOptions? options = null)
            => new() { Token = "test-access-token-" + Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddHours(1) };
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N");
        public TokenValidationResult ValidateToken(string token, TokenOptions? options = null)
            => TokenValidationResult.Success(new Dictionary<string, string>());
    }

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_oauth_test_{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    public void Dispose() { }

    /// <summary>Builds an OAuthServer wired to the SQLite stores registered via the production DI extension.</summary>
    private (OAuthServer server, MutableClock clock, IServiceProvider sp) BuildServer()
    {
        var services = new ServiceCollection();
        services.AddOAuthServerStores(useSqlite: true, dbPath: _dbPath);
        var sp = services.BuildServiceProvider();

        var clock = new MutableClock();
        var settings = new OAuthServerSettings
        {
            Issuer = "KoboldLair",
            AccessTokenLifetimeSeconds = 3600,
            RefreshTokenLifetimeSeconds = 1209600,
            AuthorizationCodeLifetimeSeconds = 60,
            DeviceCodeLifetimeSeconds = 600,
            DeviceCodePollingIntervalSeconds = 5,
            RotateRefreshTokens = true,
            RequirePkceForPublicClients = true
        };
        var tokenOptions = new TokenOptions { Secret = "x".PadRight(40, 'x'), Issuer = "KoboldLair", Audience = "KoboldLair" };
        var server = new OAuthServer(settings, new FakeTokenProvider(), tokenOptions,
            sp.GetRequiredService<IOAuthClientStore>(),
            sp.GetRequiredService<IAuthorizationCodeStore>(),
            sp.GetRequiredService<IRefreshTokenStore>(),
            sp.GetRequiredService<IDeviceCodeStore>(),
            sp.GetRequiredService<IConsentStore>(),
            deviceVerificationUri: "https://localhost/device", clock: clock);
        return (server, clock, sp);
    }

    [Fact]
    public async Task ClientCredentials_AgainstSqlite_IssuesAccessToken()
    {
        var (server, _, _) = BuildServer();

        var client = await server.ClientRegistration.RegisterAsync(new ClientRegistrationRequest
        {
            Name = "svc",
            ClientType = OAuthClientType.Confidential,
            AllowedGrantTypes = new List<string> { OAuthGrantTypes.ClientCredentials },
            AllowedScopes = new List<string> { "read", "write" }
        });

        var resp = await server.Token.HandleAsync(new TokenRequest
        {
            GrantType = OAuthGrantTypes.ClientCredentials,
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret
        });

        resp.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeviceCode_AgainstSqlite_PendingThenApproved_IssuesToken()
    {
        var (server, clock, _) = BuildServer();

        var client = await server.ClientRegistration.RegisterAsync(new ClientRegistrationRequest
        {
            Name = "device-app",
            ClientType = OAuthClientType.Public,
            AllowedGrantTypes = new List<string> { OAuthGrantTypes.DeviceCode },
            AllowedScopes = new List<string> { "read" }
        });

        var device = await server.DeviceAuthorization.HandleAsync(new DeviceAuthorizationRequest { ClientId = client.ClientId });

        // poll while pending — exercises GetByDeviceCodeAsync against the SQLite device-code store
        var pending = () => server.Token.HandleAsync(new TokenRequest
        {
            GrantType = OAuthGrantTypes.DeviceCode,
            ClientId = client.ClientId,
            DeviceCode = device.DeviceCode
        });
        (await pending.Should().ThrowAsync<OAuthServerException>())
            .Which.ErrorCode.Should().Be(OAuthErrorCodes.AuthorizationPending);

        // approve (GetByUserCodeAsync + UpdateAsync), then poll again after the interval
        await server.DeviceAuthorization.ApproveAsync(device.UserCode, "user-1", approved: true);
        clock.Advance(TimeSpan.FromSeconds(6));

        var resp = await server.Token.HandleAsync(new TokenRequest
        {
            GrantType = OAuthGrantTypes.DeviceCode,
            ClientId = client.ClientId,
            DeviceCode = device.DeviceCode
        });
        resp.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisteredClient_WithCollections_SurvivesFreshStoreInstance()
    {
        var (server, _, _) = BuildServer();

        var registered = await server.ClientRegistration.RegisterAsync(new ClientRegistrationRequest
        {
            Name = "web-app",
            ClientType = OAuthClientType.Confidential,
            RedirectUris = new List<string> { "https://app/cb", "https://app/cb2" },
            AllowedGrantTypes = new List<string> { OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken },
            AllowedScopes = new List<string> { "read", "write" }
        });

        // Fresh store instance pointed at the same db file — proves persistence + JSON round-trip.
        var dir = Path.GetDirectoryName(_dbPath)!;
        var freshStore = new SqlOAuthClientStore(new PasswordSettings(dir, Path.GetFileName(_dbPath)));
        await freshStore.InitializeAsync();

        var loaded = await freshStore.GetByClientIdAsync(registered.ClientId);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("web-app");
        loaded.ClientType.Should().Be(OAuthClientType.Confidential);
        loaded.RedirectUris.Should().BeEquivalentTo(new[] { "https://app/cb", "https://app/cb2" });
        loaded.AllowedGrantTypes.Should().BeEquivalentTo(new[] { OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken });
        loaded.AllowedScopes.Should().BeEquivalentTo(new[] { "read", "write" });
    }
}
