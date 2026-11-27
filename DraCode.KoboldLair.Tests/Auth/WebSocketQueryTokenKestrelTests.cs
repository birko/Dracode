using System.Net;
using System.Net.WebSockets;
using Birko.Security;
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
/// TASK-174: the WebSocket endpoints on a real Kestrel socket with JWT on. TestServer puts the WebSocket feature on the
/// request itself, so the other socket tests cannot tell whether <c>UseWebSockets</c> runs before
/// <c>UseAuthentication</c> — and Birko reads <c>?token=</c> only when the request is known to be a WebSocket upgrade.
/// </summary>
public sealed class WebSocketQueryTokenKestrelTests(WebSocketQueryTokenKestrelTests.KestrelHost host)
    : IClassFixture<WebSocketQueryTokenKestrelTests.KestrelHost>
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly WebApplicationFactory<Program> _factory = host.Factory;
    private readonly Uri _baseAddress = host.BaseAddress;

    /// <summary>One server for the class: every test only connects, and each host takes seconds to stop.</summary>
    public sealed class KestrelHost : IDisposable
    {
        public WebApplicationFactory<Program> Factory { get; }
        public Uri BaseAddress { get; }

        public KestrelHost()
        {
            var projectsPath = Path.Combine(Path.GetTempPath(), "kl-ws-kestrel-tests", Guid.NewGuid().ToString("N"));
            var config = new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Enabled"] = "true",
                ["Authentication:Jwt:Secret"] = Secret,
                ["Authentication:Jwt:Issuer"] = "KoboldLair",
                ["Authentication:Jwt:Audience"] = "KoboldLair",
                ["Authentication:Daemon:LoopbackBypass"] = "false",
                ["KoboldLair:ProjectsPath"] = projectsPath,
                // A free loopback port, so the run never collides with another server on the default 5000.
                ["urls"] = "http://127.0.0.1:0",
            };

            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
                builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
            });
            Factory.UseKestrel();
            Factory.StartServer();
            BaseAddress = Factory.ClientOptions.BaseAddress;
        }

        public void Dispose()
        {
            try { Factory.Dispose(); }
            catch (AggregateException) { }
            catch (OperationCanceledException) { }
        }
    }

    private string MintToken() =>
        _factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = UserId.ToString(),
            ["scope"] = KoboldLairPermissionChecker.ViewOwn,
        }).Token;

    private Uri SocketUri(string path, string? token)
    {
        var uri = new UriBuilder(_baseAddress) { Scheme = "ws", Path = path };
        if (token is not null)
            uri.Query = "token=" + Uri.EscapeDataString(token);
        return uri.Uri;
    }

    /// <summary>Connects over a real socket and returns the upgrade's status code (101 when accepted).</summary>
    private async Task<HttpStatusCode> ConnectAsync(string path, string? token)
    {
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await socket.ConnectAsync(SocketUri(path, token), timeout.Token);
            var status = socket.HttpStatusCode;
            socket.Abort();
            return status;
        }
        catch (WebSocketException)
        {
            return socket.HttpStatusCode;
        }
    }

    [Theory]
    [InlineData("/dragon")]
    [InlineData("/kobold")]
    public async Task A_query_token_is_accepted_on_a_real_socket(string path)
    {
        (await ConnectAsync(path, MintToken())).Should().Be(HttpStatusCode.SwitchingProtocols,
            "a browser can only send its token as ?token= on a WebSocket");
    }

    [Theory]
    [InlineData("/dragon")]
    [InlineData("/kobold")]
    public async Task An_upgrade_without_a_token_is_refused(string path)
    {
        (await ConnectAsync(path, null)).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/dragon")]
    [InlineData("/kobold")]
    public async Task An_upgrade_with_a_bad_token_is_refused(string path)
    {
        (await ConnectAsync(path, "not-a-jwt")).Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Birko reads a query token only where a header cannot be set; a plain REST call must use the header.</summary>
    [Fact]
    public async Task A_plain_api_request_ignores_a_query_token()
    {
        using var client = new HttpClient { BaseAddress = _baseAddress };

        var response = await client.GetAsync($"/api/v1/whoami?token={Uri.EscapeDataString(MintToken())}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
