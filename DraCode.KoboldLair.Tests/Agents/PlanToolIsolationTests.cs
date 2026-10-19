using System.Collections.Concurrent;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Agents;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Agents;

/// <summary>
/// TASK-120: the plan tools hold per-run state on the instance, so two Kobolds running at once each write to their own
/// plan and raise their own escalation. Before, the state was static and the last Kobold to start owned it.
/// </summary>
public class PlanToolIsolationTests
{
    private sealed record Run(
        KoboldImplementationPlan Plan,
        Guid KoboldId,
        ConcurrentBag<EscalationAlert> Escalations,
        ReflectionTool Reflect,
        UpdatePlanStepTool UpdateStep);

    private static Run CreateRun(string name)
    {
        var plan = new KoboldImplementationPlan
        {
            ProjectId = $"project-{name}",
            TaskId = $"task-{name}",
            Steps =
            [
                new ImplementationStep { Index = 1, Title = $"{name} step 1" },
                new ImplementationStep { Index = 2, Title = $"{name} step 2" }
            ]
        };
        var koboldId = Guid.NewGuid();
        var escalations = new ConcurrentBag<EscalationAlert>();
        return new Run(
            plan,
            koboldId,
            escalations,
            new ReflectionTool(plan, null, null, plan.ProjectId, plan.TaskId, koboldId, "csharp", escalations.Add, null),
            new UpdatePlanStepTool(plan, null, projectId: plan.ProjectId, taskId: plan.TaskId, koboldId: koboldId));
    }

    private static Dictionary<string, object> Escalate(string blocker) => new()
    {
        ["progress_percent"] = 10,
        ["confidence_percent"] = 80,
        ["decision"] = "escalate",
        ["escalation_type"] = "needs_split",
        ["blockers"] = blocker
    };

    [Fact]
    public async Task Two_Kobolds_reflecting_at_once_each_write_to_their_own_plan_and_raise_their_own_escalation()
    {
        var a = CreateRun("a");
        var b = CreateRun("b");
        const int calls = 25;

        await Task.WhenAll(Enumerable.Range(0, calls).SelectMany(i => new[]
        {
            Task.Run(() => a.Reflect.ExecuteAsync("", Escalate($"a-{i}"))),
            Task.Run(() => b.Reflect.ExecuteAsync("", Escalate($"b-{i}")))
        }));

        foreach (var (run, name) in new[] { (a, "a"), (b, "b") })
        {
            run.Plan.Reflections.Should().HaveCount(calls)
                .And.OnlyContain(r => r.Blockers!.StartsWith($"{name}-"));
            run.Plan.Escalations.Should().HaveCount(calls)
                .And.OnlyContain(e => e.ProjectId == run.Plan.ProjectId && e.KoboldId == run.KoboldId);
            run.Escalations.Should().HaveCount(calls)
                .And.OnlyContain(e => e.KoboldId == run.KoboldId && e.Summary.Contains($"{name}-"));
        }
    }

    [Fact]
    public async Task Two_Kobolds_completing_steps_at_once_each_advance_their_own_plan()
    {
        var a = CreateRun("a");
        var b = CreateRun("b");

        await Task.WhenAll(
            Task.Run(() => a.UpdateStep.ExecuteAsync("", new() { ["step_index"] = 1, ["status"] = "completed" })),
            Task.Run(() => b.UpdateStep.ExecuteAsync("", new() { ["step_index"] = 2, ["status"] = "skipped" })));

        a.Plan.Steps[0].Status.Should().Be(StepStatus.Completed);
        a.Plan.Steps[1].Status.Should().Be(StepStatus.Pending);
        b.Plan.Steps[0].Status.Should().Be(StepStatus.Pending);
        b.Plan.Steps[1].Status.Should().Be(StepStatus.Skipped);
    }
}
