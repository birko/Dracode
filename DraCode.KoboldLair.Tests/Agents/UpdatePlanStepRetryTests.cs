using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Agents;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Agents;

/// <summary>
/// TASK-126: <c>update_plan_step</c> with status "failed" decides retry vs. final failure through Birko's
/// <c>ImplementationStep.RetryOrFail</c> and stores Birko's typed error category.
/// </summary>
public class UpdatePlanStepRetryTests
{
    private static (KoboldImplementationPlan Plan, UpdatePlanStepTool Tool) Create(int maxRetries = 3)
    {
        var plan = new KoboldImplementationPlan
        {
            ProjectId = "p",
            TaskId = "t",
            Steps =
            [
                new ImplementationStep { Index = 1, Title = "build", MaxRetries = maxRetries, FilesToCreate = ["a.ts"] },
                new ImplementationStep { Index = 2, Title = "uses a.ts", FilesToModify = ["a.ts"] }
            ]
        };
        plan.Steps[0].Start();
        return (plan, new UpdatePlanStepTool(plan, null, projectId: plan.ProjectId, taskId: plan.TaskId));
    }

    private static Task<string> FailStep(UpdatePlanStepTool tool, string error) =>
        tool.ExecuteAsync("", new Dictionary<string, object> { ["step_index"] = 1, ["status"] = "failed", ["output"] = error });

    [Fact]
    public async Task A_transient_failure_with_retries_left_goes_back_to_pending()
    {
        var (plan, tool) = Create();

        await FailStep(tool, "network error: connection reset by peer");

        var step = plan.Steps[0];
        step.Status.Should().Be(StepStatus.Pending);
        step.RetryCount.Should().Be(1);
        step.StartedAt.Should().BeNull();
        step.ErrorMessage.Should().Be("network error: connection reset by peer");
        step.ErrorCategory.Should().Be(Birko.AI.Models.ErrorCategory.Transient);
        plan.Steps[1].Status.Should().Be(StepStatus.Pending, "a retried step skips nothing");
    }

    [Fact]
    public async Task A_transient_failure_with_no_retries_left_fails_and_says_so()
    {
        var (plan, tool) = Create(maxRetries: 0);

        await FailStep(tool, "network error: connection reset by peer");

        var step = plan.Steps[0];
        step.Status.Should().Be(StepStatus.Failed);
        step.ErrorMessage.Should().Be("Max retries (0) exhausted. Last error: network error: connection reset by peer");
        step.ErrorCategory.Should().Be(Birko.AI.Models.ErrorCategory.Transient);
        plan.Steps[1].Status.Should().Be(StepStatus.Skipped, "its dependent step is skipped");
    }

    [Fact]
    public async Task A_permanent_failure_fails_at_once()
    {
        var (plan, tool) = Create();

        await FailStep(tool, "401 unauthorized: invalid api key");

        plan.Steps[0].Status.Should().Be(StepStatus.Failed);
        plan.Steps[0].RetryCount.Should().Be(0);
        plan.Steps[0].ErrorCategory.Should().Be(Birko.AI.Models.ErrorCategory.Permanent);
    }

    [Fact]
    public async Task A_failure_with_no_message_fails_at_once_with_no_category()
    {
        var (plan, tool) = Create();

        // The classifier says Unknown only for an empty message; anything unrecognised defaults to Transient
        await FailStep(tool, "");

        plan.Steps[0].Status.Should().Be(StepStatus.Failed);
        plan.Steps[0].ErrorCategory.Should().BeNull("DraCode's Unknown category is stored as not classified");
    }
}
