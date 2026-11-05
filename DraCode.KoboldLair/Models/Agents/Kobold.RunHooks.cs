using System.Text.Json;
using Birko.AI.Agents;
using Birko.AI.Models;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Models.Agents
{
    public partial class Kobold
    {
        /// <summary>
        /// Watches one Kobold run through Birko's <see cref="IAgentHooks"/> (TASK-125). Every run publishes its tool calls as
        /// run events (TASK-106). A run with step detection also tracks the plan's current step, reminds the model to
        /// <c>reflect</c> every <see cref="CheckpointInterval"/> iterations of one step, auto-advances a step whose files
        /// validate, saves a conversation checkpoint after each finished step, and stops the run once every step is done.
        /// </summary>
        private sealed class RunHooks : AgentHooks
        {
            private readonly Kobold _kobold;
            private readonly KoboldPlanService? _planService;
            private readonly bool _detectSteps;

            private IReadOnlyList<Message>? _conversation;
            private int _consistentLength;
            private int _stepIterationCount;
            private bool _toolTurnPending;
            private bool _calledUpdatePlanStep;
            private bool _checkpointPending;

            public RunHooks(Kobold kobold, KoboldPlanService? planService, bool detectSteps)
            {
                _kobold = kobold;
                _planService = planService;
                _detectSteps = detectSteps && kobold.ImplementationPlan != null;
                CurrentStepIndex = kobold.ImplementationPlan?.CurrentStepIndex ?? 0;
            }

            /// <summary>The plan step the run is on, as the hooks last saw it.</summary>
            public int CurrentStepIndex { get; set; }

            private KoboldImplementationPlan Plan => _kobold.ImplementationPlan!;

            private bool CanSaveCheckpoint => _planService != null && !string.IsNullOrEmpty(_kobold.ProjectId);

            /// <summary>
            /// The conversation up to the start of the last iteration, before any message that iteration added — always
            /// ending in a user turn, so it can be checkpointed and resumed even when the run was cancelled part-way through
            /// a model call or a tool batch.
            /// </summary>
            public List<Message> ConsistentConversation() =>
                _conversation?.Take(_consistentLength).ToList() ?? new List<Message>();

            public override async ValueTask OnIterationStartingAsync(AgentIterationContext context)
            {
                _conversation = context.Conversation;

                if (_detectSteps && await FinishToolTurnAsync(context.Conversation))
                {
                    context.Stop("all plan steps complete");
                    return;
                }

                // Recorded before this iteration adds anything; Agent throws on cancellation right after this hook.
                _consistentLength = context.Conversation.Count;

                if (!_detectSteps) return;

                _stepIterationCount++;
                var interval = _kobold.CheckpointInterval;
                if (interval > 0 && _stepIterationCount > 1 && _stepIterationCount % interval == 0
                    && CurrentStepIndex < Plan.Steps.Count)
                {
                    context.AppendUserMessage(ReflectionReminder(context.Iteration, Plan.Steps[CurrentStepIndex]));
                    _kobold._logger?.LogDebug("Checkpoint reminder injected at iteration {Iteration}", context.Iteration);
                }
            }

            public override ValueTask OnToolCallStartingAsync(ToolCallStarted call, CancellationToken cancellationToken)
            {
                _kobold.PublishRunEvent(new ToolCallStartedEvent
                {
                    ToolName = call.ToolName,
                    InputJson = JsonSerializer.Serialize(call.Input)
                });
                return ValueTask.CompletedTask;
            }

            public override ValueTask OnToolCallFinishedAsync(ToolCallFinished call, CancellationToken cancellationToken)
            {
                var result = call.Result ?? $"Error: {call.Exception?.Message}";
                var preview = result.Length > 500 ? string.Concat(result.AsSpan(0, 500), "...") : result;
                _kobold.PublishRunEvent(new ToolCallResultEvent { ToolName = call.ToolName, ResultPreview = preview });

                if (!_detectSteps) return ValueTask.CompletedTask;

                _toolTurnPending = true;
                if (call.ToolName == "update_plan_step")
                {
                    _calledUpdatePlanStep = true;
                    TrackStepChange();
                }
                return ValueTask.CompletedTask;
            }

            /// <summary>
            /// Finishes the bookkeeping of the run's last tool turn, which no further iteration start picks up (the run hit
            /// its iteration budget). Returns true when every plan step is done.
            /// </summary>
            public Task<bool> CompleteAsync(IReadOnlyList<Message> conversation) =>
                _detectSteps ? FinishToolTurnAsync(conversation) : Task.FromResult(false);

            // The model moved the plan with update_plan_step: close the step it was on and start the next one.
            private void TrackStepChange()
            {
                var newStepIndex = Plan.CurrentStepIndex;
                if (newStepIndex == CurrentStepIndex || CurrentStepIndex >= Plan.Steps.Count)
                    return;

                var completedStep = Plan.Steps[CurrentStepIndex];
                completedStep.Metrics.IterationsUsed = _stepIterationCount;
                _kobold._logger?.LogInformation(
                    "✅ Step {StepIndex} completed: {StepTitle} ({IterationCount} iterations, status: {Status})",
                    completedStep.Index, completedStep.Title, _stepIterationCount, completedStep.Status);

                _stepIterationCount = 0;
                CurrentStepIndex = newStepIndex;
                _checkpointPending = true;
                StartCurrentStep();
            }

            // Runs once the tool turn's results are in the conversation, so a checkpoint saved here ends in a user turn.
            private async Task<bool> FinishToolTurnAsync(IReadOnlyList<Message> conversation)
            {
                if (!_toolTurnPending) return false;
                _toolTurnPending = false;

                var calledUpdatePlanStep = _calledUpdatePlanStep;
                _calledUpdatePlanStep = false;

                if (!calledUpdatePlanStep && CurrentStepIndex < Plan.Steps.Count)
                    await AutoAdvanceIfValidatedAsync();

                if (_checkpointPending)
                {
                    _checkpointPending = false;
                    if (CanSaveCheckpoint)
                        await _planService!.SaveConversationCheckpointAsync(Plan, conversation.ToList());
                }

                if (CurrentStepIndex < Plan.Steps.Count) return false;

                _kobold._logger?.LogInformation("✅ All {TotalSteps} steps completed for Kobold {KoboldId}",
                    Plan.Steps.Count, _kobold.Id.ToString()[..8]);
                return true;
            }

            // The model did not mark the step, but its files validate: complete it on the model's behalf.
            private async Task AutoAdvanceIfValidatedAsync()
            {
                var currentStep = Plan.Steps[CurrentStepIndex];
                if (currentStep.Status != StepStatus.Pending && currentStep.Status != StepStatus.InProgress)
                    return;

                var (isComplete, _) = await _kobold.ValidateStepCompletionAsync(currentStep);
                if (!isComplete) return;

                _kobold._logger?.LogWarning(
                    "⚠️ AUTO-ADVANCE: Step {StepIndex} validated as complete but agent didn't mark it. Auto-advancing.\n" +
                    "  Step: {StepTitle}\n" +
                    "  Iterations on this step: {IterationCount}",
                    currentStep.Index, currentStep.Title, _stepIterationCount);

                currentStep.Complete("Auto-completed by validation (agent didn't mark explicitly)");
                currentStep.Metrics.IterationsUsed = _stepIterationCount;
                currentStep.Metrics.AutoCompleted = true;
                Plan.CurrentStepIndex++;
                Plan.AddLogEntry($"Auto-advanced from step {currentStep.Index} after validation passed");

                if (CanSaveCheckpoint)
                    await _planService!.SavePlanDebouncedAsync(Plan);

                _kobold._logger?.LogInformation("✅ Step {StepIndex} auto-completed: {StepTitle} ({IterationCount} iterations)",
                    currentStep.Index, currentStep.Title, _stepIterationCount);

                _stepIterationCount = 0;
                CurrentStepIndex = Plan.CurrentStepIndex;
                _checkpointPending = true;
                StartCurrentStep();
            }

            public void StartCurrentStep()
            {
                if (CurrentStepIndex >= Plan.Steps.Count) return;

                var step = Plan.Steps[CurrentStepIndex];
                step.Start();
                _kobold._logger?.LogInformation(
                    "🔷 Kobold {KoboldId} starting step {StepIndex}/{TotalSteps}: {StepTitle}\n" +
                    "  Files to create: {FilesToCreate}\n" +
                    "  Files to modify: {FilesToModify}",
                    _kobold.Id.ToString()[..8], step.Index, Plan.Steps.Count, step.Title,
                    step.FilesToCreate.Count > 0 ? string.Join(", ", step.FilesToCreate) : "none",
                    step.FilesToModify.Count > 0 ? string.Join(", ", step.FilesToModify) : "none");
            }

            private string ReflectionReminder(int iteration, ImplementationStep step) => $@"
---
**REFLECTION REQUIRED** (Iteration {iteration}, step iteration {_stepIterationCount})

You've spent {_stepIterationCount} iterations on step {CurrentStepIndex + 1}: '{step.Title}'

Call the `reflect` tool now with your assessment:
- progress_percent: your estimate of step completion (0-100)
- confidence_percent: confidence this approach will succeed (0-100)
- decision: ""continue"", ""pivot"" (change approach), or ""escalate"" (need upstream help)
- blockers: any obstacles (optional)
- adjustment: what you're changing if pivoting (optional)

If step is complete, call `update_plan_step` with status 'completed' instead.
---";
        }
    }
}
