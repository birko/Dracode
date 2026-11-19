using Birko.AI.Orchestration.Models;
using Birko.AI;
using Birko.AI.Agents;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
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

    // --- answers on resume (TASK-022) -------------------------------------------------------------

    private const string AnswerProject = "proj-answers";

    /// <summary>A real plan service on the temp dir, so an asking run leaves a checkpoint the next run resumes.</summary>
    private KoboldPlanService PlanService()
    {
        var projects = new ProjectRepository(_dir);
        if (projects.GetById(AnswerProject) == null)
            projects.Add(new Project { Id = AnswerProject, Name = "answers", OwnerId = "o", Paths = { Output = _dir } });
        return new KoboldPlanService(_dir, projectRepository: projects, debounceIntervalMs: 0);
    }

    private (Kobold Kobold, TouchTool Touch) PlannedKobold(ILlmProvider provider, KoboldImplementationPlan plan, Guid taskId,
        params HumanAnswer[] answers)
    {
        var agent = new CodingAgent(provider, new AgentOptions { WorkingDirectory = _dir, Verbose = false });
        var touch = new TouchTool();
        agent.AddTool(touch);
        var kobold = new Kobold(agent, "coding");
        kobold.AssignTask(taskId, "work the plan", AnswerProject);
        kobold.ImplementationPlan = plan;
        kobold.HumanAnswers = answers;
        var asked = 0;
        kobold.OnAskHuman = (_, _) => Task.FromResult(new AskHumanOutcome(true, $"dec-{++asked}", null));
        return (kobold, touch);
    }

    private static async Task<(KoboldImplementationPlan Plan, Guid TaskId)> SavedPlan(KoboldPlanService plans)
    {
        var taskId = Guid.NewGuid();
        var plan = new KoboldImplementationPlan
        {
            ProjectId = AnswerProject, TaskId = taskId.ToString(), TaskDescription = "work the plan",
            Steps = [Step(1, "never-1.txt"), Step(2, "never-2.txt")]
        };
        await plans.SavePlanAsync(plan);
        return (plan, taskId);
    }

    private static LlmResponse Ask(string id, string question, params string[] options) => new()
    {
        StopReason = "tool_use",
        Content = new List<ContentBlock>
        {
            new() { Type = "tool_use", Id = id, Name = "ask_human",
                Input = new Dictionary<string, object> { ["question"] = question, ["options"] = options } }
        }
    };

    private static HumanAnswer Answered(string id, string question, string answer) =>
        new(id, PendingDecisionSource.AskHuman, question, null, null, answer, false, null);

    private static string Json(IEnumerable<Message> messages) => System.Text.Json.JsonSerializer.Serialize(messages);

    private static int Count(string text, string part) =>
        (text.Length - text.Replace(part, "").Length) / part.Length;

    /// <summary>Run 1 asks on step 1 and stops, leaving its checkpoint.</summary>
    private async Task<(KoboldImplementationPlan Plan, Guid TaskId)> AskedOnTheFirstStep(KoboldPlanService plans)
    {
        var (plan, taskId) = await SavedPlan(plans);
        var asking = new ScriptedProvider((call, _) => Task.FromResult(
            call == 1 ? Ask("ask-1", "Which database?", "sqlite", "postgres") : EndTurn));
        var (first, _) = PlannedKobold(asking, plan, taskId);
        await first.StartWorkingWithPlanEnhancedAsync(plans, maxIterations: 10);
        asking.Requests.Should().HaveCount(1);
        plan.CurrentStepIndex.Should().Be(0, "the question was asked on the first step");
        return (plan, taskId);
    }

    [Fact]
    public async Task A_Kobold_that_asked_resumes_its_conversation_with_the_answer_and_acts_on_the_chosen_option()
    {
        var plans = PlanService();
        var (plan, taskId) = await AskedOnTheFirstStep(plans);

        ScriptedProvider? resumed = null;
        resumed = new ScriptedProvider((call, _) => Task.FromResult(call switch
        {
            1 => Touch(Json(resumed!.Requests[0]).Contains("Their answer: postgres") ? "postgres.txt" : "sqlite.txt"),
            _ => EndTurn
        }));
        var (second, _) = PlannedKobold(resumed, plan, taskId, Answered("dec-1", "Which database?", "postgres"));
        await second.StartWorkingWithPlanEnhancedAsync(plans, maxIterations: 10);

        var request = resumed.Requests[0];
        Count(Json(request), "ask-1").Should().Be(2, "the restored question and its one result, no second result");
        var last = request[^1];
        last.Role.Should().Be("user");
        var text = last.Content.Should().BeOfType<string>().Subject;
        text.Should().StartWith(HumanAnswer.TagFor("dec-1")).And.Contain("A person answered").And.Contain("postgres")
            .And.Contain("Your task was paused for a person's decision").And.NotContain("server was restarted");
        File.Exists(Path.Combine(_dir, "postgres.txt")).Should().BeTrue("the next action reflects the chosen option");
        File.Exists(Path.Combine(_dir, "sqlite.txt")).Should().BeFalse();
    }

    [Fact]
    public async Task Without_an_answer_a_run_stopped_on_the_first_step_starts_fresh()
    {
        var plans = PlanService();
        var (plan, taskId) = await AskedOnTheFirstStep(plans);

        var rerun = new ScriptedProvider((_, _) => Task.FromResult(EndTurn));
        var (again, _) = PlannedKobold(rerun, plan, taskId);
        await again.StartWorkingWithPlanEnhancedAsync(plans, maxIterations: 1);

        rerun.Requests[0].Should().ContainSingle("only the first step resumes a conversation, and only for an answer");
    }

    [Fact]
    public async Task A_timed_out_question_resumes_with_no_answer_rather_than_one()
    {
        var plans = PlanService();
        var (plan, taskId) = await AskedOnTheFirstStep(plans);

        var resumed = new ScriptedProvider((_, _) => Task.FromResult(EndTurn));
        var (second, _) = PlannedKobold(resumed, plan, taskId,
            new HumanAnswer("dec-1", PendingDecisionSource.AskHuman, "Which database?", null, null, null, true, null));
        await second.StartWorkingWithPlanEnhancedAsync(plans, maxIterations: 1);

        var text = (string)resumed.Requests[0][^1].Content!;
        text.Should().Contain(HumanAnswer.TagFor("dec-1")).And.Contain("no answer, timed out").And.NotContain("Their answer");
    }

    [Fact]
    public async Task An_answer_already_in_the_restored_conversation_is_not_delivered_again()
    {
        var plans = PlanService();
        var (plan, taskId) = await AskedOnTheFirstStep(plans);
        var first = Answered("dec-1", "Which database?", "postgres");

        // Run 2 hears dec-1, then asks a second question and stops, saving a checkpoint that holds dec-1's message
        var asksAgain = new ScriptedProvider((call, _) => Task.FromResult(call == 1 ? Ask("ask-2", "Which port?") : EndTurn));
        var (second, _) = PlannedKobold(asksAgain, plan, taskId, first);
        await second.StartWorkingWithPlanEnhancedAsync(plans, maxIterations: 10);
        asksAgain.Requests.Should().HaveCount(1);

        var resumed = new ScriptedProvider((_, _) => Task.FromResult(EndTurn));
        var (third, _) = PlannedKobold(resumed, plan, taskId, first, Answered("dec-2", "Which port?", "5432"));
        await third.StartWorkingWithPlanEnhancedAsync(plans, maxIterations: 1);

        var sent = Json(resumed.Requests[0]);
        Count(sent, "decision dec-1]").Should().Be(1, "dec-1 is in the restored conversation already");
        Count(sent, "decision dec-2]").Should().Be(1);
        ((string)resumed.Requests[0][^1].Content!).Should().NotContain("decision dec-1]");
    }

    [Fact]
    public async Task On_the_basic_plan_path_the_first_prompt_carries_every_answer()
    {
        var provider = new ScriptedProvider((_, _) => Task.FromResult(EndTurn));
        var (kobold, _, _) = CreateKobold(provider, Step(1, "never-1.txt"));
        kobold.HumanAnswers = [Answered("dec-1", "Which database?", "postgres")];

        await kobold.StartWorkingWithPlanAsync(planService: null, maxIterations: 1);

        Json(provider.Requests[0]).Should().Contain("decision dec-1]").And.Contain("Their answer: postgres");
    }
}
