using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Endpoint;

/// <summary>
/// Validation-gate coverage for /kobold project mode (TASK-040). The end-to-end run (real plan + git commit
/// on the feature branch) needs a live Kobold and is covered by the task's human test plan; here we pin the
/// pure pre-flight checks — required ids and the execution-state guard (Paused/Suspended/Cancelled rejected).
/// </summary>
public class ProjectRunModeHandlerTests
{
    private static Project RunnableProject() => new()
    {
        Id = "p1",
        Name = "demo",
        ExecutionState = ProjectExecutionState.Running
    };

    private static KoboldRunRequest Req() => new() { Mode = "project", ProjectId = "p1", TaskId = "TASK-001" };

    [Fact]
    public void EnsureRunnable_passes_for_a_running_project_with_ids()
    {
        var act = () => ProjectRunModeHandler.EnsureRunnable(RunnableProject(), Req());
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureRunnable_rejects_missing_projectId()
    {
        var req = Req(); req.ProjectId = null;
        var act = () => ProjectRunModeHandler.EnsureRunnable(RunnableProject(), req);
        act.Should().Throw<ArgumentException>().WithMessage("*projectId*");
    }

    [Fact]
    public void EnsureRunnable_rejects_missing_taskId()
    {
        var req = Req(); req.TaskId = null;
        var act = () => ProjectRunModeHandler.EnsureRunnable(RunnableProject(), req);
        act.Should().Throw<ArgumentException>().WithMessage("*taskId*");
    }

    [Fact]
    public void EnsureRunnable_rejects_unknown_project()
    {
        var act = () => ProjectRunModeHandler.EnsureRunnable(null, Req());
        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    [Theory]
    [InlineData(ProjectExecutionState.Paused)]
    [InlineData(ProjectExecutionState.Suspended)]
    [InlineData(ProjectExecutionState.Cancelled)]
    public void EnsureRunnable_rejects_non_running_execution_state(ProjectExecutionState state)
    {
        var project = RunnableProject();
        project.ExecutionState = state;
        var act = () => ProjectRunModeHandler.EnsureRunnable(project, Req());
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{state}*");
    }
}
