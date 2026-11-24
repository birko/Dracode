using Birko.AI;
using Birko.AI.Agents;
using Birko.AI.Models;
using Birko.AI.Orchestration.Models;
using Birko.AI.Orchestration.Services;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// TASK-171, FEATURE-028 D10: in parallel execution a question parks the task, and Drake starts no further batch or
/// step group while it waits; a stopped run ends the parallel execution quietly rather than as a failure.
/// </summary>
public class ParallelQuestionTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private const string ProjectId = "proj-parallel";

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_parallel_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();
        _kobolds = new KoboldFactory(projects, NullLoggerFactory.Instance, new KoboldLairConfiguration { ProjectsPath = _dir });
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private sealed class NoProvider : ILlmProvider
    {
        public string Name => "none";
        public Action<string, string>? MessageCallback { get; set; }
        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private (Drake drake, TaskRecord task) NewDrake(int maxParallelSteps)
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId, pendingDecisions: _decisions,
            planningConfig: new PlanningConfiguration { ExecutionMode = "parallel", MaxParallelSteps = maxParallelSteps });
        var task = tracker.AddTask("[api-1] Build the storage layer");
        tracker.UpdateTask(task, TaskStatus.Working, "coding");
        return (drake, task);
    }

    // Steps 1 and 2 touch different files (one group); step 3 modifies step 1's file (a later group)
    private static KoboldImplementationPlan Plan() => new()
    {
        ProjectId = ProjectId, TaskId = "t", TaskDescription = "Build the storage layer",
        Steps =
        [
            new ImplementationStep { Index = 1, Title = "one", FilesToCreate = ["a.txt"] },
            new ImplementationStep { Index = 2, Title = "two", FilesToCreate = ["b.txt"] },
            new ImplementationStep { Index = 3, Title = "three", FilesToModify = ["a.txt"] }
        ]
    };

    private static (List<Message>, Kobold)? Ran() =>
        (new List<Message>(), new Kobold(new CodingAgent(new NoProvider(), new AgentOptions()), "coding"));

    private static AskHumanQuestion Question() => new("Which database?", ["sqlite", "postgres"], false, null);

    [Fact]
    public async Task After_a_batch_asks_no_further_group_starts_and_nothing_fails()
    {
        var (drake, task) = NewDrake(maxParallelSteps: 3); // steps 1 and 2 run as two batches at once
        var ran = new List<int>();
        drake.StepBatchRunner = async (t, batch, _) =>
        {
            lock (ran) ran.AddRange(batch.Select(s => s.Index));
            if (batch.Any(s => s.Index == 1))
                (await drake.AskHumanAsync(t, Question())).Parked.Should().BeTrue();
            return Ran();
        };

        var run = () => drake.ExecuteTaskParallelAsync(task, Plan(), new StepDependencyAnalyzer(), "coding", 10, null, null,
            CancellationToken.None);

        await run.Should().NotThrowAsync();
        ran.Should().BeEquivalentTo([1, 2], "the group in flight finishes; the next group never starts for a task that waits on a person");
        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
    }

    [Fact]
    public async Task A_run_cancelled_between_batches_ends_without_throwing()
    {
        var (drake, task) = NewDrake(maxParallelSteps: 1); // steps 1 and 2 in one batch
        using var cancel = new CancellationTokenSource();
        var ran = new List<int>();
        drake.StepBatchRunner = (_, batch, _) =>
        {
            ran.AddRange(batch.Select(s => s.Index));
            cancel.Cancel(); // an escalation parked the task and cancelled its run
            return Task.FromResult(Ran());
        };

        var run = () => drake.ExecuteTaskParallelAsync(task, Plan(), new StepDependencyAnalyzer(), "coding", 10, null, null,
            cancel.Token);

        await run.Should().NotThrowAsync("a stopped run is not a failure");
        ran.Should().Equal([1, 2], "no group starts after the run was stopped");
    }
}
