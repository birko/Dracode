using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services.Checks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The acceptance check runs the registered checkers on every finished task after its own checks, cheapest first
/// (TASK-217, FEATURE-078 D8, D10, D30): every checker answers in one shape, a finding keeps the task from Done and
/// reopens it, a checker that cannot run goes to a person, and with none registered a task is judged as before.
/// </summary>
public class TaskCheckerTests : IAsyncLifetime
{
    private const string ProjectId = "proj-checks";
    private const string Task = "[api-1] Create the greeter (src/greet.py). Exports: greet function";
    private string _dir = null!;
    private string _workspace = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;

    /// <summary>A checker that answers what it is told to and records what it was asked.</summary>
    private sealed class StubChecker(string name, int cost, params TaskFinding[] findings) : ITaskChecker
    {
        public string Name { get; } = name;
        public int Cost { get; } = cost;
        public Exception? Throws { get; init; }
        public readonly List<TaskCheckInput> Asked = new();
        public readonly List<string> Log;

        public StubChecker(string name, int cost, List<string> log, params TaskFinding[] findings) : this(name, cost, findings) => Log = log;

        public Task<IReadOnlyList<TaskFinding>> CheckAsync(TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            Asked.Add(input);
            Log?.Add(Name);
            if (Throws != null)
                throw Throws;
            return System.Threading.Tasks.Task.FromResult<IReadOnlyList<TaskFinding>>(findings);
        }
    }

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_checks_{Guid.NewGuid():N}");
        _workspace = Path.Combine(_dir, "workspace");
        Directory.CreateDirectory(Path.Combine(_workspace, "src"));
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();
        _kobolds = new KoboldFactory(projects, NullLoggerFactory.Instance, new KoboldLairConfiguration
        {
            ProjectsPath = _dir,
            DefaultProvider = "ollama",
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = "ollama", Type = "ollama", DefaultModel = "m1", IsEnabled = true, RequiresApiKey = false,
                    CompatibleAgents = new() { "all" }, Configuration = new()
                }
            }
        });
    }

    public System.Threading.Tasks.Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private (Drake Drake, TaskTracker Tracker, TaskRecord Task) Finishing(params ITaskChecker[] checkers)
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama", projectId: ProjectId,
            pendingDecisions: _decisions,
            acceptance: new VerificationConfiguration { TaskAcceptanceEnabled = true, MaxAcceptanceReopens = 2 })
        {
            TaskChecks = new TaskCheckRunner(checkers)
        };
        var task = tracker.AddTask(Task);
        tracker.UpdateTask(task, TaskStatus.Working, "python");
        return (drake, tracker, task);
    }

    private void KeepsItsPromises() => File.WriteAllText(Path.Combine(_workspace, "src", "greet.py"), "def greet(name):\n    return name\n");

    private Kobold Finished(TaskRecord task)
    {
        var kobold = _kobolds.CreateKobold("ollama", "python", new AgentOptions { WorkingDirectory = _workspace }, projectId: ProjectId);
        kobold.AssignTask(Guid.Parse(task.Id), task.Task, ProjectId);
        typeof(Kobold).GetProperty(nameof(Kobold.Status))!.SetValue(kobold, KoboldStatus.Done);
        return kobold;
    }

    [Fact]
    public async System.Threading.Tasks.Task Checkers_run_cheapest_first_and_stop_at_the_first_that_finds_something()
    {
        var log = new List<string>();
        var runner = new TaskCheckRunner([
            new StubChecker("costly", 30, log),
            new StubChecker("cheap", 1, log),
            new StubChecker("middle", 10, log, new TaskFinding("middle", "missing", "No greeting for an empty name")),
        ]);

        var report = await runner.RunAsync(new TaskCheckInput(new TaskRecord { Task = Task }, _workspace, ProjectId));

        log.Should().Equal("cheap", "middle");
        report.Ran.Should().Equal("cheap", "middle");
        report.Findings.Should().ContainSingle().Which.ToString()
            .Should().Be("[middle] missing: No greeting for an empty name");
        report.Passed.Should().BeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task A_checker_that_throws_is_reported_as_unable_to_run_never_as_a_pass_and_the_rest_still_run()
    {
        var log = new List<string>();
        var runner = new TaskCheckRunner([
            new StubChecker("broken", 1, log) { Throws = new InvalidOperationException("the model is unreachable") },
            new StubChecker("after", 2, log),
        ]);

        var report = await runner.RunAsync(new TaskCheckInput(new TaskRecord { Task = Task }, _workspace, ProjectId));

        log.Should().Equal("broken", "after");
        report.CouldNotRun.Should().ContainSingle().Which.Should().Be("The broken check could not run: the model is unreachable");
        report.Passed.Should().BeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task A_finding_keeps_the_task_from_Done_and_reopens_it_with_the_finding()
    {
        var checker = new StubChecker("intent", 1,
            new TaskFinding("intent", "missing", "greet() ignores the empty name the task asks to handle", "\"handles an empty name\""));
        var (drake, _, task) = Finishing(checker);
        KeepsItsPromises();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Unassigned);
        task.AcceptanceAttempts.Should().Be(1, "one limit counts the acceptance check's reopens and the checkers'");
        task.Task.Should().Contain("Acceptance check failed (attempt 1)")
            .And.Contain("[intent] missing: greet() ignores the empty name");
        var asked = checker.Asked.Should().ContainSingle().Subject;
        asked.Task.Id.Should().Be(task.Id);
        asked.WorkspacePath.Should().Be(_workspace);
        asked.ProjectId.Should().Be(ProjectId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Every_checker_passing_lets_the_task_through_to_Done()
    {
        var log = new List<string>();
        var (drake, _, task) = Finishing(new StubChecker("intent", 1, log), new StubChecker("conventions", 2, log));
        KeepsItsPromises();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Done);
        log.Should().Equal("intent", "conventions");
    }

    [Fact]
    public async System.Threading.Tasks.Task A_checker_that_cannot_run_puts_the_task_before_a_person()
    {
        var (drake, _, task) = Finishing(new StubChecker("intent", 1) { Throws = new InvalidOperationException("no reply") });
        KeepsItsPromises();

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        var card = (await _decisions.GetUnresolvedForTaskAsync(task.Id))!;
        card.Source.Should().Be(PendingDecisionSource.Approval);
        PendingDecisionNotification.QuestionText(card).Should().Contain("The intent check could not run: no reply");
    }

    [Fact]
    public async System.Threading.Tasks.Task A_task_its_own_checks_already_failed_is_reopened_without_running_the_checkers()
    {
        var checker = new StubChecker("intent", 1);
        var (drake, _, task) = Finishing(checker);
        // No src/greet.py: the task's own promise (its target file) is broken

        await drake.SyncTaskFromKoboldAsync(Finished(task));

        task.Status.Should().Be(TaskStatus.Unassigned);
        checker.Asked.Should().BeEmpty("the cheapest check already failed it");
    }

    private sealed class ScriptedProvider(string reply) : ILlmProvider
    {
        public string? SystemPrompt;
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public System.Threading.Tasks.Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            SystemPrompt = systemPrompt;
            return System.Threading.Tasks.Task.FromResult(new LlmResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock> { new() { Type = "text", Text = reply } }
            });
        }

        public System.Threading.Tasks.Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SampleChecker(ILlmProvider provider) : CheckerAgent(provider)
    {
        public override string Name => "sample";
        public override int Cost => 5;
        protected override string Role => "the sample checker: does greet.py greet";
        protected override string Process => "1. Read greet.py.\n2. Stop: report what does not greet.";
        protected override string Rules => "1. Judge greeting only.";
        protected override System.Threading.Tasks.Task<string> GatherAsync(TaskCheckInput input, CancellationToken cancellationToken) =>
            System.Threading.Tasks.Task.FromResult("greet.py: def greet(name): return name");
    }

    [Fact]
    public async System.Threading.Tasks.Task A_checker_agent_follows_the_prompt_conventions_and_answers_in_the_shared_shape()
    {
        var model = new ScriptedProvider("""
            ```json
            { "findings": [ { "kind": "wrong", "text": "greet returns the name without a greeting", "evidence": "greet.py line 1" }, { "kind": "x" } ] }
            ```
            """);
        ITaskChecker checker = new SampleChecker(model);

        var findings = await checker.CheckAsync(new TaskCheckInput(new TaskRecord { Task = Task }, _workspace, ProjectId));

        findings.Should().ContainSingle("a finding with no text is no finding")
            .Which.Should().Be(new TaskFinding("sample", "wrong", "greet returns the name without a greeting", "greet.py line 1"));
        model.SystemPrompt.Should().Contain("the sample checker: does greet.py greet").And.Contain("## Process")
            .And.Contain("## Rules, in priority order").And.NotContain("\"findings\"", "the output schema goes in the message");
    }

    [Theory]
    [InlineData("""{ "findings": [] }""", true)]
    [InlineData("I think it is fine.", false)]
    [InlineData("""{ "verdict": "pass" }""", false)]
    public void A_reply_with_no_findings_list_cannot_count_as_a_pass(string reply, bool readable)
    {
        var read = () => CheckerAgent.ReadFindings("sample", reply);

        if (readable)
            read().Should().BeEmpty();
        else
            read.Should().Throw<InvalidOperationException>();
    }
}
