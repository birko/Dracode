using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Birko.Security;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Server.Api;
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
/// <c>GET /api/v1/runs/{id}/events</c> (TASK-045): a run's events as Server-Sent Events, in order, until the run ends;
/// <c>?token=</c> auth for browsers; the same ownership rule as the status endpoint; heartbeats on an idle stream; a
/// disconnect detaches the subscription. A "manual" run mode starts a run and publishes nothing, so each test drives the
/// run's events itself through the server's own event source.
/// </summary>
public class RunEventsSseTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long-xyz";
    private const string UserA = "11111111-1111-1111-1111-111111111111";
    private const string UserB = "22222222-2222-2222-2222-222222222222";

    private sealed class ManualRunModeHandler : IKoboldRunModeHandler
    {
        public string Mode => "manual";
        public Task<KoboldRunStartInfo> StartAsync(KoboldRunRequest request, Guid runId, KoboldCaller caller, CancellationToken ct)
            => Task.FromResult(new KoboldRunStartInfo("manual", null));
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "true",
            ["Authentication:Jwt:Secret"] = Secret,
            ["Authentication:Jwt:Issuer"] = "KoboldLair",
            ["Authentication:Jwt:Audience"] = "KoboldLair",
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = Path.Combine(Path.GetTempPath(), "kl-sse-tests", Guid.NewGuid().ToString("N")),
        };

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IKoboldRunModeHandler, ManualRunModeHandler>();
            });
        });
    }

    private static string MintToken(WebApplicationFactory<Program> factory, string sub) =>
        factory.Services.GetRequiredService<ITokenProvider>().GenerateToken(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["scope"] = KoboldLairPermissionChecker.ViewOwn
        }).Token;

    private static async Task<Guid> StartRunAsync(HttpClient client, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/runs")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { mode = "manual" })
        };
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return Guid.Parse(doc.RootElement.GetProperty("runId").GetString()!);
    }

    private static Task<HttpResponseMessage> OpenStreamAsync(HttpClient client, Guid runId, string token) =>
        // ?token= is how a browser EventSource authenticates — no Authorization header here on purpose
        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/runs/{runId}/events?token={token}"),
            HttpCompletionOption.ResponseHeadersRead);

    /// <summary>Reads SSE frames ("event" name, "data" JSON) until the server closes the stream.</summary>
    private static async Task<List<(string Event, string Data)>> ReadFramesAsync(HttpResponseMessage response, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
        var frames = new List<(string, string)>();
        string? name = null, data = null;
        while (await reader.ReadLineAsync(cts.Token) is { } line)
        {
            if (line.StartsWith("event: ")) name = line["event: ".Length..];
            else if (line.StartsWith("data: ")) data = line["data: ".Length..];
            else if (line.Length == 0 && name != null)
            {
                frames.Add((name, data ?? ""));
                name = data = null;
            }
        }
        return frames;
    }

    [Fact]
    public async Task The_stream_delivers_the_runs_events_in_order_and_closes_at_the_end()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA);
        var events = factory.Services.GetRequiredService<KoboldRunEventSource>();
        var runId = await StartRunAsync(client, token);

        using var response = await OpenStreamAsync(client, runId, token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        events.Publish(new RunStartedEvent { RunId = runId });
        events.Publish(new ToolCallStartedEvent { RunId = runId, ToolName = "list_files", InputJson = "{}" });
        events.Publish(new ToolCallResultEvent { RunId = runId, ToolName = "list_files", ResultPreview = "a.txt" });
        events.Publish(new ReflectionEvent { RunId = runId });
        events.Publish(new PlanStepUpdatedEvent { RunId = runId, StepIndex = 0, Status = StepStatus.Completed, CompletedSteps = 1, TotalSteps = 1 });
        events.Publish(new RunCompletedEvent { RunId = runId, FinalStatus = KoboldStatus.Done, CompletedSteps = 1, TotalSteps = 1 });

        var frames = await ReadFramesAsync(response, TimeSpan.FromSeconds(10));

        frames.Select(f => f.Event).Should().Equal(
            "kobold_run_started", "kobold_tool_call", "kobold_tool_call", "kobold_reflect", "kobold_stream", "kobold_complete");
        using var last = JsonDocument.Parse(frames[^1].Data);
        last.RootElement.GetProperty("finalStatus").GetString().Should().Be("Done");
        last.RootElement.GetProperty("runId").GetGuid().Should().Be(runId);
    }

    [Fact]
    public async Task A_run_that_already_ended_gets_its_ending_and_the_stream_closes()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var token = MintToken(factory, UserA);
        var events = factory.Services.GetRequiredService<KoboldRunEventSource>();
        var registry = factory.Services.GetRequiredService<RunRegistry>();
        var runId = await StartRunAsync(client, token);
        events.Publish(new RunErrorEvent { RunId = runId, Message = "boom" });
        for (var i = 0; i < 200 && registry.Get(runId)?.IsTerminal != true; i++) await Task.Delay(10);

        using var response = await OpenStreamAsync(client, runId, token);
        var frames = await ReadFramesAsync(response, TimeSpan.FromSeconds(10));

        frames.Should().ContainSingle();
        frames[0].Event.Should().Be("error");
        frames[0].Data.Should().Contain("boom");
    }

    [Fact]
    public async Task Another_callers_run_is_not_found()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var runId = await StartRunAsync(client, MintToken(factory, UserA));

        using var response = await OpenStreamAsync(client, runId, MintToken(factory, UserB));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Without_a_token_the_stream_is_refused()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var runId = await StartRunAsync(client, MintToken(factory, UserA));

        using var response = await client.GetAsync($"/api/v1/runs/{runId}/events", HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_idle_stream_sends_heartbeats_and_a_disconnect_detaches_it()
    {
        var previous = RunsEndpoints.SseHeartbeat;
        RunsEndpoints.SseHeartbeat = TimeSpan.FromMilliseconds(100);
        try
        {
            using var factory = CreateFactory();
            var client = factory.CreateClient();
            var token = MintToken(factory, UserA);
            var events = factory.Services.GetRequiredService<KoboldRunEventSource>();
            var runId = await StartRunAsync(client, token);
            var before = events.SubscriberCount(runId); // the run registry's own subscription

            var response = await OpenStreamAsync(client, runId, token);
            var stream = await response.Content.ReadAsStreamAsync();
            var reader = new StreamReader(stream);
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                (await reader.ReadLineAsync(cts.Token)).Should().Be(": heartbeat");
            events.SubscriberCount(runId).Should().Be(before + 1);

            reader.Dispose();
            response.Dispose();
            for (var i = 0; i < 300 && events.SubscriberCount(runId) > before; i++) await Task.Delay(10);

            events.SubscriberCount(runId).Should().Be(before, "the stream's subscription is detached when the client leaves");
        }
        finally
        {
            RunsEndpoints.SseHeartbeat = previous;
        }
    }
}
