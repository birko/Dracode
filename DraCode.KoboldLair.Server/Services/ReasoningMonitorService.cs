using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Orchestrators;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors working Kobolds for reasoning anomalies.
    /// Detects stuck loops, stalled progress, repeated errors, and budget exhaustion.
    /// Creates escalation alerts and routes them through Drake for upstream handling.
    /// </summary>
    public class ReasoningMonitorService : PeriodicBackgroundService
    {
        private readonly ILogger<ReasoningMonitorService> _logger;
        private readonly KoboldFactory _koboldFactory;
        private readonly DrakeFactory _drakeFactory;
        private readonly ReflectionConfiguration _config;

        protected override ILogger Logger => _logger;

        public ReasoningMonitorService(
            ILogger<ReasoningMonitorService> logger,
            KoboldFactory koboldFactory,
            DrakeFactory drakeFactory,
            ReflectionConfiguration config,
            int monitorIntervalSeconds = 45)
            : base(TimeSpan.FromSeconds(monitorIntervalSeconds), initialDelay: TimeSpan.FromSeconds(30))
        {
            _logger = logger;
            _koboldFactory = koboldFactory;
            _drakeFactory = drakeFactory;
            _config = config;
        }

        protected override async Task ExecuteCycleAsync(CancellationToken stoppingToken)
        {
            if (!_config.Enabled)
                return;

            var workingKobolds = _koboldFactory.GetKoboldsByStatus(KoboldStatus.Working);
            if (workingKobolds.Count == 0)
            {
                _logger.LogDebug("No working Kobolds to monitor");
                return;
            }

            _logger.LogDebug("ReasoningMonitor checking {Count} working Kobold(s)", workingKobolds.Count);

            foreach (var kobold in workingKobolds)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await CheckKoboldAsync(kobold);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error monitoring Kobold {KoboldId}", kobold.Id.ToString()[..8]);
                }
            }
        }

        private async Task CheckKoboldAsync(Kobold kobold)
        {
            var plan = kobold.ImplementationPlan;
            if (plan == null) return;

            if (!string.IsNullOrEmpty(kobold.ProjectId)
                && BelongsToParkedTask(kobold, _drakeFactory.GetDrakesByProject(kobold.ProjectId)))
                return;

            // Check 1: Stuck loop - same file written repeatedly across execution log
            CheckStuckLoop(kobold, plan);

            // Check 2: Stalled progress - no update_plan_step calls but LLM still responding
            CheckStalledProgress(kobold, plan);

            // Check 3: Repeated errors - last N reflections have identical blockers
            CheckRepeatedErrors(kobold, plan);

            // Check 4: Budget warning - high iteration usage with low progress
            CheckBudgetExhaustion(kobold, plan);

            await Task.CompletedTask;
        }

        public static bool BelongsToParkedTask(Kobold kobold, IEnumerable<Drake> projectDrakes) =>
            kobold.TaskId is { } taskId && projectDrakes.Any(d => d.IsTaskParked(taskId.ToString()));

        private void CheckStuckLoop(Kobold kobold, KoboldImplementationPlan plan)
        {
            // Look for files written more than MaxFileWriteRepetitions times in execution log
            var fileWritePattern = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var logEntry in plan.ExecutionLog)
            {
                // Check for patterns like "wrote file X" or "created file X" in log entries
                if (logEntry.Message.Contains("write_file", StringComparison.OrdinalIgnoreCase) ||
                    logEntry.Message.Contains("created", StringComparison.OrdinalIgnoreCase))
                {
                    // Use the whole message as a rough key
                    if (fileWritePattern.ContainsKey(logEntry.Message))
                        fileWritePattern[logEntry.Message]++;
                    else
                        fileWritePattern[logEntry.Message] = 1;
                }
            }

            var repeatedWrites = fileWritePattern.Where(kvp => kvp.Value >= _config.MaxFileWriteRepetitions).ToList();
            if (repeatedWrites.Any())
            {
                CreateMonitorEscalation(kobold, plan,
                    EscalationType.WrongApproach,
                    $"Stuck loop detected: {repeatedWrites.Count} file operation(s) repeated {_config.MaxFileWriteRepetitions}+ times");
            }
        }

        private void CheckStalledProgress(Kobold kobold, KoboldImplementationPlan plan)
        {
            // Check if no progress in configured timeout
            var lastStepUpdate = plan.Steps
                .Where(s => s.CompletedAt.HasValue)
                .Select(s => s.CompletedAt!.Value)
                .DefaultIfEmpty(plan.CreatedAt)
                .Max();

            var timeSinceProgress = DateTime.UtcNow - lastStepUpdate;
            if (timeSinceProgress.TotalMinutes >= _config.NoProgressTimeoutMinutes &&
                kobold.LastLlmResponseAt.HasValue &&
                (DateTime.UtcNow - kobold.LastLlmResponseAt.Value).TotalMinutes < _config.NoProgressTimeoutMinutes)
            {
                // LLM is responding but no step completions — likely stuck
                CreateMonitorEscalation(kobold, plan,
                    EscalationType.WrongApproach,
                    $"No step progress for {timeSinceProgress.TotalMinutes:F0} minutes despite active LLM responses");
            }
        }

        private void CheckRepeatedErrors(Kobold kobold, KoboldImplementationPlan plan)
        {
            var stallCount = _config.StallDetectionCount;
            var recentReflections = plan.Reflections.TakeLast(stallCount).ToList();

            if (recentReflections.Count < stallCount) return;

            // Check if all recent reflections have the same blocker
            var blockers = recentReflections
                .Where(r => !string.IsNullOrEmpty(r.Blockers))
                .Select(r => r.Blockers!)
                .ToList();

            if (blockers.Count >= stallCount && blockers.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            {
                CreateMonitorEscalation(kobold, plan,
                    EscalationType.WrongApproach,
                    $"Same blocker reported {stallCount} consecutive times: {blockers.First()}");
            }
        }

        private void CheckBudgetExhaustion(Kobold kobold, KoboldImplementationPlan plan)
        {
            var totalReflections = plan.Reflections.Count;
            if (totalReflections < 3) return; // Need at least a few reflections to judge

            var latestReflection = plan.Reflections.Last();
            var planProgress = plan.ProgressPercentage;

            // If >75% of expected iterations consumed but <50% progress
            var totalSteps = plan.Steps.Count;
            var completedSteps = plan.CompletedStepsCount;
            var expectedProgress = totalSteps > 0 ? (double)completedSteps / totalSteps * 100 : 0;

            if (totalReflections > 10 && expectedProgress < 50 && latestReflection.ConfidencePercent < 50)
            {
                CreateMonitorEscalation(kobold, plan,
                    EscalationType.NeedsSplit,
                    $"Budget concern: {completedSteps}/{totalSteps} steps ({expectedProgress:F0}%) after {totalReflections} reflections, confidence {latestReflection.ConfidencePercent}%");
            }
        }

        private void CreateMonitorEscalation(Kobold kobold, KoboldImplementationPlan plan, EscalationType type, string summary)
        {
            // Avoid duplicate escalations for the same issue
            var recentEscalations = plan.Escalations
                .Where(e => e.Source == EscalationSource.ReasoningMonitor &&
                           e.Type == type &&
                           (DateTime.UtcNow - e.CreatedAt).TotalMinutes < 5)
                .ToList();

            if (recentEscalations.Any())
            {
                _logger.LogDebug("Skipping duplicate monitor escalation for Kobold {KoboldId}: {Type}",
                    kobold.Id.ToString()[..8], type);
                return;
            }

            // The same kind of alert again on this task, after its automatic route already ran: a fix task too (TASK-223)
            var repeated = RepeatedKind(plan, summary);

            var alert = new EscalationAlert
            {
                ProjectId = kobold.ProjectId ?? plan.ProjectId,
                TaskId = kobold.TaskId?.ToString() ?? plan.TaskId,
                WorkerId = kobold.Id.ToString(),
                AgentType = kobold.AgentType,
                Source = EscalationSource.ReasoningMonitor,
                Type = type,
                Summary = summary,
                ReflectionHistory = plan.Reflections.TakeLast(5).ToList()
            };

            plan.Escalations.Add(alert);

            _logger.LogWarning(
                "ReasoningMonitor escalation for Kobold {KoboldId}: {Type} - {Summary}",
                kobold.Id.ToString()[..8], type, summary);

            // Route through Drake (with retry if Drake not yet available)
            if (!string.IsNullOrEmpty(kobold.ProjectId))
            {
                _ = RouteEscalationWithRetryAsync(kobold.ProjectId, alert, repeated);
            }
        }

        /// <summary>
        /// Routes an escalation alert to a Drake, retrying if no Drake is currently available.
        /// </summary>
        private Task RouteEscalationWithRetryAsync(string projectId, EscalationAlert alert, string? repeatedKind, int maxRetries = 3) =>
            RouteToHoldingDrakeAsync(alert, () => _drakeFactory.GetDrakesByProject(projectId), _logger, maxRetries,
                TimeSpan.FromSeconds(30), repeatedKind);

        /// <summary>
        /// The fix kind (<c>monitor-stuck-loop</c>, <c>monitor-stalled</c>, <c>monitor-repeated-blocker</c>,
        /// <c>monitor-budget</c>) when <paramref name="plan"/> already holds a monitor alert of the same kind as
        /// <paramref name="summary"/>, else null (TASK-223): the first alert of a kind takes its automatic route, a repeat
        /// is filed as a fix task as well.
        /// </summary>
        public static string? RepeatedKind(KoboldImplementationPlan plan, string summary)
        {
            if (MonitorKind(summary) is not { } kind)
                return null;
            return plan.Escalations.Any(e => e.Source == EscalationSource.ReasoningMonitor && MonitorKind(e.Summary) == kind)
                ? $"monitor-{kind}"
                : null;
        }

        // Each check's summary starts with its own words (CheckStuckLoop, CheckStalledProgress, CheckRepeatedErrors,
        // CheckBudgetExhaustion)
        private static string? MonitorKind(string? summary) => summary switch
        {
            not null when summary.StartsWith("Stuck loop", StringComparison.Ordinal) => "stuck-loop",
            not null when summary.StartsWith("No step progress", StringComparison.Ordinal) => "stalled",
            not null when summary.StartsWith("Same blocker", StringComparison.Ordinal) => "repeated-blocker",
            not null when summary.StartsWith("Budget concern", StringComparison.Ordinal) => "budget",
            _ => null
        };

        /// <summary>
        /// Hands a monitor escalation to the Drake whose tracker holds its task (TASK-169). A project has one Drake per
        /// task area, and every other Drake would answer "task not found" and do nothing, so the first Drake is never
        /// taken blindly. While none holds the task (none yet, or the task not loaded yet), it retries after
        /// <paramref name="retryDelay"/>; after the last attempt it logs a warning and routes nothing. Returns the Drake
        /// that handled the escalation, or null.
        /// </summary>
        internal static async Task<Drake?> RouteToHoldingDrakeAsync(EscalationAlert alert, Func<IReadOnlyList<Drake>> drakes,
            ILogger logger, int maxRetries, TimeSpan retryDelay, string? repeatedKind = null)
        {
            if (string.IsNullOrEmpty(alert.TaskId))
            {
                logger.LogWarning("Escalation {AlertId} ({Type}) for project {ProjectId} names no task; not routed",
                    alert.Id[..8], alert.Type, alert.ProjectId);
                return null;
            }

            var projectDrakes = 0;
            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                var current = drakes();
                projectDrakes = current.Count;
                var drake = current.FirstOrDefault(d => d.HoldsTask(alert.TaskId));
                if (drake != null)
                {
                    try
                    {
                        if (repeatedKind != null)
                            await drake.FileFixTaskAsync(alert.TaskId, repeatedKind, alert.Summary);
                        await drake.HandleEscalationAsync(alert);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to handle escalation {AlertId} via Drake for project {ProjectId}",
                            alert.Id[..8], alert.ProjectId);
                    }
                    return drake;
                }

                if (attempt < maxRetries - 1)
                {
                    logger.LogDebug("No Drake of project {ProjectId} holds task {TaskId} ({Count} Drakes), retrying escalation routing in {Delay}s (attempt {Attempt}/{Max})",
                        alert.ProjectId, alert.TaskId[..Math.Min(8, alert.TaskId.Length)], current.Count, retryDelay.TotalSeconds, attempt + 1, maxRetries);
                    await Task.Delay(retryDelay);
                }
            }

            logger.LogWarning(
                "No Drake of project {ProjectId} holds task {TaskId} after {MaxRetries} attempts ({Count} Drakes) - escalation {AlertId} ({Type}) was not routed. " +
                "Alert is preserved in plan and will be processed when its Drake is recreated.",
                alert.ProjectId, alert.TaskId[..Math.Min(8, alert.TaskId.Length)], maxRetries, projectDrakes, alert.Id[..8], alert.Type);
            return null;
        }
    }
}
