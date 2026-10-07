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
/// A run without the enhanced plan path (every ad-hoc <c>/kobold</c> run) publishes its tool calls, so clients and the run
/// registry see progress, not just start and end (TASK-106 / FIELD-020).
/// </summary>
public class PlanlessRunEventsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_planless_{Guid.NewGuid():N}");

    public PlanlessRunEventsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>First reply calls <c>list_files</c>, the second ends the run.</summary>
    private sealed class OneToolCallProvider : ILlmProvider
    {
        private int _calls;
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Interlocked.Increment(ref _calls) == 1
                ? new LlmResponse
                {
                    StopReason = "tool_use",
                    Content = new List<ContentBlock>
                    {
                        new() { Type = "tool_use", Id = "t1", Name = "list_files", Input = new Dictionary<string, object> { ["directory"] = "." } }
                    }
                }
                : new LlmResponse
                {
                    StopReason = "end_turn",
                    Content = new List<ContentBlock> { new() { Type = "text", Text = "done" } }
                });

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task A_planless_run_publishes_its_tool_calls_in_order()
    {
        var runId = Guid.NewGuid();
        var events = new KoboldRunEventSource();
        using var subscription = events.Subscribe(runId, out var reader);
        var agent = new CodingAgent(new OneToolCallProvider(), new AgentOptions { WorkingDirectory = _dir, Verbose = false });
        var kobold = new Kobold(agent, "coding");
        kobold.SetRunEventSource(events);
        kobold.AssignRunId(runId);
        kobold.AssignTask(Guid.NewGuid(), "list the files");

        await kobold.StartWorkingWithPlanAsync(planService: null, maxIterations: 4);

        var kinds = new List<string>();
        while (reader.TryRead(out var evt))
            kinds.Add(evt.Kind + (evt is ToolCallStartedEvent s ? ":" + s.ToolName : evt is ToolCallResultEvent r ? ":" + r.ToolName : ""));

        kinds.Should().Equal("run_started", "tool_call_started:list_files", "tool_call_result:list_files", "run_completed");
        agent.Tools.Should().NotContain(t => t is DraCode.KoboldLair.Agents.Tools.RunEventPublishingTool,
            "the agent gets its own tools back after the run");
    }
}
