using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// A reasoning-monitor escalation goes to the Drake that holds its task (TASK-169). A project has one Drake per task
/// area; the monitor used to take the project's first Drake, which in a project with more than one area often did not
/// hold the task and so answered "task not found" and did nothing.
/// </summary>
public class MonitorEscalationRoutingTests : IAsyncLifetime
{
    private string _dir = null!;
    private KoboldFactory _kobolds = null!;
    private SqlProjectRepository _projects = null!;
    private const string ProjectId = "proj-route";

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_route_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_dir, "tasks"));
        _projects = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await _projects.InitializeAsync();
        var config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir,
            DefaultProvider = "ollama",
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = "ollama", Type = "ollama", DefaultModel = "m1",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" },
                    Configuration = new()
                }
            }
        };
        _kobolds = new KoboldFactory(_projects, NullLoggerFactory.Instance, config);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private (Drake drake, TaskTracker tracker) NewDrake(string area)
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "tasks", $"{area}-tasks.md"), "ollama", projectId: ProjectId);
        return (drake, tracker);
    }

    private static EscalationAlert WrongAgent(TaskRecord task) => new()
    {
        ProjectId = ProjectId,
        TaskId = task.Id,
        Type = EscalationType.WrongAgentType,
        Summary = "The task needs a different agent type"
    };

    [Fact]
    public async Task The_escalation_reaches_the_second_Drake_when_it_holds_the_task()
    {
        var (backend, backendTasks) = NewDrake("backend");
        var (frontend, frontendTasks) = NewDrake("frontend");
        backendTasks.AddTask("[api-1] Build the store");
        var task = frontendTasks.AddTask("[ui-1] Draw the login form");
        frontendTasks.UpdateTask(task, TaskStatus.Working, "csharp");

        var handledBy = await ReasoningMonitorService.RouteToHoldingDrakeAsync(WrongAgent(task),
            () => new[] { backend, frontend }, NullLogger.Instance, maxRetries: 1, TimeSpan.Zero);

        handledBy.Should().BeSameAs(frontend);
        task.Status.Should().Be(TaskStatus.Unassigned, "the Drake holding it reset the task for another agent type");
    }

    [Fact]
    public async Task When_no_Drake_holds_the_task_nothing_is_routed()
    {
        var (backend, backendTasks) = NewDrake("backend");
        var (frontend, _) = NewDrake("frontend");
        var held = backendTasks.AddTask("[api-1] Build the store");
        backendTasks.UpdateTask(held, TaskStatus.Working, "csharp");
        var stray = new TaskRecord { Task = "[x-1] A task no Drake holds" };
        var calls = 0;

        var handledBy = await ReasoningMonitorService.RouteToHoldingDrakeAsync(WrongAgent(stray),
            () => { calls++; return new[] { backend, frontend }; }, NullLogger.Instance, maxRetries: 2, TimeSpan.Zero);

        handledBy.Should().BeNull();
        calls.Should().Be(2, "it looks again before giving up, as a Drake may not have loaded the task yet");
        held.Status.Should().Be(TaskStatus.Working, "no Drake was handed an escalation for a task it does not hold");
    }
}
