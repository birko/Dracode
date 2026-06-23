using System.Threading.Channels;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Endpoint;

/// <summary>
/// Protocol round-trip coverage for the /kobold endpoint (TASK-038), driving the transport-agnostic
/// <see cref="KoboldEndpointService.PumpAsync"/> against a real <see cref="KoboldRunEventSource"/> and a
/// list-collecting sink. The WS glue (accept/auth/receive) is exercised by the live websocat step in the
/// task's human test plan; the ordered <c>kobold_*</c> projection + terminal/cancel behaviour are unit-tested here.
/// </summary>
public class KoboldEndpointProtocolTests
{
    private static readonly Guid Run = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    /// <summary>Collects every frame the pump sends, in order.</summary>
    private static (List<KoboldWireMessage> sink, Func<KoboldWireMessage, CancellationToken, Task> send) Sink()
    {
        var list = new List<KoboldWireMessage>();
        return (list, (msg, _) => { list.Add(msg); return Task.CompletedTask; });
    }

    [Fact]
    public async Task Pump_emits_run_started_then_ordered_kobold_frames_through_to_complete()
    {
        var src = new KoboldRunEventSource();
        using var subscription = src.Subscribe(Run, out var reader);
        var (sink, send) = Sink();

        var pump = KoboldEndpointService.PumpAsync(
            Run, new KoboldRunStartInfo("project", ".worktrees/feature-x"), reader, send, CancellationToken.None);

        // A realistic publish sequence from a Kobold run.
        src.Publish(new ToolCallStartedEvent { RunId = Run, ToolName = "write_file", InputJson = "{\"path\":\"a.cs\"}" });
        src.Publish(new ToolCallResultEvent { RunId = Run, ToolName = "write_file", ResultPreview = "ok" });
        src.Publish(new ReflectionEvent { RunId = Run, Entry = new ReflectionEntry { ConfidencePercent = 80 } });
        src.Publish(new PlanStepUpdatedEvent { RunId = Run, StepIndex = 0, Status = StepStatus.Completed, CompletedSteps = 1, TotalSteps = 2 });
        src.Publish(new RunCompletedEvent { RunId = Run, FinalStatus = KoboldStatus.Done, CompletedSteps = 2, TotalSteps = 2 });

        await pump; // terminal RunCompleted ends the pump

        sink.Select(m => m.Type).Should().Equal(
            "kobold_run_started",
            "kobold_tool_call",
            "kobold_tool_call",
            "kobold_reflect",
            "kobold_stream",
            "kobold_complete");

        var started = sink[0];
        started.RunId.Should().Be(Run);
        started.Mode.Should().Be("project");
        started.Worktree.Should().Be(".worktrees/feature-x");
        started.Seq.Should().Be(0);

        sink[1].Phase.Should().Be("started");
        sink[2].Phase.Should().Be("result");
        sink.Last().FinalStatus.Should().Be(nameof(KoboldStatus.Done));

        // Monotonic ordering carried through from the event source.
        sink.Skip(1).Select(m => m.Seq).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Pump_maps_run_error_to_error_frame_and_stops()
    {
        var src = new KoboldRunEventSource();
        using var subscription = src.Subscribe(Run, out var reader);
        var (sink, send) = Sink();

        var pump = KoboldEndpointService.PumpAsync(
            Run, new KoboldRunStartInfo("adhoc", null), reader, send, CancellationToken.None);

        src.Publish(new RunErrorEvent { RunId = Run, Message = "boom" });
        await pump;

        sink.Select(m => m.Type).Should().Equal("kobold_run_started", "error");
        sink.Last().Message.Should().Be("boom");
    }

    [Fact]
    public async Task Cancelling_the_pump_stops_relay_without_completing_the_run()
    {
        var src = new KoboldRunEventSource();
        using var subscription = src.Subscribe(Run, out var reader);
        var (sink, send) = Sink();
        using var cts = new CancellationTokenSource();

        var pump = KoboldEndpointService.PumpAsync(
            Run, new KoboldRunStartInfo("project", null), reader, send, cts.Token);

        src.Publish(new ToolCallStartedEvent { RunId = Run, ToolName = "read_file" });
        // Simulate a client disconnect: the endpoint cancels the pump (it does NOT call CompleteRun).
        cts.Cancel();

        await pump; // returns cleanly on cancellation, does not throw

        // The run is still live — a fresh subscriber can attach and the Kobold keeps publishing.
        using var late = src.Subscribe(Run, out var lateReader);
        src.Publish(new RunCompletedEvent { RunId = Run, FinalStatus = KoboldStatus.Done });
        var next = await lateReader.ReadAsync();
        next.Should().BeOfType<RunCompletedEvent>();

        sink.Should().Contain(m => m.Type == "kobold_run_started");
        sink.Should().NotContain(m => m.Type == "kobold_complete");
    }
}
