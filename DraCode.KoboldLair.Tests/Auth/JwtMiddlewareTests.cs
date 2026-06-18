using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Birko.Security;
using Birko.Security.Hashing;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>
/// Pure-logic coverage for the daemon loopback bypass decision (TASK-032) — no host required.
/// </summary>
public class DaemonLoopbackTests
{
    [Fact]
    public void Disabled_flag_never_bypasses()
    {
        DaemonLoopback.ShouldBypass(false, ["http://127.0.0.1:5000"]).Should().BeFalse();
    }

    [Fact]
    public void Enabled_with_all_loopback_binds_bypasses()
    {
        DaemonLoopback.ShouldBypass(true, ["http://127.0.0.1:5000", "http://localhost:5001", "http://[::1]:5002"])
            .Should().BeTrue();
    }

    [Fact]
    public void Enabled_but_any_non_loopback_bind_enforces()
    {
        DaemonLoopback.ShouldBypass(true, ["http://127.0.0.1:5000", "http://0.0.0.0:8080"]).Should().BeFalse();
        DaemonLoopback.ShouldBypass(true, ["http://192.168.1.10:5000"]).Should().BeFalse();
    }

    [Fact]
    public void Enabled_but_unknown_binds_fail_safe_to_enforce()
    {
        DaemonLoopback.ShouldBypass(true, null).Should().BeFalse();
        DaemonLoopback.ShouldBypass(true, []).Should().BeFalse();
    }
}

/// <summary>
/// End-to-end coverage for the JWT validation middleware (TASK-032): 401 without a token, 200 with
/// a valid token, the <c>?token=</c> query path, the permission filter, and the daemon loopback
/// bypass. Boots the real server via <see cref="WebApplicationFactory{T}"/> with a JSON backend and
/// background services removed to keep the host light.
/// </summary>
public class JwtMiddlewareTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static WebApplicationFactory<Program> CreateFactory(
        bool loopbackBypass = false,
        string? bindUrls = null)
    {
        var hash = new Pbkdf2PasswordHasher().Hash("pw-correct-horse");
        var projectsPath = Path.Combine(Path.GetTempPath(), "kl-jwt-tests", Guid.NewGuid().ToString("N"));

        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["Authentication:Jwt:Users:0:Id"] = AdminId.ToString(),
            ["Authentication:Jwt:Users:0:Username"] = "admin",
            ["Authentication:Jwt:Users:0:PasswordHash"] = hash,
            ["Authentication:Jwt:Users:0:Roles:0"] = "admin",
            ["Authentication:Daemon:LoopbackBypass"] = loopbackBypass ? "true" : "false",
            // JSON-file backend avoids SQLite migration + SQL repos for a light test host.
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = projectsPath,
        };
        if (bindUrls is not null)
            config["urls"] = bindUrls;

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            // Strip background services (Drake/Wyrm/Wyvern loops, job scheduler) — these tests only
            // exercise the HTTP auth pipeline.
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    private static string MintToken(WebApplicationFactory<Program> factory, string scope, Guid? sub = null)
    {
        var tokenProvider = factory.Services.GetRequiredService<ITokenProvider>();
        return tokenProvider.GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = (sub ?? AdminId).ToString(),
            ["scope"] = scope
        }).Token;
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_with_valid_token_returns_200()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, scope: KoboldLairPermissionChecker.ViewOwn);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Query_string_token_authenticates_for_sse_ws_reuse()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, scope: KoboldLairPermissionChecker.ViewOwn);

        // No Authorization header — token only in the query string (OnMessageReceived path).
        var response = await client.GetAsync($"/api/v1/whoami?token={Uri.EscapeDataString(token)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Authenticated_but_missing_permission_returns_403()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        // Has a scope, but not view_own and not the "*" wildcard.
        var token = MintToken(factory, scope: KoboldLairPermissionChecker.ViewAll);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_then_call_protected_endpoint_succeeds_end_to_end()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/auth/login", new { username = "admin", password = "pw-correct-horse" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        body!.Token.Should().NotBeNullOrEmpty();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Loopback_daemon_bypass_allows_no_token()
    {
        using var factory = CreateFactory(loopbackBypass: true, bindUrls: "http://127.0.0.1:5000");
        var client = factory.CreateClient();

        // No token at all — the synthetic loopback principal carries the "*" permission.
        var response = await client.GetAsync("/api/v1/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
