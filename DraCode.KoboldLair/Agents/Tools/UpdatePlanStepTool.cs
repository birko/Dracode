using System.Text.Json;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for updating implementation plan step status during execution.
    /// Kobolds use this tool to mark steps as completed, failed, or skipped.
    /// The plan is automatically saved after each update for resumability.
    /// </summary>
    public class UpdatePlanStepTool : Tool
    {
        private static readonly object _lock = new();
        private static KoboldImplementationPlan? _currentPlan;
        private static KoboldPlanService? _planService;

        public override string Name => "update_plan_step";

        public override string Description => @"Update the status of a step in your implementation plan.

IMPORTANT: You MUST call this tool after completing each step in your plan to track progress.
This ensures the plan is saved and can be resumed if interrupted.

Parameters:
- step_index (required): The step number (1-based) to update
- status (required): The new status - one of: ""completed"", ""failed"", ""skipped""
- output (optional): A brief summary of what was done or why it failed/was skipped

Returns: Confirmation of the update with current plan progress.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                step_index = new
                {
                    type = "integer",
                    description = "The step number (1-based) to update"
                },
                status = new
                {
                    type = "string",
                    @enum = new[] { "completed", "failed", "skipped" },
                    description = "The new status for the step"
                },
                output = new
                {
                    type = "string",
                    description = "Brief summary of what was done or reason for failure/skip"
                }
            },
            required = new[] { "step_index", "status" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> arguments)
        {
            try
            {
                // Get step index
                if (!arguments.TryGetValue("step_index", out var stepIndexObj))
                {
                    return "Error: 'step_index' parameter is required";
                }

                int stepIndex;
                if (stepIndexObj is JsonElement jsonElement)
                {
                    stepIndex = jsonElement.GetInt32();
                }
                else if (stepIndexObj is int intVal)
                {
                    stepIndex = intVal;
                }
                else if (stepIndexObj is long longVal)
                {
                    stepIndex = (int)longVal;
                }
                else if (!int.TryParse(stepIndexObj?.ToString(), out stepIndex))
                {
                    return "Error: 'step_index' must be an integer";
                }

                // Get status
                if (!arguments.TryGetValue("status", out var statusObj) || statusObj == null)
                {
                    return "Error: 'status' parameter is required";
                }

                var statusStr = statusObj is JsonElement statusElement
                    ? statusElement.GetString()
                    : statusObj.ToString();

                if (string.IsNullOrEmpty(statusStr))
                {
                    return "Error: 'status' parameter is required";
                }

                // Get optional output
                string? output = null;
                if (arguments.TryGetValue("output", out var outputObj) && outputObj != null)
                {
                    output = outputObj is JsonElement outputElement
                        ? outputElement.GetString()
                        : outputObj.ToString();
                }

                // Parse status
                StepStatus newStatus = statusStr.ToLowerInvariant() switch
                {
                    "completed" => StepStatus.Completed,
                    "failed" => StepStatus.Failed,
                    "skipped" => StepStatus.Skipped,
                    _ => throw new ArgumentException($"Invalid status '{statusStr}'. Must be 'completed', 'failed', or 'skipped'.")
                };

                // Update the plan
                lock (_lock)
                {
                    if (_currentPlan == null)
                    {
                        return "Error: No implementation plan is currently active. This tool can only be used when working with a plan.";
                    }

                    // Find the step (1-based index from user, 0-based in list)
                    var arrayIndex = stepIndex - 1;
                    if (arrayIndex < 0 || arrayIndex >= _currentPlan.Steps.Count)
                    {
                        return $"Error: Step {stepIndex} does not exist. Valid steps are 1 to {_currentPlan.Steps.Count}.";
                    }

                    var step = _currentPlan.Steps[arrayIndex];

                    // Update step status
                    switch (newStatus)
                    {
                        case StepStatus.Completed:
                            step.Complete(output);
                            _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) completed");
                            break;
                        case StepStatus.Failed:
                            step.Fail(output);
                            _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) failed: {output ?? "No reason provided"}");
                            break;
                        case StepStatus.Skipped:
                            step.Skip(output);
                            _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) skipped: {output ?? "No reason provided"}");
                            break;
                    }

                    // Advance current step index if this was the current step and it's done
                    if (arrayIndex == _currentPlan.CurrentStepIndex &&
                        (newStatus == StepStatus.Completed || newStatus == StepStatus.Skipped))
                    {
                        _currentPlan.AdvanceToNextStep();
                    }

                    // Save the plan asynchronously (fire and forget, but log errors)
                    if (_planService != null && !string.IsNullOrEmpty(_currentPlan.ProjectId))
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await _planService.SavePlanAsync(_currentPlan);
                            }
                            catch (Exception ex)
                            {
                                // Log but don't fail - the in-memory state is still updated
                                Console.Error.WriteLine($"Warning: Failed to save plan update: {ex.Message}");
                            }
                        });
                    }

                    // Build response
                    var completedCount = _currentPlan.CompletedStepsCount;
                    var totalSteps = _currentPlan.Steps.Count;
                    var progress = _currentPlan.ProgressPercentage;

                    var statusIcon = newStatus switch
                    {
                        StepStatus.Completed => "✅",
                        StepStatus.Failed => "❌",
                        StepStatus.Skipped => "⏭️",
                        _ => "?"
                    };

                    var nextStepInfo = "";
                    if (_currentPlan.HasMoreSteps)
                    {
                        var nextStep = _currentPlan.CurrentStep;
                        if (nextStep != null)
                        {
                            nextStepInfo = $"\n\nNext step: Step {nextStep.Index} - {nextStep.Title}";
                        }
                    }
                    else
                    {
                        nextStepInfo = "\n\nAll steps completed! Plan execution finished.";
                    }

                    return $@"{statusIcon} Step {stepIndex} marked as {statusStr}

Progress: {completedCount}/{totalSteps} steps ({progress}%){nextStepInfo}";
                }
            }
            catch (Exception ex)
            {
                return $"Error updating plan step: {ex.Message}";
            }
        }

        /// <summary>
        /// Registers the current plan and service for use by this tool.
        /// Call this before the Kobold starts working with a plan.
        /// </summary>
        public static void RegisterContext(KoboldImplementationPlan plan, KoboldPlanService? service)
        {
            lock (_lock)
            {
                _currentPlan = plan;
                _planService = service;
            }
        }

        /// <summary>
        /// Clears the current plan context.
        /// Call this when the Kobold finishes working.
        /// </summary>
        public static void ClearContext()
        {
            lock (_lock)
            {
                _currentPlan = null;
                _planService = null;
            }
        }

        /// <summary>
        /// Gets the current plan being tracked (for testing/debugging).
        /// </summary>
        public static KoboldImplementationPlan? GetCurrentPlan()
        {
            lock (_lock)
            {
                return _currentPlan;
            }
        }
    }
}
