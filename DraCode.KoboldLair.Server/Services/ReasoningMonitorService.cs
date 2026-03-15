using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;

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

            var alert = new EscalationAlert
            {
                ProjectId = kobold.ProjectId ?? plan.ProjectId,
                TaskId = kobold.TaskId?.ToString() ?? plan.TaskId,
                KoboldId = kobold.Id,
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
                _ = RouteEscalationWithRetryAsync(kobold.ProjectId, alert);
            }
        }

        /// <summary>
        /// Routes an escalation alert to a Drake, retrying if no Drake is currently available.
        /// </summary>
        private async Task RouteEscalationWithRetryAsync(string projectId, EscalationAlert alert, int maxRetries = 3)
        {
            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                var drakes = _drakeFactory.GetDrakesByProject(projectId);
                var drake = drakes.FirstOrDefault();
                if (drake != null)
                {
                    try
                    {
                        await drake.HandleEscalationAsync(alert);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to handle escalation {AlertId} via Drake for project {ProjectId}",
                            alert.Id[..8], projectId);
                    }
                    return;
                }

                if (attempt < maxRetries - 1)
                {
                    _logger.LogDebug("No Drake found for project {ProjectId}, retrying escalation routing in 30s (attempt {Attempt}/{Max})",
                        projectId, attempt + 1, maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }

            _logger.LogWarning(
                "No Drake found for project {ProjectId} after {MaxRetries} attempts - escalation {AlertId} ({Type}) could not be routed. " +
                "Alert is preserved in plan and will be processed when Drake is recreated.",
                projectId, maxRetries, alert.Id[..8], alert.Type);
        }
    }
}
