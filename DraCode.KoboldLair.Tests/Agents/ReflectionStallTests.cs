using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Agents;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Agents;

/// <summary>
/// TASK-182: progress flat at 100% means the step is done, not stuck, so it raises no stall escalation; flat below
/// 100% still does.
/// </summary>
public class ReflectionStallTests
{
    private static (ReflectionTool Tool, List<EscalationAlert> Escalations) NewTool()
    {
        var plan = new KoboldImplementationPlan
        {
            ProjectId = "p", TaskId = "t",
            Steps = [new ImplementationStep { Index = 1, Title = "only step" }]
        };
        var escalations = new List<EscalationAlert>();
        return (new ReflectionTool(plan, null, null, "p", "t", Guid.NewGuid(), "csharp", escalations.Add, null), escalations);
    }

    private static Dictionary<string, object> Reflection(int progress) => new()
    {
        ["progress_percent"] = progress,
        ["confidence_percent"] = 90,
        ["decision"] = "continue"
    };

    [Fact]
    public async Task Reflections_flat_at_100_percent_raise_no_stall_escalation()
    {
        var (tool, escalations) = NewTool();

        for (var i = 0; i < 3; i++)
            await tool.ExecuteAsync("", Reflection(100));

        escalations.Should().BeEmpty("a step reported 100% done is finished, not stalled");
    }

    [Fact]
    public async Task Reflections_flat_below_100_percent_still_raise_a_stall_escalation()
    {
        var (tool, escalations) = NewTool();

        for (var i = 0; i < 3; i++)
            await tool.ExecuteAsync("", Reflection(60));

        escalations.Should().ContainSingle().Which.Summary.Should().Contain("Progress stalled");
    }
}
