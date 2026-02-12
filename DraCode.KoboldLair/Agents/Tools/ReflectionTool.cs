using System.Text.Json;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for structured self-reflection checkpoints during Kobold execution.
    /// Allows Kobolds to report progress, confidence, blockers, and decisions.
    /// Automatically detects concerning patterns and signals Drake for intervention.
    /// </summary>
    public class ReflectionTool : Tool
    {
        private static readonly object _lock = new();
        private static KoboldImplementationPlan? _currentPlan;
        private static KoboldPlanService? _planService;
        private static SharedPlanningContextService? _sharedPlanningContext;
        private static string? _currentProjectId;
        private static string? _currentTaskId;
        private static string? _currentKoboldId;
        private static ILogger? _logger;
        private static int _currentIteration;
        private static Action<DrakeInterventionSignal>? _interventionCallback;

        public override string Name => "reflect";

        public override string Description => @"Report a self-reflection checkpoint during task execution.

Use this tool periodically (every 3 iterations or when significant progress is made) to:
1. Track your progress toward completing the current step
2. Report any blockers or obstacles you're facing
3. Assess your confidence that the current approach will succeed
4. Decide whether to continue, pivot to a new approach, or escalate to supervisor

This helps ensure you stay on track and enables early detection of issues.

Parameters:
- progress_percent (required): How far along you are toward completing the current step (0-100)
- files_done (optional): Array of file paths you've successfully created or modified so far
- blockers (optional): Array of current obstacles or challenges you're facing
- confidence (required): Your confidence (0-100) that your current approach will succeed
- decision (required): Your decision - ""continue"", ""pivot"", or ""escalate""
- notes (optional): Any additional observations or context

Returns: Guidance based on your reflection and acknowledgment of recorded checkpoint.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                progress_percent = new
                {
                    type = "integer",
                    minimum = 0,
                    maximum = 100,
                    description = "Progress percentage toward completing current step (0-100)"
                },
                files_done = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "List of files successfully created or modified so far"
                },
                blockers = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "List of current obstacles or challenges"
                },
                confidence = new
                {
                    type = "integer",
                    minimum = 0,
                    maximum = 100,
                    description = "Confidence level (0-100) that current approach will succeed"
                },
                decision = new
                {
                    type = "string",
                    @enum = new[] { "continue", "pivot", "escalate" },
                    description = "Your decision: continue with current approach, pivot to new approach, or escalate to supervisor"
                },
                notes = new
                {
                    type = "string",
                    description = "Optional additional observations or context"
                }
            },
            required = new[] { "progress_percent", "confidence", "decision" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> arguments)
        {
            try
            {
                // Parse progress_percent
                if (!arguments.TryGetValue("progress_percent", out var progressObj))
                {
                    return "Error: 'progress_percent' parameter is required";
                }
                var progressPercent = ParseInt(progressObj, "progress_percent");
                if (progressPercent < 0 || progressPercent > 100)
                {
                    return "Error: 'progress_percent' must be between 0 and 100";
                }

                // Parse confidence
                if (!arguments.TryGetValue("confidence", out var confidenceObj))
                {
                    return "Error: 'confidence' parameter is required";
                }
                var confidence = ParseInt(confidenceObj, "confidence");
                if (confidence < 0 || confidence > 100)
                {
                    return "Error: 'confidence' must be between 0 and 100";
                }

                // Parse decision
                if (!arguments.TryGetValue("decision", out var decisionObj) || decisionObj == null)
                {
                    return "Error: 'decision' parameter is required";
                }
                var decisionStr = decisionObj is JsonElement decisionElement
                    ? decisionElement.GetString()
                    : decisionObj.ToString();

                if (string.IsNullOrEmpty(decisionStr))
                {
                    return "Error: 'decision' parameter is required";
                }

                ReflectionDecision decision = decisionStr.ToLowerInvariant() switch
                {
                    "continue" => ReflectionDecision.Continue,
                    "pivot" => ReflectionDecision.Pivot,
                    "escalate" => ReflectionDecision.Escalate,
                    _ => throw new ArgumentException($"Invalid decision '{decisionStr}'. Must be 'continue', 'pivot', or 'escalate'.")
                };

                // Parse optional files_done
                var filesDone = new List<string>();
                if (arguments.TryGetValue("files_done", out var filesObj) && filesObj != null)
                {
                    filesDone = ParseStringArray(filesObj);
                }

                // Parse optional blockers
                var blockers = new List<string>();
                if (arguments.TryGetValue("blockers", out var blockersObj) && blockersObj != null)
                {
                    blockers = ParseStringArray(blockersObj);
                }

                // Parse optional notes
                string? notes = null;
                if (arguments.TryGetValue("notes", out var notesObj) && notesObj != null)
                {
                    notes = notesObj is JsonElement notesElement
                        ? notesElement.GetString()
                        : notesObj.ToString();
                }

                // Create the reflection signal
                lock (_lock)
                {
                    if (_currentPlan == null)
                    {
                        return "Error: No implementation plan is currently active. Reflection requires an active plan context.";
                    }

                    var reflection = new ReflectionSignal
                    {
                        StepIndex = _currentPlan.CurrentStepIndex,
                        Iteration = _currentIteration,
                        ProgressPercent = progressPercent,
                        FilesDone = filesDone,
                        Blockers = blockers,
                        Confidence = confidence,
                        Decision = decision,
                        Notes = notes
                    };

                    // Initialize reflection history if needed
                    _currentPlan.ReflectionHistory ??= new List<ReflectionSignal>();

                    // Check for intervention conditions
                    var interventionSignal = CheckForIntervention(reflection, _currentPlan.ReflectionHistory);
                    if (interventionSignal != null)
                    {
                        reflection.DrakeSignalSent = true;
                        reflection.InterventionReasonSent = interventionSignal.Reason;

                        // Invoke callback if registered
                        _interventionCallback?.Invoke(interventionSignal);

                        _logger?.LogWarning(
                            "🚨 Intervention signal generated for Kobold {KoboldId} - Reason: {Reason}, Confidence: {Confidence}%",
                            _currentKoboldId?[..8] ?? "unknown", interventionSignal.Reason, confidence);
                    }

                    // Add to history
                    _currentPlan.ReflectionHistory.Add(reflection);

                    // Log the checkpoint
                    _currentPlan.AddLogEntry($"Reflection checkpoint: {progressPercent}% progress, {confidence}% confidence, decision: {decision}");

                    _logger?.LogInformation(
                        "📊 Reflection checkpoint - Kobold {KoboldId}, Step {StepIndex}, Iteration {Iteration}: " +
                        "{Progress}% progress, {Confidence}% confidence, {Decision}",
                        _currentKoboldId?[..8] ?? "unknown", _currentPlan.CurrentStepIndex + 1,
                        _currentIteration, progressPercent, confidence, decision);

                    // Record reflection to shared planning context
                    if (_sharedPlanningContext != null && !string.IsNullOrEmpty(_currentProjectId) && !string.IsNullOrEmpty(_currentTaskId))
                    {
                        try
                        {
                            _sharedPlanningContext.RecordReflectionAsync(_currentProjectId, _currentTaskId, reflection)
                                .GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to record reflection to shared planning context");
                        }
                    }

                    // Save plan with updated reflection history
                    if (_planService != null && !string.IsNullOrEmpty(_currentPlan.ProjectId))
                    {
                        try
                        {
                            _planService.SavePlanAsync(_currentPlan).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to save plan after reflection");
                        }
                    }

                    // Generate guidance based on the reflection
                    return GenerateGuidance(reflection, interventionSignal);
                }
            }
            catch (Exception ex)
            {
                return $"Error processing reflection: {ex.Message}";
            }
        }

        /// <summary>
        /// Checks if the current reflection warrants a Drake intervention signal
        /// </summary>
        private DrakeInterventionSignal? CheckForIntervention(ReflectionSignal current, List<ReflectionSignal> history)
        {
            InterventionReason? reason = null;

            // Check 1: Agent explicitly requested escalation
            if (current.Decision == ReflectionDecision.Escalate)
            {
                reason = InterventionReason.AgentEscalated;
            }
            // Check 2: Low confidence (below threshold)
            else if (current.Confidence < InterventionThresholds.LowConfidenceThreshold)
            {
                reason = InterventionReason.LowConfidence;
            }
            // Check 3: Multiple blockers
            else if (current.Blockers.Count >= InterventionThresholds.MultipleBlockersThreshold)
            {
                reason = InterventionReason.MultipleBlockers;
            }
            // Check 4: Declining confidence over multiple checkpoints
            else if (history.Count >= InterventionThresholds.DecliningConfidenceCheckpoints - 1)
            {
                var recentHistory = history
                    .TakeLast(InterventionThresholds.DecliningConfidenceCheckpoints - 1)
                    .ToList();

                if (recentHistory.Count >= InterventionThresholds.DecliningConfidenceCheckpoints - 1)
                {
                    var firstConfidence = recentHistory[0].Confidence;
                    var confidenceDrop = firstConfidence - current.Confidence;

                    if (confidenceDrop >= InterventionThresholds.DecliningConfidenceDropThreshold)
                    {
                        reason = InterventionReason.DecliningConfidence;
                    }
                }
            }
            // Check 5: Stalled progress
            else if (history.Count >= InterventionThresholds.StalledProgressCheckpoints - 1)
            {
                var recentHistory = history
                    .TakeLast(InterventionThresholds.StalledProgressCheckpoints - 1)
                    .ToList();

                // All recent checkpoints show 0% progress AND current is 0%
                if (current.ProgressPercent == 0 && recentHistory.All(h => h.ProgressPercent == 0))
                {
                    reason = InterventionReason.StalledProgress;
                }
            }

            if (reason == null)
            {
                return null;
            }

            return new DrakeInterventionSignal
            {
                KoboldId = _currentKoboldId ?? string.Empty,
                TaskId = _currentTaskId ?? string.Empty,
                ProjectId = _currentProjectId ?? string.Empty,
                Reason = reason.Value,
                Confidence = current.Confidence,
                Blockers = current.Blockers,
                SourceReflection = current
            };
        }

        /// <summary>
        /// Generates guidance based on the reflection state
        /// </summary>
        private string GenerateGuidance(ReflectionSignal reflection, DrakeInterventionSignal? intervention)
        {
            var sb = new System.Text.StringBuilder();

            // Acknowledge the checkpoint
            var decisionEmoji = reflection.Decision switch
            {
                ReflectionDecision.Continue => "✅",
                ReflectionDecision.Pivot => "🔄",
                ReflectionDecision.Escalate => "🚨",
                _ => "📊"
            };

            sb.AppendLine($"{decisionEmoji} Reflection checkpoint recorded");
            sb.AppendLine();
            sb.AppendLine($"Progress: {reflection.ProgressPercent}%");
            sb.AppendLine($"Confidence: {reflection.Confidence}%");
            sb.AppendLine($"Decision: {reflection.Decision}");

            if (reflection.FilesDone.Count > 0)
            {
                sb.AppendLine($"Files completed: {string.Join(", ", reflection.FilesDone)}");
            }

            if (reflection.Blockers.Count > 0)
            {
                sb.AppendLine($"Blockers: {string.Join(", ", reflection.Blockers)}");
            }

            sb.AppendLine();

            // Add intervention warning if applicable
            if (intervention != null)
            {
                sb.AppendLine("⚠️ **INTERVENTION SIGNAL GENERATED**");
                sb.AppendLine($"Reason: {GetInterventionReasonDescription(intervention.Reason)}");
                sb.AppendLine("Drake supervisor has been notified and may intervene.");
                sb.AppendLine();
            }

            // Generate guidance based on confidence level
            sb.AppendLine("**Guidance:**");

            if (reflection.Confidence >= 70)
            {
                sb.AppendLine("High confidence - Continue with current approach.");
                if (reflection.Decision == ReflectionDecision.Continue)
                {
                    sb.AppendLine("Your approach is working well. Proceed to the next action.");
                }
            }
            else if (reflection.Confidence >= 40)
            {
                sb.AppendLine("Medium confidence - Consider simplifying your approach.");
                sb.AppendLine("If facing obstacles, try breaking the problem into smaller pieces.");
                if (reflection.Blockers.Count > 0)
                {
                    sb.AppendLine($"Address blockers one at a time: start with '{reflection.Blockers[0]}'");
                }
            }
            else
            {
                sb.AppendLine("Low confidence - Strongly recommend reassessing approach.");
                sb.AppendLine("Consider:");
                sb.AppendLine("1. Re-reading the step requirements carefully");
                sb.AppendLine("2. Checking if any prerequisite files/dependencies are missing");
                sb.AppendLine("3. Simplifying the implementation");
                if (reflection.Decision != ReflectionDecision.Escalate)
                {
                    sb.AppendLine("4. Escalating to supervisor if you're stuck");
                }
            }

            // Decision-specific guidance
            if (reflection.Decision == ReflectionDecision.Pivot)
            {
                sb.AppendLine();
                sb.AppendLine("**Pivoting:** Before changing approach, document:");
                sb.AppendLine("- Why the previous approach wasn't working");
                sb.AppendLine("- What the new approach will be");
                sb.AppendLine("- Expected outcome of the new approach");
            }
            else if (reflection.Decision == ReflectionDecision.Escalate)
            {
                sb.AppendLine();
                sb.AppendLine("**Escalating:** Escalation signal sent. While waiting:");
                sb.AppendLine("- Document what you've tried so far");
                sb.AppendLine("- Note any partial progress that might be salvageable");
                sb.AppendLine("- Consider if there's a simpler version of the task you could complete");
            }

            return sb.ToString();
        }

        private string GetInterventionReasonDescription(InterventionReason reason)
        {
            return reason switch
            {
                InterventionReason.AgentEscalated => "Agent explicitly requested escalation",
                InterventionReason.LowConfidence => $"Confidence dropped below {InterventionThresholds.LowConfidenceThreshold}%",
                InterventionReason.DecliningConfidence => $"Confidence declined by {InterventionThresholds.DecliningConfidenceDropThreshold}%+ over {InterventionThresholds.DecliningConfidenceCheckpoints} checkpoints",
                InterventionReason.MultipleBlockers => $"{InterventionThresholds.MultipleBlockersThreshold}+ blockers reported",
                InterventionReason.RepeatedFileModifications => $"Same file modified {InterventionThresholds.RepeatedFileModificationThreshold}+ times (possible stuck loop)",
                InterventionReason.StalledProgress => $"No progress for {InterventionThresholds.StalledProgressCheckpoints} consecutive checkpoints",
                _ => "Unknown reason"
            };
        }

        #region Parsing Helpers

        private static int ParseInt(object obj, string paramName)
        {
            if (obj is JsonElement jsonElement)
            {
                return jsonElement.GetInt32();
            }
            if (obj is int intVal)
            {
                return intVal;
            }
            if (obj is long longVal)
            {
                return (int)longVal;
            }
            if (int.TryParse(obj?.ToString(), out var parsed))
            {
                return parsed;
            }
            throw new ArgumentException($"'{paramName}' must be an integer");
        }

        private static List<string> ParseStringArray(object obj)
        {
            var result = new List<string>();

            if (obj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonElement.EnumerateArray())
                {
                    var str = item.GetString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        result.Add(str);
                    }
                }
            }
            else if (obj is IEnumerable<object> enumerable)
            {
                foreach (var item in enumerable)
                {
                    var str = item?.ToString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        result.Add(str);
                    }
                }
            }
            else if (obj is IEnumerable<string> strings)
            {
                result.AddRange(strings.Where(s => !string.IsNullOrEmpty(s)));
            }

            return result;
        }

        #endregion

        #region Static Context Management

        /// <summary>
        /// Registers the current plan and service for use by this tool.
        /// Call this before the Kobold starts working with a plan.
        /// </summary>
        public static void RegisterContext(
            KoboldImplementationPlan plan,
            KoboldPlanService? planService,
            ILogger? logger = null,
            SharedPlanningContextService? sharedPlanningContext = null,
            string? projectId = null,
            string? taskId = null,
            string? koboldId = null,
            Action<DrakeInterventionSignal>? interventionCallback = null)
        {
            lock (_lock)
            {
                _currentPlan = plan;
                _planService = planService;
                _logger = logger;
                _sharedPlanningContext = sharedPlanningContext;
                _currentProjectId = projectId ?? plan.ProjectId;
                _currentTaskId = taskId ?? plan.TaskId;
                _currentKoboldId = koboldId;
                _currentIteration = 0;
                _interventionCallback = interventionCallback;
            }
        }

        /// <summary>
        /// Updates the current iteration count (called from execution loop)
        /// </summary>
        public static void SetCurrentIteration(int iteration)
        {
            lock (_lock)
            {
                _currentIteration = iteration;
            }
        }

        /// <summary>
        /// Clears the current context.
        /// Call this when the Kobold finishes working.
        /// </summary>
        public static void ClearContext()
        {
            lock (_lock)
            {
                _currentPlan = null;
                _planService = null;
                _logger = null;
                _sharedPlanningContext = null;
                _currentProjectId = null;
                _currentTaskId = null;
                _currentKoboldId = null;
                _currentIteration = 0;
                _interventionCallback = null;
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
        /// Gets the pending intervention signal if any (for Drake to check).
        /// Returns the most recent unacknowledged signal from the current plan.
        /// </summary>
        public static DrakeInterventionSignal? GetPendingInterventionSignal()
        {
            lock (_lock)
            {
                if (_currentPlan?.ReflectionHistory == null)
                {
                    return null;
                }

                var lastReflectionWithSignal = _currentPlan.ReflectionHistory
                    .LastOrDefault(r => r.DrakeSignalSent);

                if (lastReflectionWithSignal == null)
                {
                    return null;
                }

                // Reconstruct the signal from the reflection
                return new DrakeInterventionSignal
                {
                    KoboldId = _currentKoboldId ?? string.Empty,
                    TaskId = _currentTaskId ?? string.Empty,
                    ProjectId = _currentProjectId ?? string.Empty,
                    Reason = lastReflectionWithSignal.InterventionReasonSent ?? InterventionReason.AgentEscalated,
                    Confidence = lastReflectionWithSignal.Confidence,
                    Blockers = lastReflectionWithSignal.Blockers,
                    SourceReflection = lastReflectionWithSignal
                };
            }
        }

        #endregion
    }
}
