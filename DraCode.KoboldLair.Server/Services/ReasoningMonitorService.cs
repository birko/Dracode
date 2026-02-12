using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors Kobold reflection patterns and detects concerning behaviors.
    /// Uses data from ReflectionTool checkpoints to identify:
    /// - Declining confidence trends
    /// - Repeated file modifications (stuck loops)
    /// - Stalled progress
    /// - High blocker counts
    /// - Explicit escalation requests
    ///
    /// When critical patterns are detected, can automatically mark Kobolds as stuck
    /// for Drake to handle on the next monitoring cycle.
    /// </summary>
    public class ReasoningMonitorService : BackgroundService
    {
        private readonly ILogger<ReasoningMonitorService> _logger;
        private readonly DrakeFactory _drakeFactory;
        private readonly SharedPlanningContextService _sharedPlanningContext;
        private readonly ProjectService _projectService;
        private readonly KoboldPlanService _planService;
        private readonly ReasoningMonitorConfiguration _config;

        public ReasoningMonitorService(
            ILogger<ReasoningMonitorService> logger,
            DrakeFactory drakeFactory,
            SharedPlanningContextService sharedPlanningContext,
            ProjectService projectService,
            KoboldPlanService planService,
            IOptions<KoboldLairConfiguration> config)
        {
            _logger = logger;
            _drakeFactory = drakeFactory;
            _sharedPlanningContext = sharedPlanningContext;
            _projectService = projectService;
            _planService = planService;
            _config = config.Value.ReasoningMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.Enabled)
            {
                _logger.LogInformation("Reasoning Monitor Service is disabled via configuration");
                return;
            }

            _logger.LogInformation(
                "Reasoning Monitor Service started with {Interval}s interval, AutoIntervention: {AutoIntervention}",
                _config.MonitoringIntervalSeconds, _config.AutoInterventionEnabled);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorAllProjectsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Reasoning Monitor cycle");
                }

                await Task.Delay(TimeSpan.FromSeconds(_config.MonitoringIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("Reasoning Monitor Service stopped");
        }

        private async Task MonitorAllProjectsAsync(CancellationToken stoppingToken)
        {
            var projects = _projectService.GetAllProjects()
                .Where(p => p.Status == ProjectStatus.InProgress ||
                           p.Status == ProjectStatus.Analyzed)
                .ToList();

            foreach (var project in projects)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await MonitorProjectAsync(project.Id, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error monitoring project {ProjectId}", project.Id);
                }
            }
        }

        private async Task MonitorProjectAsync(string projectId, CancellationToken stoppingToken)
        {
            // Get active agents for this project
            var activeAgents = await _sharedPlanningContext.GetActiveAgentsAsync(projectId);

            foreach (var agent in activeAgents)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    // Get reflections for this agent's task
                    var reflections = await _sharedPlanningContext.GetTaskReflectionsAsync(projectId, agent.TaskId);

                    if (reflections.Count == 0)
                    {
                        // No reflections yet, nothing to analyze
                        continue;
                    }

                    // Analyze patterns
                    var patterns = AnalyzePatterns(reflections, agent);

                    // Log any concerning patterns
                    foreach (var pattern in patterns)
                    {
                        LogPattern(pattern, agent);

                        // If critical and auto-intervention enabled, mark Kobold as stuck
                        if (_config.AutoInterventionEnabled && pattern.Severity == PatternSeverity.Critical)
                        {
                            await HandleCriticalPatternAsync(pattern, agent, projectId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error analyzing reflections for agent {AgentId} in project {ProjectId}",
                        agent.AgentId, projectId);
                }
            }
        }

        private List<DetectedPattern> AnalyzePatterns(List<ReflectionSignal> reflections, AgentPlanningContext agent)
        {
            var patterns = new List<DetectedPattern>();

            if (reflections.Count == 0)
                return patterns;

            var latestReflection = reflections[^1];
            var recentReflections = reflections.TakeLast(_config.DecliningConfidenceCheckpoints).ToList();

            // Pattern 1: Explicit escalation request
            if (latestReflection.Decision == ReflectionDecision.Escalate)
            {
                patterns.Add(new DetectedPattern
                {
                    Type = PatternType.ExplicitEscalation,
                    Severity = PatternSeverity.Critical,
                    Message = "Agent explicitly requested escalation",
                    Confidence = latestReflection.Confidence,
                    SourceReflection = latestReflection
                });
            }

            // Pattern 2: Low confidence
            if (latestReflection.Confidence < _config.LowConfidenceInterventionThreshold)
            {
                patterns.Add(new DetectedPattern
                {
                    Type = PatternType.LowConfidence,
                    Severity = PatternSeverity.Warning,
                    Message = $"Confidence below threshold ({latestReflection.Confidence}% < {_config.LowConfidenceInterventionThreshold}%)",
                    Confidence = latestReflection.Confidence,
                    SourceReflection = latestReflection
                });
            }

            // Pattern 3: Declining confidence
            if (recentReflections.Count >= _config.DecliningConfidenceCheckpoints)
            {
                var firstConfidence = recentReflections[0].Confidence;
                var lastConfidence = recentReflections[^1].Confidence;
                var confidenceDrop = firstConfidence - lastConfidence;

                if (confidenceDrop >= _config.DecliningConfidenceDropThreshold)
                {
                    patterns.Add(new DetectedPattern
                    {
                        Type = PatternType.DecliningConfidence,
                        Severity = PatternSeverity.Warning,
                        Message = $"Confidence dropped {confidenceDrop}% over {recentReflections.Count} checkpoints ({firstConfidence}% -> {lastConfidence}%)",
                        Confidence = lastConfidence,
                        SourceReflection = latestReflection
                    });
                }
            }

            // Pattern 4: Multiple blockers
            if (latestReflection.Blockers.Count >= _config.HighBlockerThreshold)
            {
                patterns.Add(new DetectedPattern
                {
                    Type = PatternType.MultipleBlockers,
                    Severity = PatternSeverity.Warning,
                    Message = $"{latestReflection.Blockers.Count} blockers reported: {string.Join(", ", latestReflection.Blockers.Take(3))}",
                    Confidence = latestReflection.Confidence,
                    SourceReflection = latestReflection
                });
            }

            // Pattern 5: Stalled progress
            if (recentReflections.Count >= _config.StalledProgressCheckpoints)
            {
                var stalledCheckpoints = recentReflections.TakeLast(_config.StalledProgressCheckpoints).ToList();
                if (stalledCheckpoints.All(r => r.ProgressPercent == 0))
                {
                    patterns.Add(new DetectedPattern
                    {
                        Type = PatternType.StalledProgress,
                        Severity = PatternSeverity.Critical,
                        Message = $"No progress for {_config.StalledProgressCheckpoints} consecutive checkpoints",
                        Confidence = latestReflection.Confidence,
                        SourceReflection = latestReflection
                    });
                }
            }

            // Pattern 6: Repeated file modifications (detect stuck loops)
            var fileModificationCounts = reflections
                .SelectMany(r => r.FilesDone)
                .GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count());

            var repeatedFiles = fileModificationCounts
                .Where(kvp => kvp.Value >= _config.RepeatedFileModificationThreshold)
                .ToList();

            if (repeatedFiles.Any())
            {
                var mostRepeated = repeatedFiles.OrderByDescending(kvp => kvp.Value).First();
                patterns.Add(new DetectedPattern
                {
                    Type = PatternType.RepeatedFileModifications,
                    Severity = PatternSeverity.Critical,
                    Message = $"File '{mostRepeated.Key}' modified {mostRepeated.Value} times (possible stuck loop)",
                    Confidence = latestReflection.Confidence,
                    SourceReflection = latestReflection
                });
            }

            return patterns;
        }

        private void LogPattern(DetectedPattern pattern, AgentPlanningContext agent)
        {
            var logLevel = pattern.Severity switch
            {
                PatternSeverity.Critical => LogLevel.Error,
                PatternSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information
            };

            var severityIcon = pattern.Severity switch
            {
                PatternSeverity.Critical => "🚨",
                PatternSeverity.Warning => "⚠️",
                _ => "ℹ️"
            };

            _logger.Log(logLevel,
                "{Icon} Reasoning Monitor: {PatternType} detected for agent {AgentId} (task {TaskId})\n" +
                "  Message: {Message}\n" +
                "  Confidence: {Confidence}%",
                severityIcon, pattern.Type, agent.AgentId[..8], agent.TaskId[..8],
                pattern.Message, pattern.Confidence);
        }

        private async Task HandleCriticalPatternAsync(DetectedPattern pattern, AgentPlanningContext agent, string projectId)
        {
            _logger.LogWarning(
                "🛑 Auto-intervention triggered for agent {AgentId} - Pattern: {PatternType}, Task: {TaskId}",
                agent.AgentId[..8], pattern.Type, agent.TaskId[..8]);

            // Get the Drake(s) for this project
            var drakes = _drakeFactory.GetDrakesByProject(projectId);
            if (drakes.Count == 0)
            {
                _logger.LogWarning("No Drake found for project {ProjectId}, cannot intervene", projectId);
                return;
            }

            // Find the Kobold in any of the project's Drakes using the task ID
            Kobold? kobold = null;
            foreach (var drake in drakes)
            {
                kobold = drake.GetKoboldForTask(agent.TaskId);
                if (kobold != null) break;
            }
            if (kobold == null)
            {
                _logger.LogWarning("Kobold {KoboldId} not found in Drake, cannot intervene", agent.AgentId);
                return;
            }

            // Mark as stuck if it's working
            if (kobold.Status == KoboldStatus.Working)
            {
                // Get the plan to add log entry
                var plan = kobold.ImplementationPlan;
                if (plan != null)
                {
                    var interventionReason = pattern.Type switch
                    {
                        PatternType.ExplicitEscalation => "Agent requested escalation",
                        PatternType.StalledProgress => "No progress detected",
                        PatternType.RepeatedFileModifications => "Stuck loop detected",
                        PatternType.DecliningConfidence => "Confidence declining rapidly",
                        PatternType.MultipleBlockers => "Too many blockers",
                        PatternType.LowConfidence => "Confidence too low",
                        _ => "Concerning pattern detected"
                    };

                    plan.AddLogEntry($"⚠️ Reasoning Monitor Intervention: {interventionReason}. Pattern: {pattern.Message}");

                    try
                    {
                        await _planService.SavePlanAsync(plan);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save plan with intervention entry");
                    }
                }

                // Mark the Kobold as stuck
                var workingDuration = DateTime.UtcNow - (kobold.StartedAt ?? DateTime.UtcNow);
                kobold.MarkAsStuck(workingDuration, TimeSpan.Zero); // Zero timeout since we're forcing it

                _logger.LogInformation(
                    "Kobold {KoboldId} marked as stuck by Reasoning Monitor. Drake will recover on next cycle.",
                    kobold.Id.ToString()[..8]);
            }
        }
    }

    /// <summary>
    /// Types of concerning patterns the monitor can detect
    /// </summary>
    public enum PatternType
    {
        ExplicitEscalation,
        LowConfidence,
        DecliningConfidence,
        MultipleBlockers,
        StalledProgress,
        RepeatedFileModifications
    }

    /// <summary>
    /// Severity levels for detected patterns
    /// </summary>
    public enum PatternSeverity
    {
        /// <summary>
        /// Informational only, no action needed
        /// </summary>
        Info,

        /// <summary>
        /// Concerning but not yet critical
        /// </summary>
        Warning,

        /// <summary>
        /// Requires immediate attention/intervention
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents a detected concerning pattern in Kobold execution
    /// </summary>
    public class DetectedPattern
    {
        public PatternType Type { get; set; }
        public PatternSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Confidence { get; set; }
        public ReflectionSignal? SourceReflection { get; set; }
    }
}
