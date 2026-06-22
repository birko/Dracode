using System.Threading.Channels;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Events;

/// <summary>
/// Coverage for the per-run event source (TASK-037): subscribe/publish ordering, drop-if-no-subscriber,
/// attach-mid-run, bounded DropOldest, completion, and the scripted publish-point sequence a real run emits.
/// (Full LLM-driven Kobold runs aren't unit-testable — no fake provider — so the publish points are
/// exercised directly; see the task's test-scope note.)
/// </summary>
public class KoboldRunEventSourceTests
{
    private static readonly Guid Run = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static async Task<List<KoboldRunEvent>> Drain(ChannelReader<KoboldRunEvent> r, int count)
    {
        var list = new List<KoboldRunEvent>();
        for (int i = 0; i < count; i++) list.Add(await r.ReadAsync());
        return list;
    }

    [Fact]
    public void Publish_with_no_subscriber_is_a_noop_and_never_throws()
    {
        var src = new KoboldRunEventSource();
        var act = () => src.Publish(new RunErrorEvent { RunId = Run, Message = "x" });
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Subscriber_receives_events_in_order_with_monotonic_sequence()
    {
        var src = new KoboldRunEventSource();
        using var sub = src.Subscribe(Run, out var reader);

        src.Publish(new ToolCallStartedEvent { RunId = Run, ToolName = "write_file" });
        src.Publish(new ToolCallResultEvent { RunId = Run, ToolName = "write_file" });

        var got = await Drain(reader, 2);
        got.Select(e => e.Kind).Should().Equal("tool_call_started", "tool_call_result");
        got.Select(e => e.Sequence).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Subscriber_attached_mid_run_only_sees_subsequent_events()
    {
        var src = new KoboldRunEventSource();
        // Published before anyone subscribes → dropped.
        src.Publish(new ToolCallStartedEvent { RunId = Run, ToolName = "before" });

        using var sub = src.Subscribe(Run, out var reader);
        src.Publish(new ToolCallStartedEvent { RunId = Run, ToolName = "after" });

        var got = await Drain(reader, 1);
        got.Should().ContainSingle().Which.Should().BeOfType<ToolCallStartedEvent>()
            .Which.ToolName.Should().Be("after");
    }

    [Fact]
    public async Task Slow_subscriber_drops_oldest_instead_of_blocking()
    {
        var src = new KoboldRunEventSource(capacity: 2);
        using var sub = src.Subscribe(Run, out var reader);

        // Flood beyond capacity without reading — must not block; oldest are dropped.
        for (int i = 0; i < 5; i++)
            src.Publish(new ToolCallResultEvent { RunId = Run, ToolName = $"t{i}", ResultPreview = i.ToString() });

        var first = await reader.ReadAsync() as ToolCallResultEvent;
        // Only the newest 2 survive (t3, t4) — the buffer dropped t0..t2.
        first!.ResultPreview.Should().Be("3");
    }

    [Fact]
    public async Task CompleteRun_completes_the_reader()
    {
        var src = new KoboldRunEventSource();
        using var sub = src.Subscribe(Run, out var reader);

        src.CompleteRun(Run);

        var completed = await reader.WaitToReadAsync();
        completed.Should().BeFalse("the reader is completed once the run ends");
    }

    [Fact]
    public async Task Scripted_run_emits_expected_event_sequence()
    {
        var src = new KoboldRunEventSource();
        using var sub = src.Subscribe(Run, out var reader);

        // The order a real run produces, with the real reused payload types.
        src.Publish(new ToolCallStartedEvent { RunId = Run, ToolName = "write_file" });
        src.Publish(new ToolCallResultEvent { RunId = Run, ToolName = "write_file" });
        src.Publish(new ReflectionEvent { RunId = Run, Entry = new ReflectionEntry { ConfidencePercent = 70 } });
        src.Publish(new PlanStepUpdatedEvent { RunId = Run, StepIndex = 1, Status = StepStatus.Completed, CompletedSteps = 1, TotalSteps = 3 });
        src.Publish(new RunCompletedEvent { RunId = Run, FinalStatus = KoboldStatus.Done, CompletedSteps = 3, TotalSteps = 3 });

        var got = await Drain(reader, 5);
        got.Select(e => e.Kind).Should().Equal(
            "tool_call_started", "tool_call_result", "reflection", "plan_step_updated", "run_completed");
    }
}
