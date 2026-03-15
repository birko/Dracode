using System.Text.Json;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for Kobold self-reflection during plan execution.
    /// Captures progress assessments, detects stalls, and triggers escalations
    /// when confidence drops below threshold or progress stalls.
    /// </summary>
    public class ReflectionTool : Tool
    {
        private static readonly object _lock = new();
        private static KoboldImplementationPlan? _currentPlan;
        private static KoboldPlanService? _planService;
        private static ILogger? _logger;
        private static string? _currentProjectId;
        private static string? _currentTaskId;
        private static Guid _koboldId;
        private static string? _agentType;
        private static Action<EscalationAlert>? _onEscalation;
        private static ReflectionConfiguration? _config;

        public override string Name => "reflect";

        public override string Description => @"Report your self-assessment of current progress. Call this tool at checkpoints to report your confidence, progress, and any blockers.

Parameters:
- progress_percent (required): Your estimate of overall step completion (0-100)
- blockers (optional): Description of any obstacles preventing progress
- confidence_percent (required): Your confidence this approach will succeed (0-100)
- decision (required): ""continue"" (keep going), ""pivot"" (change approach), or ""escalate"" (need help from upstream)
- escalation_type (optional, required if decision is ""escalate""): ""task_infeasible"", ""missing_dependency"", ""needs_split"", ""wrong_approach"", or ""wrong_agent_type""
- adjustment (optional): Description of what you're changing if pivoting

Returns: Guidance on remaining budget, escalation status, and next steps.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                progress_percent = new
                {
                    type = "integer",
                    description = "Estimate of overall step completion (0-100)"
                },
                blockers = new
                {
                    type = "string",
                    description = "Description of any obstacles"
                },
                confidence_percent = new
                {
                    type = "integer",
                    description = "Confidence this approach will succeed (0-100)"
                },
                decision = new
                {
                    type = "string",
                    @enum = new[] { "continue", "pivot", "escalate" },
                    description = "Your decision: continue, pivot approach, or escalate"
                },
                escalation_type = new
                {
                    type = "string",
                    @enum = new[] { "task_infeasible", "missing_dependency", "needs_split", "wrong_approach", "wrong_agent_type" },
                    description = "Type of escalation (required if decision is 'escalate')"
                },
                adjustment = new
                {
                    type = "string",
                    description = "What you're changing if pivoting"
                }
            },
            required = new[] { "progress_percent", "confidence_percent", "decision" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> arguments)
        {
            try
            {
                var progressPercent = ParseInt(arguments, "progress_percent");
                var confidencePercent = ParseInt(arguments, "confidence_percent");
                var decisionStr = ParseString(arguments, "decision") ?? "continue";
                var blockers = ParseString(arguments, "blockers");
                var escalationTypeStr = ParseString(arguments, "escalation_type");
                var adjustment = ParseString(arguments, "adjustment");

                var decision = decisionStr.ToLowerInvariant() switch
                {
                    "continue" => ReflectionDecision.Continue,
                    "pivot" => ReflectionDecision.Pivot,
                    "escalate" => ReflectionDecision.Escalate,
                    _ => ReflectionDecision.Continue
                };

                EscalationType? escalationType = escalationTypeStr?.ToLowerInvariant() switch
                {
                    "task_infeasible" => Models.Agents.EscalationType.TaskInfeasible,
                    "missing_dependency" => Models.Agents.EscalationType.MissingDependency,
                    "needs_split" => Models.Agents.EscalationType.NeedsSplit,
                    "wrong_approach" => Models.Agents.EscalationType.WrongApproach,
                    "wrong_agent_type" => Models.Agents.EscalationType.WrongAgentType,
                    _ => null
                };

                lock (_lock)
                {
                    if (_currentPlan == null)
                    {
                        return "Error: No implementation plan is currently active.";
                    }

                    var currentStepIndex = _currentPlan.CurrentStepIndex + 1; // 1-based for display

                    // Create reflection entry
                    var entry = new ReflectionEntry
                    {
                        Iteration = _currentPlan.ExecutionLog.Count,
                        StepIndex = currentStepIndex,
                        ProgressPercent = progressPercent,
                        Blockers = blockers,
                        ConfidencePercent = confidencePercent,
                        Decision = decision,
                        EscalationType = escalationType,
                        Adjustment = adjustment
                    };
                    _currentPlan.Reflections.Add(entry);

                    var config = _config ?? new ReflectionConfiguration();
                    var shouldEscalate = false;
                    EscalationType autoEscalationType = Models.Agents.EscalationType.WrongApproach;
                    string escalationReason = "";

                    // Stall detection: check if last N reflections show no progress
                    var stallCount = config.StallDetectionCount;
                    var recentReflections = _currentPlan.Reflections
                        .Where(r => r.StepIndex == currentStepIndex)
                        .TakeLast(stallCount)
                        .ToList();

                    if (recentReflections.Count >= stallCount)
                    {
                        var progressValues = recentReflections.Select(r => r.ProgressPercent).ToList();
                        var isStalled = progressValues.Last() <= progressValues.First();
                        if (isStalled)
                        {
                            shouldEscalate = true;
                            autoEscalationType = Models.Agents.EscalationType.WrongApproach;
                            escalationReason = $"Progress stalled: last {stallCount} reflections show no advancement ({string.Join("→", progressValues)}%)";
                            _logger?.LogWarning(
                                "Stall detected for Kobold {KoboldId} on step {StepIndex}: progress {Progress}",
                                _koboldId.ToString()[..8], currentStepIndex, string.Join("→", progressValues));
                        }
                    }

                    // Low confidence check
                    if (confidencePercent < config.EscalationConfidenceThreshold)
                    {
                        shouldEscalate = true;
                        autoEscalationType = escalationType ?? Models.Agents.EscalationType.WrongApproach;
                        escalationReason = $"Confidence ({confidencePercent}%) below threshold ({config.EscalationConfidenceThreshold}%)";
                    }

                    // Explicit escalation request
                    if (decision == ReflectionDecision.Escalate)
                    {
                        shouldEscalate = true;
                        autoEscalationType = escalationType ?? Models.Agents.EscalationType.WrongApproach;
                        escalationReason = $"Kobold requested escalation: {blockers ?? "no details"}";
                    }

                    // Create and dispatch escalation alert if needed
                    string escalationStatus = "";
                    if (shouldEscalate && config.Enabled)
                    {
                        var alert = new EscalationAlert
                        {
                            ProjectId = _currentProjectId ?? _currentPlan.ProjectId,
                            TaskId = _currentTaskId ?? _currentPlan.TaskId,
                            KoboldId = _koboldId,
                            AgentType = _agentType ?? "",
                            Source = EscalationSource.ReflectionTool,
                            Type = autoEscalationType,
                            Summary = escalationReason,
                            ReflectionHistory = _currentPlan.Reflections
                                .Where(r => r.StepIndex == currentStepIndex)
                                .ToList()
                        };
                        _currentPlan.Escalations.Add(alert);

                        _logger?.LogWarning(
                            "Escalation raised for Kobold {KoboldId}: {Type} - {Summary}",
                            _koboldId.ToString()[..8], autoEscalationType, escalationReason);

                        try
                        {
                            _onEscalation?.Invoke(alert);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error invoking escalation callback");
                        }

                        escalationStatus = $"\n\n⚠️ ESCALATION RAISED: {autoEscalationType} - {escalationReason}\nDrake has been notified and may intervene.";
                    }

                    // Save plan (debounced)
                    if (_planService != null && !string.IsNullOrEmpty(_currentPlan.ProjectId))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _planService.SavePlanDebouncedAsync(_currentPlan);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "Failed to save plan after reflection");
                            }
                        });
                    }

                    // Build guidance response
                    var completedSteps = _currentPlan.CompletedStepsCount;
                    var totalSteps = _currentPlan.Steps.Count;
                    var planProgress = _currentPlan.ProgressPercentage;

                    var pivotNote = decision == ReflectionDecision.Pivot && !string.IsNullOrEmpty(adjustment)
                        ? $"\n\nPivot acknowledged: {adjustment}. Proceed with your adjusted approach."
                        : "";

                    return $@"Reflection recorded for step {currentStepIndex}.

Plan progress: {completedSteps}/{totalSteps} steps ({planProgress}%)
Step progress: {progressPercent}%
Confidence: {confidencePercent}%
Decision: {decision}{pivotNote}{escalationStatus}

Continue working on your current step. Call `update_plan_step` when complete.";
                }
            }
            catch (Exception ex)
            {
                return $"Error recording reflection: {ex.Message}";
            }
        }

        /// <summary>
        /// Registers the context for the reflection tool.
        /// </summary>
        public static void RegisterContext(
            KoboldImplementationPlan plan,
            KoboldPlanService? planService,
            ILogger? logger,
            string? projectId,
            string? taskId,
            Guid koboldId,
            string? agentType,
            Action<EscalationAlert>? onEscalation,
            ReflectionConfiguration? config)
        {
            lock (_lock)
            {
                _currentPlan = plan;
                _planService = planService;
                _logger = logger;
                _currentProjectId = projectId ?? plan.ProjectId;
                _currentTaskId = taskId ?? plan.TaskId;
                _koboldId = koboldId;
                _agentType = agentType;
                _onEscalation = onEscalation;
                _config = config;
            }
        }

        /// <summary>
        /// Clears the current context.
        /// </summary>
        public static void ClearContext()
        {
            lock (_lock)
            {
                _currentPlan = null;
                _planService = null;
                _logger = null;
                _currentProjectId = null;
                _currentTaskId = null;
                _koboldId = Guid.Empty;
                _agentType = null;
                _onEscalation = null;
                _config = null;
            }
        }

        private static int ParseInt(Dictionary<string, object> arguments, string key)
        {
            if (!arguments.TryGetValue(key, out var val)) return 0;
            if (val is JsonElement je) return je.GetInt32();
            if (val is int i) return i;
            if (val is long l) return (int)l;
            if (int.TryParse(val?.ToString(), out var parsed)) return parsed;
            return 0;
        }

        private static string? ParseString(Dictionary<string, object> arguments, string key)
        {
            if (!arguments.TryGetValue(key, out var val) || val == null) return null;
            if (val is JsonElement je) return je.GetString();
            return val.ToString();
        }
    }
}
