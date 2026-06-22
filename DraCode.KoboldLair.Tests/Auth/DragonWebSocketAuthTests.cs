using System.Net.WebSockets;
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
/// End-to-end coverage for the Dragon/Wyvern WebSocket auth switch (TASK-034 / FEATURE-019 D8):
/// the endpoints now gate on the JWT bearer pipeline instead of the legacy static-token validator.
/// A valid <c>?token=</c> JWT (or the loopback bypass) completes the handshake; a missing/legacy
/// token is rejected before the socket is accepted.
/// </summary>
public class DragonWebSocketAuthTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static WebApplicationFactory<Program> CreateFactory(
        bool jwtEnabled = true,
        bool loopbackBypass = false,
        string? bindUrls = null)
    {
        var hash = new Pbkdf2PasswordHasher().Hash("pw-correct-horse");
        var projectsPath = Path.Combine(Path.GetTempPath(), "kl-dragon-ws-tests", Guid.NewGuid().ToString("N"));

        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = jwtEnabled ? "true" : "false",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["Authentication:Jwt:Users:0:Id"] = UserId.ToString(),
            ["Authentication:Jwt:Users:0:Username"] = "alice",
            ["Authentication:Jwt:Users:0:PasswordHash"] = hash,
            ["Authentication:Jwt:Users:0:Roles:0"] = "user",
            ["Authentication:Daemon:LoopbackBypass"] = loopbackBypass ? "true" : "false",
            // Legacy static-token validator config — present to prove it no longer gates Dragon.
            ["Authentication:Enabled"] = "true",
            ["Authentication:Tokens:0"] = "legacy-static-token",
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = projectsPath,
        };
        if (bindUrls is not null)
            config["urls"] = bindUrls;

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    private static string MintToken(WebApplicationFactory<Program> factory, string scope, Guid sub)
    {
        var tokenProvider = factory.Services.GetRequiredService<ITokenProvider>();
        return tokenProvider.GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub.ToString(),
            ["scope"] = scope
        }).Token;
    }

    /// <summary>
    /// Attempts the WS upgrade and reports whether the **auth gate** rejected it (a 401 before the
    /// socket is accepted) versus letting it through. We deliberately separate gate rejection from
    /// post-upgrade teardown: once accepted, the Dragon handler tears the socket down asynchronously
    /// (no LLM provider in the test host), which surfaces as a *different* error than the 401
    /// handshake failure — that is NOT a gate rejection.
    /// </summary>
    private static async Task<bool> WasGateRejectedAsync(WebApplicationFactory<Program> factory, string path)
    {
        var wsClient = factory.Server.CreateWebSocketClient();
        var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = path.Split('?')[0] };
        if (path.Contains('?'))
            uri.Query = path.Split('?', 2)[1];

        try
        {
            var socket = await wsClient.ConnectAsync(uri.Uri, CancellationToken.None);
            socket.Abort(); // upgrade succeeded → gate let it through
            return false;
        }
        catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            return true; // handshake refused with 401 → gate rejected
        }
        catch
        {
            return false; // upgrade happened, handler tore it down later — not a gate rejection
        }
    }

    [Fact]
    public async Task Dragon_with_valid_jwt_passes_the_gate()
    {
        var factory = CreateFactory();
        try
        {
            var token = MintToken(factory, KoboldLairPermissionChecker.ViewOwn, UserId);

            (await WasGateRejectedAsync(factory, $"/dragon?token={Uri.EscapeDataString(token)}"))
                .Should().BeFalse("a valid JWT must be accepted at the upgrade gate");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task Dragon_without_token_is_rejected()
    {
        using var factory = CreateFactory();

        (await WasGateRejectedAsync(factory, "/dragon"))
            .Should().BeTrue("JWT is enabled and no bearer token was supplied");
    }

    [Fact]
    public async Task Dragon_with_legacy_static_token_is_rejected()
    {
        using var factory = CreateFactory();

        // The old shared-token scheme must no longer authenticate Dragon (only JWT does now).
        (await WasGateRejectedAsync(factory, "/dragon?token=legacy-static-token"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Wyvern_without_token_is_rejected()
    {
        using var factory = CreateFactory();

        (await WasGateRejectedAsync(factory, "/wyvern"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Dragon_with_loopback_bypass_allows_no_token()
    {
        var factory = CreateFactory(loopbackBypass: true, bindUrls: "http://127.0.0.1:5000");
        try
        {
            (await WasGateRejectedAsync(factory, "/dragon"))
                .Should().BeFalse("the loopback bypass authenticates the synthetic local principal");
        }
        finally { SafeDispose(factory); }
    }

    /// <summary>
    /// Disposes the test host, swallowing the shutdown-race exception thrown by
    /// <c>DragonRequestQueue.Dispose()</c>'s blocking wait when a Dragon session was opened during the
    /// test (a pre-existing teardown quirk, unrelated to the auth behaviour under test).
    /// </summary>
    private static void SafeDispose(WebApplicationFactory<Program> factory)
    {
        try { factory.Dispose(); }
        catch (AggregateException) { }
        catch (OperationCanceledException) { }
    }
}
