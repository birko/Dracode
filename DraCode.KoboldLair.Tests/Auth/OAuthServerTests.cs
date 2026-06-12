using System.Collections.Concurrent;
using System.Linq.Expressions;
using Birko.Data.Models;
using Birko.Data.Stores;
using Birko.Security;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Endpoints.Authorize;
using Birko.Security.OAuth.Server.Endpoints.ClientRegistration;
using Birko.Security.OAuth.Server.Endpoints.DeviceAuthorization;
using Birko.Security.OAuth.Server.Endpoints.Token;
using Birko.Security.OAuth.Server.Models;
using Birko.Security.OAuth.Server.Stores;
using Birko.Time;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>
/// Grant-type coverage for the hosted Birko OAuth 2.1 authorization server (TASK-030),
/// exercised at the handler level against in-memory stores with a deterministic clock.
/// </summary>
public class OAuthServerTests
{
    // ---- helpers -------------------------------------------------------------------------

    private sealed class MutableClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTimeOffset OffsetUtcNow => new(UtcNow);
        public DateOnly Today => DateOnly.FromDateTime(UtcNow);
        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }

    // Minimal token provider — grant-flow tests don't assert JWT internals, only that a token is issued.
    private sealed class FakeTokenProvider : ITokenProvider
    {
        public TokenResult GenerateToken(IDictionary<string, string> claims, TokenOptions? options = null)
            => new() { Token = "test-access-token-" + Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddHours(1) };
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N");
        public TokenValidationResult ValidateToken(string token, TokenOptions? options = null)
            => TokenValidationResult.Success(new Dictionary<string, string>());
    }

    private class MemStore<T> : IAsyncStore<T> where T : AbstractModel, new()
    {
        private readonly ConcurrentDictionary<Guid, T> _data = new();
        public Task InitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DestroyAsync(CancellationToken ct = default) { _data.Clear(); return Task.CompletedTask; }
        public Task<long> CountAsync(Expression<Func<T, bool>>? f = null, CancellationToken ct = default)
            => Task.FromResult((long)(f == null ? _data.Values.Count : _data.Values.AsQueryable().Where(f).Count()));
        public Task<T?> ReadAsync(Guid guid, CancellationToken ct = default) { _data.TryGetValue(guid, out var v); return Task.FromResult(v); }
        public Task<T?> ReadAsync(Expression<Func<T, bool>>? f = null, CancellationToken ct = default)
        {
            var q = _data.Values.AsQueryable();
            if (f != null) q = q.Where(f);
            return Task.FromResult(q.FirstOrDefault());
        }
        public Task<Guid> CreateAsync(T data, StoreDataDelegate<T>? p = null, CancellationToken ct = default)
        {
            if (data.Guid == null || data.Guid == Guid.Empty) data.Guid = Guid.NewGuid();
            _data[data.Guid!.Value] = data; p?.Invoke(data);
            return Task.FromResult(data.Guid!.Value);
        }
        public Task UpdateAsync(T data, StoreDataDelegate<T>? p = null, CancellationToken ct = default)
        {
            if (data.Guid != null) _data[data.Guid.Value] = data; p?.Invoke(data);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(T data, CancellationToken ct = default)
        {
            if (data.Guid != null) _data.TryRemove(data.Guid.Value, out _);
            return Task.CompletedTask;
        }
        public Task<Guid> SaveAsync(T data, StoreDataDelegate<T>? p = null, CancellationToken ct = default)
            => data.Guid == null || data.Guid == Guid.Empty ? CreateAsync(data, p, ct)
                : UpdateAsync(data, p, ct).ContinueWith(_ => data.Guid!.Value, TaskScheduler.Default);
        public T CreateInstance() => new();
    }

    private sealed class ClientStore : MemStore<OAuthClient>, IOAuthClientStore { }
    private sealed class CodeStore : MemStore<AuthorizationCode>, IAuthorizationCodeStore { }
    private sealed class RefreshStore : MemStore<RefreshTokenRecord>, IRefreshTokenStore { }
    private sealed class DeviceStore : MemStore<DeviceCodeRecord>, IDeviceCodeStore { }
    private sealed class ConsentStore : MemStore<ConsentRecord>, IConsentStore { }

    private readonly MutableClock _clock = new();
    private readonly OAuthServer _server;

    public OAuthServerTests()
    {
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
        _server = new OAuthServer(settings, new FakeTokenProvider(), tokenOptions,
            new ClientStore(), new CodeStore(), new RefreshStore(), new DeviceStore(), new ConsentStore(),
            deviceVerificationUri: "https://localhost/device", clock: _clock);
    }

    private Task<ClientRegistrationResponse> RegisterAsync(OAuthClientType type, params string[] grants)
        => _server.ClientRegistration.RegisterAsync(new ClientRegistrationRequest
        {
            Name = "test",
            ClientType = type,
            AllowedGrantTypes = grants.ToList(),
            AllowedScopes = new List<string> { "read", "write" }
        });

    // ---- client_credentials --------------------------------------------------------------

    [Fact]
    public async Task ClientCredentials_ValidSecret_IssuesAccessTokenWithoutRefresh()
    {
        var client = await RegisterAsync(OAuthClientType.Confidential, OAuthGrantTypes.ClientCredentials);

        var resp = await _server.Token.HandleAsync(new TokenRequest
        {
            GrantType = OAuthGrantTypes.ClientCredentials,
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret
        });

        resp.AccessToken.Should().NotBeNullOrEmpty();
        resp.RefreshToken.Should().BeNull("client_credentials must not issue a refresh token (RFC 6749 §4.4.3)");
    }

    [Fact]
    public async Task ClientCredentials_WrongSecret_ThrowsInvalidClient()
    {
        var client = await RegisterAsync(OAuthClientType.Confidential, OAuthGrantTypes.ClientCredentials);

        var act = () => _server.Token.HandleAsync(new TokenRequest
        {
            GrantType = OAuthGrantTypes.ClientCredentials,
            ClientId = client.ClientId,
            ClientSecret = "wrong"
        });

        (await act.Should().ThrowAsync<OAuthServerException>()).Which.ErrorCode.Should().Be(OAuthErrorCodes.InvalidClient);
    }

    // ---- authorization_code (+ refresh) --------------------------------------------------

    [Fact]
    public async Task AuthorizationCode_ValidExchange_IssuesTokenPair()
    {
        var (clientId, secret, code) = await IssueAuthCodeAsync();

        var resp = await _server.Token.HandleAsync(new TokenRequest
        {
            GrantType = OAuthGrantTypes.AuthorizationCode,
            ClientId = clientId,
            ClientSecret = secret,
            Code = code,
            RedirectUri = "https://app/cb"
        });

        resp.AccessToken.Should().NotBeNullOrEmpty();
        resp.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AuthorizationCode_Reused_ThrowsInvalidGrant()
    {
        var (clientId, secret, code) = await IssueAuthCodeAsync();
        var req = new TokenRequest { GrantType = OAuthGrantTypes.AuthorizationCode, ClientId = clientId, ClientSecret = secret, Code = code, RedirectUri = "https://app/cb" };
        await _server.Token.HandleAsync(req); // first use

        var act = () => _server.Token.HandleAsync(req); // replay
        (await act.Should().ThrowAsync<OAuthServerException>()).Which.ErrorCode.Should().Be(OAuthErrorCodes.InvalidGrant);
    }

    [Fact]
    public async Task RefreshToken_Rotation_RevokesOldToken()
    {
        var (clientId, secret, code) = await IssueAuthCodeAsync();
        var pair = await _server.Token.HandleAsync(new TokenRequest { GrantType = OAuthGrantTypes.AuthorizationCode, ClientId = clientId, ClientSecret = secret, Code = code, RedirectUri = "https://app/cb" });

        var refreshed = await _server.Token.HandleAsync(new TokenRequest { GrantType = OAuthGrantTypes.RefreshToken, ClientId = clientId, ClientSecret = secret, RefreshToken = pair.RefreshToken });
        refreshed.RefreshToken.Should().NotBeNullOrEmpty().And.NotBe(pair.RefreshToken);

        // replaying the now-revoked original refresh token must fail
        var act = () => _server.Token.HandleAsync(new TokenRequest { GrantType = OAuthGrantTypes.RefreshToken, ClientId = clientId, ClientSecret = secret, RefreshToken = pair.RefreshToken });
        (await act.Should().ThrowAsync<OAuthServerException>()).Which.ErrorCode.Should().Be(OAuthErrorCodes.InvalidGrant);
    }

    private async Task<(string clientId, string clientSecret, string code)> IssueAuthCodeAsync()
    {
        // Register with a redirect URI up front — RegisterAsync rejects an authorization_code client without one.
        var client = await _server.ClientRegistration.RegisterAsync(new ClientRegistrationRequest
        {
            Name = "test",
            ClientType = OAuthClientType.Confidential,
            RedirectUris = new List<string> { "https://app/cb" },
            AllowedGrantTypes = new List<string> { OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken },
            AllowedScopes = new List<string> { "read", "write" }
        });

        var authResp = await _server.Authorize.HandleConsentAsync(new AuthorizeRequest
        {
            ResponseType = "code",
            ClientId = client.ClientId,
            RedirectUri = "https://app/cb",
            Scope = "read"
        }, userId: "user-1", approved: true);

        authResp.Code.Should().NotBeNullOrEmpty();
        return (client.ClientId, client.ClientSecret!, authResp.Code!);
    }

    // ---- device_code ----------------------------------------------------------------------

    [Fact]
    public async Task DeviceCode_PendingThenApproved_IssuesToken()
    {
        var client = await RegisterAsync(OAuthClientType.Public, OAuthGrantTypes.DeviceCode);
        var device = await _server.DeviceAuthorization.HandleAsync(new DeviceAuthorizationRequest { ClientId = client.ClientId });

        // poll while pending
        var pending = () => _server.Token.HandleAsync(new TokenRequest { GrantType = OAuthGrantTypes.DeviceCode, ClientId = client.ClientId, DeviceCode = device.DeviceCode });
        (await pending.Should().ThrowAsync<OAuthServerException>()).Which.ErrorCode.Should().Be(OAuthErrorCodes.AuthorizationPending);

        await _server.DeviceAuthorization.ApproveAsync(device.UserCode, "user-1", approved: true);
        _clock.Advance(TimeSpan.FromSeconds(6)); // clear the polling interval (slow_down)

        var resp = await _server.Token.HandleAsync(new TokenRequest { GrantType = OAuthGrantTypes.DeviceCode, ClientId = client.ClientId, DeviceCode = device.DeviceCode });
        resp.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeviceCode_UnknownCode_ThrowsInvalidGrant()
    {
        var client = await RegisterAsync(OAuthClientType.Public, OAuthGrantTypes.DeviceCode);

        var act = () => _server.Token.HandleAsync(new TokenRequest { GrantType = OAuthGrantTypes.DeviceCode, ClientId = client.ClientId, DeviceCode = "does-not-exist" });
        (await act.Should().ThrowAsync<OAuthServerException>()).Which.ErrorCode.Should().Be(OAuthErrorCodes.InvalidGrant);
    }

    // ---- unsupported ----------------------------------------------------------------------

    [Fact]
    public async Task UnknownGrantType_ThrowsUnsupportedGrantType()
    {
        var act = () => _server.Token.HandleAsync(new TokenRequest { GrantType = "password", ClientId = "x" });
        (await act.Should().ThrowAsync<OAuthServerException>()).Which.ErrorCode.Should().Be(OAuthErrorCodes.UnsupportedGrantType);
    }
}
