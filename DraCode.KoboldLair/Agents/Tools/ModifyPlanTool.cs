using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Phase 4: Tool that allows agents to suggest modifications to the implementation plan.
    /// Supports: skip step, combine steps, reorder steps, add step.
    /// Requires approval based on configuration.
    /// </summary>
    public class ModifyPlanTool : Tool
    {
        private static KoboldImplementationPlan? _currentPlan;
        private static KoboldPlanService? _planService;
        private static ILogger? _logger;
        private static bool _allowModifications = false;
        private static bool _autoApprove = false;
        private static readonly object _lockObject = new object();

        public override string Name => "modify_plan";

        public override string Description => @"Suggests a modification to the implementation plan. Use this when you identify opportunities to optimize the plan during execution.

Supported operations:
- skip: Skip a step (provide step_number and reason)
- combine: Combine two consecutive steps (provide step_number and next_step_number)
- reorder: Move a step to a different position (provide step_number and new_position)
- add: Add a new step (provide position, title, description)

The modification will be logged and may require approval depending on configuration.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                operation = new
                {
                    type = "string",
                    @enum = new[] { "skip", "combine", "reorder", "add" },
                    description = "The type of modification to make"
                },
                step_number = new
                {
                    type = "integer",
                    description = "The step number to modify (1-based)"
                },
                next_step_number = new
                {
                    type = "integer",
                    description = "For combine: the next step to combine with"
                },
                new_position = new
                {
                    type = "integer",
                    description = "For reorder: the new position for the step"
                },
                position = new
                {
                    type = "integer",
                    description = "For add: the position to insert the new step"
                },
                title = new
                {
                    type = "string",
                    description = "For add: the title of the new step"
                },
                description = new
                {
                    type = "string",
                    description = "For add: the description of the new step"
                },
                files_to_create = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "For add: optional list of files to create"
                },
                files_to_modify = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "For add: optional list of files to modify"
                },
                reason = new
                {
                    type = "string",
                    description = "Explanation for why this modification is needed"
                }
            },
            required = new[] { "operation", "reason" }
        };

        /// <summary>
        /// Registers the plan context for this tool
        /// </summary>
        public static void RegisterContext(
            KoboldImplementationPlan plan,
            KoboldPlanService? planService,
            bool allowModifications,
            bool autoApprove,
            ILogger? logger = null)
        {
            lock (_lockObject)
            {
                _currentPlan = plan;
                _planService = planService;
                _allowModifications = allowModifications;
                _autoApprove = autoApprove;
                _logger = logger;
            }
        }

        /// <summary>
        /// Clears the registered context
        /// </summary>
        public static void ClearContext()
        {
            lock (_lockObject)
            {
                _currentPlan = null;
                _planService = null;
                _logger = null;
            }
        }

        public override string Execute(string workingDirectory, Dictionary<string, object> parameters)
        {
            lock (_lockObject)
            {
                if (!_allowModifications)
                {
                    return "Error: Plan modifications are not enabled. Set AllowPlanModifications=true in configuration.";
                }

                if (_currentPlan == null)
                {
                    return "Error: No plan context registered. This tool can only be used within plan execution.";
                }

                if (!parameters.TryGetValue("operation", out var operationObj) || operationObj is not string operation)
                {
                    return "Error: Missing or invalid 'operation' parameter.";
                }

                if (!parameters.TryGetValue("reason", out var reasonObj) || reasonObj is not string reason)
                {
                    return "Error: Missing or invalid 'reason' parameter.";
                }

                // Log the modification request
                var modification = $"Operation: {operation}, Reason: {reason}";
                _logger?.LogInformation("📝 Plan modification requested: {Modification}", modification);
                _currentPlan.AddLogEntry($"Modification requested: {modification}");

                // Check auto-approval
                if (!_autoApprove)
                {
                    _currentPlan.AddLogEntry($"Modification pending approval: {modification}");
                    return $"Plan modification logged and pending approval:\n{operation}: {reason}\n\nThe modification has been recorded but not applied. Manual approval required.";
                }

                // Execute the modification
                try
                {
                    string result = operation switch
                    {
                        "skip" => HandleSkipStep(parameters, reason),
                        "combine" => HandleCombineSteps(parameters, reason),
                        "reorder" => HandleReorderStep(parameters, reason),
                        "add" => HandleAddStep(parameters, reason),
                        _ => $"Error: Unknown operation '{operation}'"
                    };

                    // Save the modified plan
                    if (_planService != null && !result.StartsWith("Error"))
                    {
                        _planService.SavePlanAsync(_currentPlan).Wait();
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to apply plan modification: {Operation}", operation);
                    return $"Error: Failed to apply modification - {ex.Message}";
                }
            }
        }

        private string HandleSkipStep(Dictionary<string, object> parameters, string reason)
        {
            if (!parameters.TryGetValue("step_number", out var stepNumObj) || 
                !int.TryParse(stepNumObj.ToString(), out int stepNumber))
            {
                return "Error: Missing or invalid 'step_number' parameter.";
            }

            var step = _currentPlan!.Steps.FirstOrDefault(s => s.Index == stepNumber);
            if (step == null)
            {
                return $"Error: Step {stepNumber} not found.";
            }

            step.Skip(reason);
            _currentPlan.AddLogEntry($"Step {stepNumber} skipped: {reason}");
            _logger?.LogInformation("⏭️ Step {StepNumber} skipped: {Reason}", stepNumber, reason);

            return $"✅ Step {stepNumber} ({step.Title}) has been marked as skipped.\nReason: {reason}";
        }

        private string HandleCombineSteps(Dictionary<string, object> parameters, string reason)
        {
            if (!parameters.TryGetValue("step_number", out var stepNumObj) || 
                !int.TryParse(stepNumObj.ToString(), out int stepNumber))
            {
                return "Error: Missing or invalid 'step_number' parameter.";
            }

            if (!parameters.TryGetValue("next_step_number", out var nextStepNumObj) || 
                !int.TryParse(nextStepNumObj.ToString(), out int nextStepNumber))
            {
                return "Error: Missing or invalid 'next_step_number' parameter.";
            }

            var step1 = _currentPlan!.Steps.FirstOrDefault(s => s.Index == stepNumber);
            var step2 = _currentPlan.Steps.FirstOrDefault(s => s.Index == nextStepNumber);

            if (step1 == null || step2 == null)
            {
                return $"Error: One or both steps not found.";
            }

            // Combine step descriptions
            step1.Description += $"\n\n**Combined with Step {nextStepNumber}:**\n{step2.Description}";
            
            // Merge file lists
            step1.FilesToCreate.AddRange(step2.FilesToCreate.Except(step1.FilesToCreate));
            step1.FilesToModify.AddRange(step2.FilesToModify.Except(step1.FilesToModify));

            // Mark second step as skipped
            step2.Skip($"Combined into step {stepNumber}: {reason}");

            _currentPlan.AddLogEntry($"Steps {stepNumber} and {nextStepNumber} combined: {reason}");
            _logger?.LogInformation("🔗 Steps {Step1} and {Step2} combined: {Reason}", stepNumber, nextStepNumber, reason);

            return $"✅ Steps {stepNumber} and {nextStepNumber} have been combined.\nReason: {reason}\n\nStep {stepNumber} now includes work from both steps.";
        }

        private string HandleReorderStep(Dictionary<string, object> parameters, string reason)
        {
            // Extract required parameters
            if (!parameters.TryGetValue("step_number", out var stepNumObj) ||
                !int.TryParse(stepNumObj.ToString(), out int stepNumber))
            {
                return "Error: Missing or invalid 'step_number' parameter.";
            }

            if (!parameters.TryGetValue("new_position", out var newPosObj) ||
                !int.TryParse(newPosObj.ToString(), out int newPosition))
            {
                return "Error: Missing or invalid 'new_position' parameter.";
            }

            var step = _currentPlan!.Steps.FirstOrDefault(s => s.Index == stepNumber);
            if (step == null)
            {
                return $"Error: Step {stepNumber} not found.";
            }

            if (newPosition < 1 || newPosition > _currentPlan.Steps.Count)
            {
                return $"Error: new_position must be between 1 and {_currentPlan.Steps.Count}.";
            }

            if (stepNumber == newPosition)
            {
                return $"Step {stepNumber} is already at position {newPosition}.";
            }

            // Remove step from current position
            _currentPlan.Steps.Remove(step);

            // Insert at new position (adjust for 0-based indexing)
            _currentPlan.Steps.Insert(newPosition - 1, step);

            // Reindex all steps
            for (int i = 0; i < _currentPlan.Steps.Count; i++)
            {
                _currentPlan.Steps[i].Index = i + 1;
            }

            _currentPlan.AddLogEntry($"Step {stepNumber} moved to position {newPosition}: {reason}");
            _logger?.LogInformation("🔀 Step {StepNumber} moved to position {NewPosition}: {Reason}", stepNumber, newPosition, reason);

            return $"✅ Step moved from position {stepNumber} to {newPosition}.\nReason: {reason}\n\nAll steps have been reindexed.";
        }

        private string HandleAddStep(Dictionary<string, object> parameters, string reason)
        {
            // Extract required parameters
            if (!parameters.TryGetValue("position", out var posObj) ||
                !int.TryParse(posObj.ToString(), out int position))
            {
                return "Error: Missing or invalid 'position' parameter.";
            }

            if (!parameters.TryGetValue("title", out var titleObj) || string.IsNullOrWhiteSpace(titleObj?.ToString()))
            {
                return "Error: Missing or invalid 'title' parameter.";
            }

            if (!parameters.TryGetValue("description", out var descObj) || string.IsNullOrWhiteSpace(descObj?.ToString()))
            {
                return "Error: Missing or invalid 'description' parameter.";
            }

            string title = titleObj.ToString()!;
            string description = descObj.ToString()!;

            if (position < 1 || position > _currentPlan!.Steps.Count + 1)
            {
                return $"Error: position must be between 1 and {_currentPlan.Steps.Count + 1}.";
            }

            // Create new step
            var newStep = new ImplementationStep
            {
                Index = position,
                Title = title,
                Description = description,
                Status = StepStatus.Pending,
                FilesToCreate = new List<string>(),
                FilesToModify = new List<string>()
            };

            // Extract optional file lists
            if (parameters.TryGetValue("files_to_create", out var filesToCreateObj) && filesToCreateObj is JsonElement filesToCreateJson)
            {
                if (filesToCreateJson.ValueKind == JsonValueKind.Array)
                {
                    foreach (var file in filesToCreateJson.EnumerateArray())
                    {
                        if (file.ValueKind == JsonValueKind.String)
                        {
                            newStep.FilesToCreate.Add(file.GetString()!);
                        }
                    }
                }
            }

            if (parameters.TryGetValue("files_to_modify", out var filesToModifyObj) && filesToModifyObj is JsonElement filesToModifyJson)
            {
                if (filesToModifyJson.ValueKind == JsonValueKind.Array)
                {
                    foreach (var file in filesToModifyJson.EnumerateArray())
                    {
                        if (file.ValueKind == JsonValueKind.String)
                        {
                            newStep.FilesToModify.Add(file.GetString()!);
                        }
                    }
                }
            }

            // Insert at position (adjust for 0-based indexing)
            _currentPlan.Steps.Insert(position - 1, newStep);

            // Reindex all steps
            for (int i = 0; i < _currentPlan.Steps.Count; i++)
            {
                _currentPlan.Steps[i].Index = i + 1;
            }

            _currentPlan.AddLogEntry($"New step added at position {position}: {title} - {reason}");
            _logger?.LogInformation("➕ New step added at position {Position}: {Title} - {Reason}", position, title, reason);

            return $"✅ New step added at position {position}.\nTitle: {title}\nReason: {reason}\n\nAll subsequent steps have been reindexed.";
        }
    }
}
