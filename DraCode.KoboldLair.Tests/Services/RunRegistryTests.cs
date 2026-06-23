using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Unit coverage for <see cref="RunRegistry"/> (TASK-044) — the run-status fold that backs
/// <c>GET /api/v1/runs/{id}</c>. Drives the real <see cref="KoboldRunEventSource"/> and asserts the
/// Pending → Running → terminal transitions survive run completion, plus the start-failure path.
/// </summary>
public class RunRegistryTests
{
    private static readonly Guid Run = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    /// <summary>Spins until the record reaches a predicate or times out (the fold runs on a background reader).</summary>
    private static async Task WaitUntil(Func<bool> predicate, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!predicate() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    [Fact]
    public void Register_starts_a_run_in_pending_state()
    {
        var registry = new RunRegistry(new KoboldRunEventSource());
        var rec = registry.Register(Run, owner: "user-a", mode: "adhoc");

        rec.State.Should().Be(RunRegistry.RunState.Pending);
        rec.Owner.Should().Be("user-a");
        rec.Mode.Should().Be("adhoc");

        var snapshot = registry.Get(Run)!;
        snapshot.RunId.Should().Be(Run);
        snapshot.State.Should().Be(RunRegistry.RunState.Pending);
        snapshot.Owner.Should().Be("user-a");
    }

    [Fact]
    public async Task Run_transitions_to_running_then_completed_and_survives_completerun()
    {
        var events = new KoboldRunEventSource();
        var registry = new RunRegistry(events);
        registry.Register(Run, "user-a", "adhoc");

        events.Publish(new PlanStepUpdatedEvent { RunId = Run, StepIndex = 0, Status = StepStatus.Completed, CompletedSteps = 1, TotalSteps = 2 });
        await WaitUntil(() => registry.Get(Run)!.State == RunRegistry.RunState.Running);
        registry.Get(Run)!.State.Should().Be(RunRegistry.RunState.Running);
        registry.Get(Run)!.CompletedSteps.Should().Be(1);

        events.Publish(new RunCompletedEvent { RunId = Run, FinalStatus = KoboldStatus.Done, CompletedSteps = 2, TotalSteps = 2 });
        events.CompleteRun(Run); // event source forgets the run — the registry must retain the terminal status

        await WaitUntil(() => registry.Get(Run)!.IsTerminal);
        var rec = registry.Get(Run)!;
        rec.State.Should().Be(RunRegistry.RunState.Completed);
        rec.TotalSteps.Should().Be(2);
        rec.CompletedAt.Should().NotBeNull();
        rec.Summary.Should().Be(nameof(KoboldStatus.Done));
    }

    [Fact]
    public async Task Run_error_event_marks_failed()
    {
        var events = new KoboldRunEventSource();
        var registry = new RunRegistry(events);
        registry.Register(Run, "user-a", "project");

        events.Publish(new RunErrorEvent { RunId = Run, Message = "boom" });
        await WaitUntil(() => registry.Get(Run)!.IsTerminal);

        var rec = registry.Get(Run)!;
        rec.State.Should().Be(RunRegistry.RunState.Failed);
        rec.Summary.Should().Be("boom");
    }

    [Fact]
    public void Fail_marks_a_run_failed_when_start_never_emitted()
    {
        var registry = new RunRegistry(new KoboldRunEventSource());
        registry.Register(Run, "user-a", "project");

        registry.Fail(Run, "project not found");

        var rec = registry.Get(Run)!;
        rec.State.Should().Be(RunRegistry.RunState.Failed);
        rec.Summary.Should().Be("project not found");
        rec.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Terminal_state_is_monotonic_late_events_do_not_resurrect_it()
    {
        var events = new KoboldRunEventSource();
        var registry = new RunRegistry(events);
        registry.Register(Run, "user-a", "adhoc");

        events.Publish(new RunCompletedEvent { RunId = Run, FinalStatus = KoboldStatus.Done, CompletedSteps = 2, TotalSteps = 2 });
        await WaitUntil(() => registry.Get(Run)!.IsTerminal);

        // A stray late plan-step (or error) must NOT flip Completed back to Running/Failed (#7).
        events.Publish(new PlanStepUpdatedEvent { RunId = Run, StepIndex = 1, Status = StepStatus.Completed, CompletedSteps = 1, TotalSteps = 2 });
        events.Publish(new RunErrorEvent { RunId = Run, Message = "late" });
        await Task.Delay(50);

        var rec = registry.Get(Run)!;
        rec.State.Should().Be(RunRegistry.RunState.Completed);
        rec.Summary.Should().Be(nameof(KoboldStatus.Done));
    }

    [Fact]
    public void Get_returns_null_for_an_unknown_run()
    {
        var registry = new RunRegistry(new KoboldRunEventSource());
        registry.Get(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void ListByOwner_scopes_to_the_owner()
    {
        var registry = new RunRegistry(new KoboldRunEventSource());
        var a1 = Guid.NewGuid(); var a2 = Guid.NewGuid(); var b1 = Guid.NewGuid();
        registry.Register(a1, "user-a", "adhoc");
        registry.Register(a2, "user-a", "project");
        registry.Register(b1, "user-b", "adhoc");

        registry.ListByOwner("user-a").Select(r => r.RunId).Should().BeEquivalentTo(new[] { a1, a2 });
        registry.ListByOwner("user-b").Select(r => r.RunId).Should().BeEquivalentTo(new[] { b1 });
    }
}
