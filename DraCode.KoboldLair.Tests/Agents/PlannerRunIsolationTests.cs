using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Orchestration.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Agents;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Agents;

/// <summary>
/// TASK-195: a plan the model submits reaches only the planner run that asked for it. A Drake's one planner serves its
/// Kobolds in parallel, and a revision can run beside a new plan; before, the result sat in a process-wide static, so
/// one run's clear erased another's plan and one run read back another's.
/// </summary>
public class PlannerRunIsolationTests
{
    /// <summary>
    /// Plans each run for the task its prompt names ("task-A" or "task-B"). Run A's closing turn waits until run B has
    /// submitted its plan, so A reads its result after B has cleared and written: the interleaving the static lost.
    /// </summary>
    private sealed class InterleavingProvider : ILlmProvider
    {
        public readonly TaskCompletionSource ASubmitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _bSubmitted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            var run = messages[0].GetText().Contains("task-A") ? "A" : "B";
            if (messages.Count == 1)
                return new LlmResponse
                {
                    StopReason = "tool_use",
                    Content =
                    [
                        new ContentBlock
                        {
                            Type = "tool_use", Id = $"plan-{run}", Name = "create_implementation_plan",
                            Input = new Dictionary<string, object>
                            {
                                ["steps"] = System.Text.Json.JsonDocument.Parse(
                                    $$"""[{"title":"Write {{run}}.cs","description":"The file task-{{run}} asks for","files_to_create":["{{run}}.cs"]}]""").RootElement.Clone()
                            }
                        }
                    ]
                };

            if (run == "A")
            {
                ASubmitted.TrySetResult();
                await _bSubmitted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            else
            {
                _bSubmitted.TrySetResult();
            }
            return new LlmResponse { StopReason = "end_turn", Content = [new ContentBlock { Type = "text", Text = "planned" }] };
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static KoboldPlannerAgent NewPlanner(ILlmProvider provider) =>
        new(provider, new AgentOptions { WorkingDirectory = Path.GetTempPath(), MaxIterations = 3 });

    private static IEnumerable<string> Titles(KoboldImplementationPlan plan) => plan.Steps.Select(s => s.Title);

    [Fact]
    public async Task Two_plans_made_at_once_by_one_planner_each_reach_their_own_run()
    {
        var provider = new InterleavingProvider();
        var planner = NewPlanner(provider);

        var a = planner.CreatePlanAsync("p", "task-A", "Implement task-A");
        await provider.ASubmitted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var b = planner.CreatePlanAsync("p", "task-B", "Implement task-B");

        Titles(await a).Should().Equal(["Write A.cs"], "run A must get the plan it asked for, not B's or a fallback");
        Titles(await b).Should().Equal(["Write B.cs"]);
    }

    [Fact]
    public async Task A_revision_beside_a_new_plan_gets_its_own_revised_plan()
    {
        var provider = new InterleavingProvider();
        var planner = NewPlanner(provider);
        var existing = new KoboldImplementationPlan
        {
            ProjectId = "p", TaskId = "task-A", TaskDescription = "Implement task-A",
            Steps = [new ImplementationStep { Index = 1, Title = "Old approach", Status = StepStatus.Failed }]
        };

        var a = planner.RevisePlanAsync(existing, [],
            new EscalationAlert { Type = EscalationType.WrongApproach, Summary = "wrong approach", TaskId = "task-A" });
        await provider.ASubmitted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var b = planner.CreatePlanAsync("p", "task-B", "Implement task-B");

        Titles(await a).Should().Equal(["Write A.cs"], "the revision must replace the failed step with its own plan");
        Titles(await b).Should().Equal(["Write B.cs"]);
    }
}
