using System.Text.Json;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Step dependency status for blocking downstream steps
    /// </summary>
    public enum StepBlockedStatus
    {
        Ready,
        BlockedByFailure
    }
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
        private static ILogger? _logger;

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
                    var dependentStepsSkipped = 0;

                    // Update step status
                    switch (newStatus)
                    {
                        case StepStatus.Completed:
                            step.Complete(output);
                            _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) completed");
                            _logger?.LogInformation("Plan step {StepIndex}/{TotalSteps} completed: {StepTitle} at {Timestamp:o}",
                                stepIndex, _currentPlan.Steps.Count, step.Title, DateTime.UtcNow);
                            break;
                        case StepStatus.Failed:
                            step.Fail(output);
                            _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) failed: {output ?? "No reason provided"}");
                            _logger?.LogError("Plan step {StepIndex}/{TotalSteps} failed: {StepTitle} - {Reason} at {Timestamp:o}",
                                stepIndex, _currentPlan.Steps.Count, step.Title, output ?? "No reason provided", DateTime.UtcNow);

                            // Automatically skip dependent steps when a step fails
                            dependentStepsSkipped = SkipDependentSteps(step, stepIndex, output);
                            if (dependentStepsSkipped > 0)
                            {
                                _currentPlan.AddLogEntry($"{dependentStepsSkipped} dependent step(s) automatically skipped due to failed step {stepIndex}");
                                _logger?.LogWarning("Auto-skipped {SkippedCount} dependent steps after step {StepIndex} failed at {Timestamp:o}",
                                    dependentStepsSkipped, stepIndex, DateTime.UtcNow);
                            }
                            break;
                        case StepStatus.Skipped:
                            step.Skip(output);
                            _currentPlan.AddLogEntry($"Step {stepIndex} ({step.Title}) skipped: {output ?? "No reason provided"}");
                            _logger?.LogInformation("Plan step {StepIndex}/{TotalSteps} skipped: {StepTitle} - {Reason} at {Timestamp:o}",
                                stepIndex, _currentPlan.Steps.Count, step.Title, output ?? "No reason provided", DateTime.UtcNow);
                            break;
                    }

                    // Advance current step index if this was the current step and it's done
                    if (arrayIndex == _currentPlan.CurrentStepIndex &&
                        (newStatus == StepStatus.Completed || newStatus == StepStatus.Skipped))
                    {
                        _currentPlan.AdvanceToNextStep();
                        _logger?.LogDebug("Plan advanced to step {CurrentStep}/{TotalSteps} at {Timestamp:o}", 
                            _currentPlan.CurrentStepIndex + 1, _currentPlan.Steps.Count, DateTime.UtcNow);
                    }

                    // Save the plan (using async internally for non-blocking I/O)
                    if (_planService != null && !string.IsNullOrEmpty(_currentPlan.ProjectId))
                    {
                        try
                        {
                            _planService.SavePlanAsync(_currentPlan).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            // Log but don't fail - the in-memory state is still updated
                            _logger?.LogWarning(ex, "Failed to save plan update at {Timestamp:o}", DateTime.UtcNow);
                        }
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
                    var blockedInfo = "";

                    // If step failed and dependent steps were skipped, inform about it
                    if (newStatus == StepStatus.Failed && dependentStepsSkipped > 0)
                    {
                        blockedInfo = $"\n\n⚠️ {dependentStepsSkipped} dependent step(s) have been automatically skipped because they depend on files from this failed step.";

                        // Get list of skipped steps
                        var skippedSteps = _currentPlan.Steps
                            .Where(s => s.Status == StepStatus.Skipped && s.Index > step.Index && s.Output?.Contains($"failed step {stepIndex}") == true)
                            .Select(s => $"  - Step {s.Index}: {s.Title}")
                            .ToList();

                        if (skippedSteps.Count > 0)
                        {
                            blockedInfo += "\nSkipped steps:\n" + string.Join("\n", skippedSteps);
                        }
                    }

                    if (_currentPlan.HasMoreSteps)
                    {
                        // Find next executable step (skip already skipped/failed ones)
                        var nextStep = _currentPlan.Steps
                            .FirstOrDefault(s => s.Status == StepStatus.Pending || s.Status == StepStatus.InProgress);

                        if (nextStep != null)
                        {
                            nextStepInfo = $"\n\nNext step: Step {nextStep.Index} - {nextStep.Title}";
                        }
                        else
                        {
                            // All remaining steps are either done or blocked
                            var pendingCount = _currentPlan.Steps.Count(s => s.Status == StepStatus.Pending);
                            if (pendingCount == 0)
                            {
                                nextStepInfo = "\n\nAll steps finished! Plan execution complete (some steps may have failed or been skipped).";
                            }
                        }
                    }
                    else
                    {
                        nextStepInfo = "\n\nAll steps completed! Plan execution finished.";
                    }

                    return $@"{statusIcon} Step {stepIndex} marked as {statusStr}

Progress: {completedCount}/{totalSteps} steps ({progress}%){blockedInfo}{nextStepInfo}";
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
        public static void RegisterContext(KoboldImplementationPlan plan, KoboldPlanService? service, ILogger? logger = null)
        {
            lock (_lock)
            {
                _currentPlan = plan;
                _planService = service;
                _logger = logger;
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
                _logger = null;
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

        /// <summary>
        /// Skips steps that depend on the failed step.
        /// Uses file dependency analysis to determine which steps need to be skipped.
        /// </summary>
        /// <param name="failedStep">The step that failed</param>
        /// <param name="failedStepIndex">1-based index of the failed step</param>
        /// <param name="failureReason">Why the step failed</param>
        /// <returns>Number of steps that were skipped</returns>
        private static int SkipDependentSteps(ImplementationStep failedStep, int failedStepIndex, string? failureReason)
        {
            if (_currentPlan == null)
            {
                return 0;
            }

            var skippedCount = 0;
            var analyzer = new StepDependencyAnalyzer();

            // Get files that the failed step was supposed to create or modify
            var failedStepOutputFiles = new HashSet<string>(
                failedStep.FilesToCreate.Concat(failedStep.FilesToModify),
                StringComparer.OrdinalIgnoreCase
            );

            if (failedStepOutputFiles.Count == 0)
            {
                return 0; // No files to check dependencies against
            }

            // Check all subsequent pending steps for dependencies on the failed step
            foreach (var step in _currentPlan.Steps.Where(s => s.Index > failedStep.Index && s.Status == StepStatus.Pending))
            {
                // Check if this step depends on files the failed step was supposed to create/modify
                var dependsOnFailedStep = analyzer.HasDependency(failedStep, step);

                // Also check if this step tries to modify files the failed step was supposed to create
                var modifiesFailedOutput = step.FilesToModify.Any(f =>
                    failedStep.FilesToCreate.Contains(f, StringComparer.OrdinalIgnoreCase));

                if (dependsOnFailedStep || modifiesFailedOutput)
                {
                    var reason = $"Blocked: dependency on failed step {failedStepIndex} ({failedStep.Title}). " +
                                 $"Original failure: {failureReason ?? "No reason provided"}";
                    step.Skip(reason);
                    _currentPlan.AddLogEntry($"Step {step.Index} ({step.Title}) skipped - depends on failed step {failedStepIndex}");
                    _logger?.LogWarning(
                        "🟠 Step {StepIndex} ({StepTitle}) auto-skipped due to dependency on failed step {FailedStepIndex} at {Timestamp:o}",
                        step.Index, step.Title, failedStepIndex, DateTime.UtcNow);
                    skippedCount++;
                }
            }

            return skippedCount;
        }
    }
}
