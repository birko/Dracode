using Birko.AI.Orchestration.Models;
using Birko.AI;
using Birko.AI.Agents;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// TASK-125: the enhanced plan path runs on Birko's <see cref="Agent"/> with hooks instead of a copy of its loop — step
/// detection, the reflection reminder and tool-call events come from the hooks, and the run's cancellation token reaches
/// the provider and the tools.
/// </summary>
public class KoboldRunHooksTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_runhooks_{Guid.NewGuid():N}");

    public KoboldRunHooksTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>Answers each model call from a script; records what each call was sent.</summary>
    private sealed class ScriptedProvider(Func<int, CancellationToken, Task<LlmResponse>> script) : ILlmProvider
    {
        public List<List<Message>> Requests { get; } = new();
        public List<CancellationToken> Tokens { get; } = new();
        public List<List<string>> OfferedTools { get; } = new();
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            Tokens.Add(cancellationToken);
            OfferedTools.Add(tools.Select(t => t.Name).ToList());
            return script(Requests.Count, cancellationToken);
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>Writes <c>path</c> when asked and records the token it was run with.</summary>
    private sealed class TouchTool : Tool
    {
        public List<CancellationToken> Tokens { get; } = new();
        public override string Name => "touch";
        public override string Description => "test tool";
        public override object? InputSchema => new { type = "object" };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input,
            CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            if (input.TryGetValue("path", out var path))
                await File.WriteAllTextAsync(Path.Combine(workingDirectory, path.ToString()!), "x", cancellationToken);
            return "ok";
        }
    }

    private static LlmResponse ToolUse(string name, Dictionary<string, object> input) => new()
    {
        StopReason = "tool_use",
        Content = new List<ContentBlock> { new() { Type = "tool_use", Id = Guid.NewGuid().ToString("N"), Name = name, Input = input } }
    };

    private static LlmResponse CompleteStep(int index) =>
        ToolUse("update_plan_step", new Dictionary<string, object> { ["step_index"] = index, ["status"] = "completed" });

    private static LlmResponse Touch(string? path = null) =>
        ToolUse("touch", path == null ? new Dictionary<string, object>() : new Dictionary<string, object> { ["path"] = path });

    private static readonly LlmResponse EndTurn = new()
    {
        StopReason = "end_turn",
        Content = new List<ContentBlock> { new() { Type = "text", Text = "done" } }
    };

    // Steps expect files nobody writes, so the auto-advance validation never completes them by accident.
    private static ImplementationStep Step(int index, string file) =>
        new() { Index = index, Title = $"step {index}", FilesToCreate = [file] };

    private (Kobold Kobold, CodingAgent Agent, TouchTool Touch) CreateKobold(ILlmProvider provider, params ImplementationStep[] steps)
    {
        var agent = new CodingAgent(provider, new AgentOptions { WorkingDirectory = _dir, Verbose = false });
        var touch = new TouchTool();
        agent.AddTool(touch);
        var kobold = new Kobold(agent, "coding");
        kobold.AssignTask(Guid.NewGuid(), "work the plan");
        kobold.ImplementationPlan = new KoboldImplementationPlan { Steps = steps.ToList() };
        return (kobold, agent, touch);
    }

    [Fact]
    public async Task The_run_stops_without_another_model_call_once_update_plan_step_finishes_the_last_step()
    {
        var provider = new ScriptedProvider((call, _) => Task.FromResult(call switch
        {
            1 => CompleteStep(1),
            2 => CompleteStep(2),
            _ => EndTurn
        }));
        var (kobold, agent, _) = CreateKobold(provider, Step(1, "never-1.txt"), Step(2, "never-2.txt"));
        var runId = Guid.NewGuid();
        var events = new KoboldRunEventSource();
        using var subscription = events.Subscribe(runId, out var reader);
        kobold.SetRunEventSource(events);
        kobold.AssignRunId(runId);

        await kobold.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 10);

        provider.Requests.Should().HaveCount(2, "step detection ends the run when the plan is done");
        kobold.Status.Should().Be(KoboldStatus.Done);
        kobold.ImplementationPlan!.Steps.Should().OnlyContain(s => s.Status == StepStatus.Completed);
        agent.Hooks.Should().BeNull();

        var kinds = new List<string>();
        while (reader.TryRead(out var evt))
            kinds.Add(evt.Kind + (evt is ToolCallStartedEvent s ? ":" + s.ToolName : evt is ToolCallResultEvent r ? ":" + r.ToolName : ""));
        kinds.Should().ContainInOrder(
            "run_started",
            "tool_call_started:update_plan_step", "tool_call_result:update_plan_step",
            "tool_call_started:update_plan_step", "tool_call_result:update_plan_step",
            "run_completed");
    }

    [Fact]
    public async Task A_step_whose_files_validate_is_auto_advanced_when_the_model_does_not_mark_it()
    {
        var provider = new ScriptedProvider((call, _) => Task.FromResult(call == 1 ? Touch("made.txt") : EndTurn));
        var (kobold, _, _) = CreateKobold(provider, Step(1, "made.txt"));

        await kobold.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 10);

        provider.Requests.Should().HaveCount(1, "the auto-advanced step was the last one");
        var step = kobold.ImplementationPlan!.Steps[0];
        step.Status.Should().Be(StepStatus.Completed);
        step.Metrics.AutoCompleted.Should().BeTrue();
        kobold.Status.Should().Be(KoboldStatus.Done);
    }

    [Fact]
    public async Task The_reflection_reminder_is_sent_every_CheckpointInterval_iterations_of_one_step()
    {
        var provider = new ScriptedProvider((call, _) => Task.FromResult(call < 4 ? Touch() : EndTurn));
        var (kobold, _, _) = CreateKobold(provider, Step(1, "never.txt"));
        kobold.CheckpointInterval = 3;

        await kobold.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 10);

        static bool EndsWithReminder(List<Message> request) =>
            request[^1].Role == "user" && request[^1].Content is string text && text.Contains("REFLECTION REQUIRED");
        provider.Requests.Select(EndsWithReminder).Should().Equal(false, false, true, false);
    }

    [Fact]
    public async Task The_run_cancellation_token_reaches_the_provider_and_the_tools()
    {
        using var cts = new CancellationTokenSource();
        var provider = new ScriptedProvider((call, _) => Task.FromResult(call == 1 ? Touch() : EndTurn));
        var (kobold, _, touch) = CreateKobold(provider, Step(1, "never.txt"));

        await kobold.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 10, cancellationToken: cts.Token);

        // The run passes its own token, linked to the caller's (it can stop itself after a question, TASK-021);
        // the caller's cancellation reaching it is Cancelling_during_a_model_call_ends_the_run_gracefully_...
        var runToken = provider.Tokens.Should().NotBeEmpty().And.Subject.First();
        runToken.CanBeCanceled.Should().BeTrue();
        provider.Tokens.Should().OnlyContain(t => t == runToken);
        touch.Tokens.Should().ContainSingle().Which.Should().Be(runToken);
    }

    [Fact]
    public async Task Cancelling_during_a_model_call_ends_the_run_gracefully_and_keeps_the_Kobold_Working()
    {
        using var cts = new CancellationTokenSource();
        var provider = new ScriptedProvider(async (call, token) =>
        {
            if (call == 1) return Touch();
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, token); // a model call in flight when shutdown begins
            return EndTurn;
        });
        var (kobold, _, _) = CreateKobold(provider, Step(1, "never.txt"));

        var messages = await kobold.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 10, cancellationToken: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(10));

        kobold.Status.Should().Be(KoboldStatus.Working, "a shutdown is not a failure; the plan resumes on restart");
        messages.Should().HaveCount(3, "prompt, tool_use, tool_result: the conversation up to the cancelled call");
        messages[^1].Role.Should().Be("user", "a resumable checkpoint ends in a user turn");
    }

    [Fact]
    public async Task Cancelling_a_planless_run_keeps_the_Kobold_Working_rather_than_Done()
    {
        using var cts = new CancellationTokenSource();
        var provider = new ScriptedProvider(async (_, token) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return EndTurn;
        });
        var agent = new CodingAgent(provider, new AgentOptions { WorkingDirectory = _dir, Verbose = false });
        var kobold = new Kobold(agent, "coding");
        kobold.AssignTask(Guid.NewGuid(), "no plan");

        await kobold.StartWorkingWithPlanAsync(planService: null, maxIterations: 4, cancellationToken: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(10));

        kobold.Status.Should().Be(KoboldStatus.Working);
        agent.Hooks.Should().BeNull();
    }

    // --- ask_human (TASK-021) ---------------------------------------------------------------------

    private static LlmResponse AskThenTouch() => new()
    {
        StopReason = "tool_use",
        Content = new List<ContentBlock>
        {
            new() { Type = "tool_use", Id = "ask-1", Name = "ask_human",
                Input = new Dictionary<string, object> { ["question"] = "Which database?" } },
            new() { Type = "tool_use", Id = "touch-1", Name = "touch",
                Input = new Dictionary<string, object> { ["path"] = "after-ask.txt" } }
        }
    };

    [Fact]
    public async Task Asking_ends_the_run_after_the_turn_with_the_question_and_its_result_saved()
    {
        var provider = new ScriptedProvider((call, _) => Task.FromResult(call == 1 ? AskThenTouch() : EndTurn));
        var (kobold, _, touch) = CreateKobold(provider, Step(1, "never-1.txt"));
        kobold.OnAskHuman = (_, _) => Task.FromResult(new AskHumanOutcome(true, "dec-1", null));

        // No cancellation is passed in: the run stops itself, so a later run of the task cannot be cancelled by this one
        var conversation = await kobold.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 10);

        provider.Requests.Should().HaveCount(1, "the run stops before the next model call");
        touch.Tokens.Should().HaveCount(1, "a later call in the same turn still runs");
        var last = System.Text.Json.JsonSerializer.Serialize(conversation[^1]);
        last.Should().Contain("ask-1").And.Contain("dec-1", "the saved conversation ends with the question's result");
        kobold.Status.Should().Be(KoboldStatus.Working);
        kobold.ImplementationPlan!.Steps[0].Status.Should().NotBe(StepStatus.Completed);
    }

    [Fact]
    public async Task On_the_basic_plan_path_asking_also_ends_the_run_after_the_turn()
    {
        var provider = new ScriptedProvider((call, _) => Task.FromResult(call == 1 ? AskThenTouch() : EndTurn));
        var (kobold, _, touch) = CreateKobold(provider, Step(1, "never-1.txt"));
        kobold.OnAskHuman = (_, _) => Task.FromResult(new AskHumanOutcome(true, "dec-1", null));

        await kobold.StartWorkingWithPlanAsync(planService: null, maxIterations: 10);

        provider.Requests.Should().HaveCount(1);
        touch.Tokens.Should().HaveCount(1);
        kobold.Status.Should().Be(KoboldStatus.Working);
    }

    [Fact]
    public async Task A_refused_question_does_not_end_the_run()
    {
        var provider = new ScriptedProvider((call, _) => Task.FromResult(call == 1 ? AskThenTouch() : EndTurn));
        var (kobold, _, _) = CreateKobold(provider, Step(1, "never-1.txt"));
        kobold.OnAskHuman = (_, _) => Task.FromResult(new AskHumanOutcome(false, null, "limit reached"));

        await kobold.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 10);

        provider.Requests.Should().HaveCount(2, "the model hears 'not asked' and goes on");
    }

    [Fact]
    public async Task A_run_without_a_plan_is_not_offered_ask_human()
    {
        var provider = new ScriptedProvider((_, _) => Task.FromResult(EndTurn));
        var (kobold, _, _) = CreateKobold(provider);
        kobold.ImplementationPlan = null;
        kobold.OnAskHuman = (_, _) => Task.FromResult(new AskHumanOutcome(true, "dec-1", null));

        await kobold.StartWorkingWithPlanAsync(planService: null, maxIterations: 2);

        provider.OfferedTools.Should().OnlyContain(names => !names.Contains("ask_human"));
    }

    [Fact]
    public async Task A_plan_run_is_offered_ask_human_only_when_Drake_wired_it()
    {
        var withAsk = new ScriptedProvider((_, _) => Task.FromResult(EndTurn));
        var (asking, _, _) = CreateKobold(withAsk, Step(1, "never-1.txt"));
        asking.OnAskHuman = (_, _) => Task.FromResult(new AskHumanOutcome(true, "dec-1", null));
        await asking.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 1);

        var without = new ScriptedProvider((_, _) => Task.FromResult(EndTurn));
        var (silent, _, _) = CreateKobold(without, Step(1, "never-1.txt"));
        await silent.StartWorkingWithPlanEnhancedAsync(planService: null, maxIterations: 1);

        withAsk.OfferedTools[0].Should().Contain("ask_human");
        without.OfferedTools[0].Should().NotContain("ask_human");
    }
}
