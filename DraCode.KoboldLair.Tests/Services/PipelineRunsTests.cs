using Birko.AI;
using Birko.AI.Agents;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The pipeline's own Kobold runs reach the run list (TASK-184): a run nobody registered is registered by
/// <see cref="RunRegistry.TrackUnregisteredRuns"/> when it starts — mode <c>pipeline</c>, its project and task from the
/// run's events, the project's owner — and folds to its end like any run, on the plan path and the no-plan path alike
/// (the latter now publishes its start and end). A transport's run, registered first, is left as it was.
/// </summary>
public class PipelineRunsTests : IDisposable
{
    private const string Project = "p-1";
    private const string Owner = "github:4242";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_pipeline_runs_{Guid.NewGuid():N}");

    public PipelineRunsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>Ends the run at once with a text reply.</summary>
    private sealed class DoneProvider : ILlmProvider
    {
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmResponse { StopReason = "end_turn", Content = new List<ContentBlock> { new() { Type = "text", Text = "done" } } });

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static async Task WaitUntil(Func<bool> predicate, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!predicate() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    private (KoboldRunEventSource Events, RunRegistry Registry) Tracked()
    {
        var events = new KoboldRunEventSource();
        var registry = new RunRegistry(events);
        registry.TrackUnregisteredRuns(projectId => projectId == Project ? Owner : null);
        return (events, registry);
    }

    private Kobold PipelineKobold(KoboldRunEventSource events, Guid taskId)
    {
        var agent = new CodingAgent(new DoneProvider(), new AgentOptions { WorkingDirectory = _dir, Verbose = false });
        var kobold = new Kobold(agent, "coding");
        kobold.SetRunEventSource(events);
        kobold.AssignTask(taskId, "Write the greet module", Project);
        return kobold;
    }

    [Fact]
    public async Task A_planless_pipeline_run_is_registered_with_its_project_task_and_owner_and_ends_completed()
    {
        var (events, registry) = Tracked();
        var taskId = Guid.NewGuid();
        var kobold = PipelineKobold(events, taskId);

        await kobold.StartWorkingAsync(maxIterations: 2);

        var run = registry.Get(kobold.RunId);
        run.Should().NotBeNull("the no-plan path now starts a run, and the registry picks it up");
        run!.Mode.Should().Be(RunRegistry.PipelineMode);
        run.Owner.Should().Be(Owner);
        run.ProjectId.Should().Be(Project);
        run.TaskId.Should().Be(taskId.ToString());
        await WaitUntil(() => registry.Get(kobold.RunId)!.IsTerminal);
        registry.Get(kobold.RunId)!.State.Should().Be(RunRegistry.RunState.Completed);
        registry.ListByOwner(Owner).Select(r => r.RunId).Should().Equal(kobold.RunId);
    }

    [Fact]
    public async Task A_plan_path_pipeline_run_is_registered_too()
    {
        var (events, registry) = Tracked();
        var kobold = PipelineKobold(events, Guid.NewGuid());

        await kobold.StartWorkingWithPlanAsync(planService: null, maxIterations: 2);

        await WaitUntil(() => registry.Get(kobold.RunId)?.IsTerminal == true);
        var run = registry.Get(kobold.RunId)!;
        run.Mode.Should().Be(RunRegistry.PipelineMode);
        run.State.Should().Be(RunRegistry.RunState.Completed);
    }

    [Fact]
    public async Task A_transports_run_registered_first_keeps_its_own_mode_and_owner()
    {
        var (events, registry) = Tracked();
        var runId = Guid.NewGuid();
        registry.Register(runId, "cli-user", "adhoc", prompt: "Rename the README");
        var kobold = PipelineKobold(events, Guid.NewGuid());
        kobold.AssignRunId(runId);

        await kobold.StartWorkingAsync(maxIterations: 2);

        await WaitUntil(() => registry.Get(runId)!.IsTerminal);
        var run = registry.Get(runId)!;
        run.Mode.Should().Be("adhoc");
        run.Owner.Should().Be("cli-user");
        run.Prompt.Should().Be("Rename the README");
        registry.List().Should().HaveCount(1);
    }

    [Fact]
    public async Task Without_tracking_an_unregistered_run_is_not_listed()
    {
        var events = new KoboldRunEventSource();
        var registry = new RunRegistry(events);
        var kobold = PipelineKobold(events, Guid.NewGuid());

        await kobold.StartWorkingAsync(maxIterations: 2);

        registry.List().Should().BeEmpty();
    }
}
