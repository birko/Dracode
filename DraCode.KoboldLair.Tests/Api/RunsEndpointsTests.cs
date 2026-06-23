using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Api;

/// <summary>
/// Coverage for the /api/v1/runs REST surface (TASK-044): start returns a runId (202), status is
/// queryable, and ownership is enforced (a caller can't read another caller's run → 404, not 403, so
/// existence isn't leaked). A stub <see cref="IKoboldRunModeHandler"/> ("test" mode) stands in for the
/// real adhoc/project run so the HTTP tests stay deterministic (no git / no LLM). Mirrors ApiV1SkeletonTests.
/// </summary>
public class RunsEndpointsTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private static readonly string UserA = "11111111-1111-1111-1111-111111111111";
    private static readonly string UserB = "22222222-2222-2222-2222-222222222222";

    /// <summary>A run-mode handler that completes instantly via the event source — no git, no Kobold, no LLM.</summary>
    private sealed class StubRunModeHandler : IKoboldRunModeHandler
    {
        private readonly KoboldRunEventSource _events;
        public StubRunModeHandler(KoboldRunEventSource events) => _events = events;
        public string Mode => "test";
        public Task<KoboldRunStartInfo> StartAsync(KoboldRunRequest request, Guid runId, KoboldCaller caller, CancellationToken ct)
        {
            _events.Publish(new RunCompletedEvent { RunId = runId, FinalStatus = KoboldStatus.Done });
            return Task.FromResult(new KoboldRunStartInfo("test", null));
        }
    }

    /// <summary>Throws a non-caller-error from StartAsync — exercises the broadened catch (→ 500, run failed, #1).</summary>
    private sealed class ThrowingRunModeHandler : IKoboldRunModeHandler
    {
        public string Mode => "boom";
        public Task<KoboldRunStartInfo> StartAsync(KoboldRunRequest request, Guid runId, KoboldCaller caller, CancellationToken ct)
            => throw new IOException("disk gone");
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var projectsPath = Path.Combine(Path.GetTempPath(), "kl-runs-tests", Guid.NewGuid().ToString("N"));
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = projectsPath,
        };

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IKoboldRunModeHandler, StubRunModeHandler>();
                services.AddSingleton<IKoboldRunModeHandler, ThrowingRunModeHandler>();
            });
        });
    }

    private static string MintToken(WebApplicationFactory<Program> factory, string sub, string scope) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = scope
        }).Token;

    private static HttpRequestMessage Authed(HttpMethod method, string uri, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, uri) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private static async Task<string> RunIdFrom(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("runId").GetString()!;
    }

    [Fact]
    public async Task Post_runs_without_token_returns_401()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/runs", new { mode = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_runs_starts_a_run_and_returns_202_with_runid()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, KoboldLairPermissionChecker.ViewOwn);

        var response = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/runs", token, new { mode = "test" }));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Guid.TryParse(await RunIdFrom(response), out _).Should().BeTrue();
        response.Headers.Location!.ToString().Should().Contain("/api/v1/runs/");
    }

    [Fact]
    public async Task Post_runs_with_unknown_mode_returns_400()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, KoboldLairPermissionChecker.ViewOwn);

        var response = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/runs", token, new { mode = "nonsense" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_own_run_returns_200_with_status()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, KoboldLairPermissionChecker.ViewOwn);

        var start = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/runs", token, new { mode = "test" }));
        var runId = await RunIdFrom(start);

        var get = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/runs/{runId}", token));

        get.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("runId").GetString().Should().Be(runId);
        doc.RootElement.GetProperty("mode").GetString().Should().Be("test");
        doc.RootElement.GetProperty("status").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Get_run_owned_by_another_caller_returns_404()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var tokenA = MintToken(factory, UserA, KoboldLairPermissionChecker.ViewOwn);
        var tokenB = MintToken(factory, UserB, KoboldLairPermissionChecker.ViewOwn);

        var start = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/runs", tokenA, new { mode = "test" }));
        var runId = await RunIdFrom(start);

        var get = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/runs/{runId}", tokenB));

        get.StatusCode.Should().Be(HttpStatusCode.NotFound, "a caller must not read (or learn of) another's run");
    }

    [Fact]
    public async Task Get_unknown_run_returns_404()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, KoboldLairPermissionChecker.ViewOwn);

        var get = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/runs/{Guid.NewGuid()}", token));

        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_runs_with_a_handler_that_throws_returns_500_not_unhandled()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA, KoboldLairPermissionChecker.ViewOwn);

        // 'boom' handler throws IOException (not Argument/InvalidOperation) → controlled 500, run failed (#1).
        var response = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/runs", token, new { mode = "boom" }));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Subjectless_callers_cannot_read_each_others_runs()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        // Tokens whose 'sub' isn't a Guid → ICurrentUser.UserId is null → owner is null. The ownership check
        // must NOT treat two null-identity callers as the same owner (#5).
        var tokenX = MintToken(factory, "not-a-guid-x", KoboldLairPermissionChecker.ViewOwn);
        var tokenY = MintToken(factory, "not-a-guid-y", KoboldLairPermissionChecker.ViewOwn);

        var start = await client.SendAsync(Authed(HttpMethod.Post, "/api/v1/runs", tokenX, new { mode = "test" }));
        start.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var runId = await RunIdFrom(start);

        var get = await client.SendAsync(Authed(HttpMethod.Get, $"/api/v1/runs/{runId}", tokenY));
        get.StatusCode.Should().Be(HttpStatusCode.NotFound, "a null-identity run must not be readable by another null-identity caller");
    }
}
