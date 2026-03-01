namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Represents the implementation plan for a Kobold task.
    /// Plans are persisted to disk to enable resumability after server restarts.
    /// </summary>
    public class KoboldImplementationPlan
    {
        /// <summary>
        /// Links to the TaskRecord this plan is for
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Project identifier
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable filename for this plan (without extension).
        /// Generated from task description with uniqueness suffix.
        /// Example: "frontend-1-user-authentication-a7f3"
        /// </summary>
        public string? PlanFilename { get; set; }

        /// <summary>
        /// Original task description
        /// </summary>
        public string TaskDescription { get; set; } = string.Empty;

        /// <summary>
        /// When the plan was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the plan was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Current status of the plan
        /// </summary>
        public PlanStatus Status { get; set; } = PlanStatus.Planning;

        /// <summary>
        /// Implementation steps in order
        /// </summary>
        public List<ImplementationStep> Steps { get; set; } = new();

        /// <summary>
        /// Current step index for resumption (0-based)
        /// </summary>
        public int CurrentStepIndex { get; set; }

        /// <summary>
        /// Error message if the plan failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Execution log entries
        /// </summary>
        public List<PlanLogEntry> ExecutionLog { get; set; } = new();

        /// <summary>
        /// Lessons learned during execution - captured insights that could help future tasks
        /// CONTEXT SLIPPING FIX: This preserves qualitative knowledge between Kobold executions
        /// </summary>
        public List<string> LessonsLearned { get; set; } = new();

        /// <summary>
        /// Approaches that worked well during execution
        /// </summary>
        public List<string> SuccessfulPatterns { get; set; } = new();

        /// <summary>
        /// Issues encountered and how they were resolved
        /// </summary>
        public List<string> ResolvedIssues { get; set; } = new();

        /// <summary>
        /// Gets the current step being executed, or null if all complete
        /// </summary>
        public ImplementationStep? CurrentStep =>
            CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

        /// <summary>
        /// Gets whether the plan has more steps to execute
        /// </summary>
        public bool HasMoreSteps => CurrentStepIndex < Steps.Count;

        /// <summary>
        /// Gets the number of completed steps
        /// </summary>
        public int CompletedStepsCount =>
            Steps.Count(s => s.Status == StepStatus.Completed);

        /// <summary>
        /// Gets the progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage =>
            Steps.Count > 0 ? (CompletedStepsCount * 100) / Steps.Count : 0;

        /// <summary>
        /// Phase 4: Indices of steps assigned to specific Kobolds for parallel execution.
        /// Maps Kobold ID to list of step indices (0-based) that the Kobold should execute.
        /// Example: { "abc123": [0, 1, 5], "def456": [2, 3, 4] }
        /// </summary>
        public Dictionary<string, List<int>> AssignedStepIndices { get; set; } = new();

        /// <summary>
        /// Phase 4: Gets the steps assigned to a specific Kobold.
        /// Returns all steps if no assignment exists (for backward compatibility).
        /// </summary>
        /// <param name="koboldId">The Kobold's unique identifier</param>
        /// <returns>List of steps assigned to this Kobold</returns>
        public List<ImplementationStep> GetAssignedSteps(string koboldId)
        {
            if (!AssignedStepIndices.TryGetValue(koboldId, out var indices) || indices.Count == 0)
            {
                // No assignment - return all steps (backward compatible)
                return Steps;
            }

            return indices.Where(i => i >= 0 && i < Steps.Count).Select(i => Steps[i]).ToList();
        }

        /// <summary>
        /// Phase 4: Assigns specific steps to a Kobold for parallel execution.
        /// </summary>
        /// <param name="koboldId">The Kobold's unique identifier</param>
        /// <param name="stepIndices">List of 0-based step indices to assign</param>
        public void AssignStepsToKobold(string koboldId, List<int> stepIndices)
        {
            AssignedStepIndices[koboldId] = stepIndices;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Phase 4: Clears step assignments for a specific Kobold.
        /// </summary>
        /// <param name="koboldId">The Kobold's unique identifier</param>
        public void ClearStepAssignments(string koboldId)
        {
            AssignedStepIndices.Remove(koboldId);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Phase 4: Clears all step assignments.
        /// </summary>
        public void ClearAllStepAssignments()
        {
            AssignedStepIndices.Clear();
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Phase 3: Gets aggregated metrics for all steps
        /// </summary>
        public PlanExecutionMetrics GetAggregatedMetrics()
        {
            var metrics = new PlanExecutionMetrics
            {
                TotalSteps = Steps.Count,
                CompletedSteps = Steps.Count(s => s.Status == StepStatus.Completed),
                FailedSteps = Steps.Count(s => s.Status == StepStatus.Failed),
                SkippedSteps = Steps.Count(s => s.Status == StepStatus.Skipped),
                TotalIterations = Steps.Sum(s => s.Metrics.IterationsUsed),
                TotalEstimatedTokens = Steps.Sum(s => s.Metrics.EstimatedTokens),
                TotalDurationSeconds = Steps.Sum(s => s.DurationSeconds),
                AutoCompletedSteps = Steps.Count(s => s.Metrics.AutoCompleted),
                AverageIterationsPerStep = Steps.Where(s => s.Metrics.IterationsUsed > 0)
                    .Select(s => s.Metrics.IterationsUsed)
                    .DefaultIfEmpty(0)
                    .Average()
            };

            if (Steps.Count > 0)
            {
                metrics.SuccessRate = (double)metrics.CompletedSteps / Steps.Count * 100;
            }

            return metrics;
        }

        /// <summary>
        /// Adds a log entry to the execution log
        /// </summary>
        public void AddLogEntry(string message)
        {
            ExecutionLog.Add(new PlanLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Message = message
            });
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Advances to the next step
        /// </summary>
        public void AdvanceToNextStep()
        {
            if (CurrentStepIndex < Steps.Count)
            {
                CurrentStepIndex++;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Marks the plan as completed
        /// </summary>
        public void MarkCompleted()
        {
            Status = PlanStatus.Completed;
            UpdatedAt = DateTime.UtcNow;
            AddLogEntry("Plan completed successfully");

            // CONTEXT SLIPPING FIX: Capture lessons learned automatically on completion
            CaptureLessonsLearned();
        }

        /// <summary>
        /// Captures lessons learned from the completed execution.
        /// This preserves qualitative knowledge for future Kobolds.
        /// </summary>
        public void CaptureLessonsLearned()
        {
            // Capture patterns from steps that required retries but ultimately succeeded
            var retriedButSucceeded = Steps.Where(s => s.Status == StepStatus.Completed && s.RetryCount > 0);
            foreach (var step in retriedButSucceeded)
            {
                if (!string.IsNullOrEmpty(step.LastErrorMessage))
                {
                    var resolvedIssue = $"Step '{step.Title}' encountered: {step.LastErrorMessage}";
                    if (!ResolvedIssues.Contains(resolvedIssue))
                    {
                        ResolvedIssues.Add(resolvedIssue);
                    }
                }

                // If it took many iterations, note the pattern
                if (step.Metrics.IterationsUsed > 10)
                {
                    var pattern = $"Step '{step.Title}' required {step.Metrics.IterationsUsed} iterations - consider breaking down similar complex steps";
                    if (!LessonsLearned.Contains(pattern))
                    {
                        LessonsLearned.Add(pattern);
                    }
                }
            }

            // Capture successful patterns from steps that completed quickly
            var quickSteps = Steps.Where(s => s.Status == StepStatus.Completed && s.Metrics.IterationsUsed > 0 && s.Metrics.IterationsUsed <= 3);
            if (quickSteps.Count() > 1)
            {
                var quickPattern = $"Tasks of this type often complete in {quickSteps.Average(s => s.Metrics.IterationsUsed):F1} iterations per step";
                if (!SuccessfulPatterns.Contains(quickPattern))
                {
                    SuccessfulPatterns.Add(quickPattern);
                }
            }

            // Capture file creation patterns
            var totalFiles = Steps.Sum(s => s.FilesToCreate.Count + s.FilesToModify.Count);
            if (totalFiles > 0)
            {
                var filePattern = $"This task created/modified {totalFiles} files across {Steps.Count} steps";
                if (!LessonsLearned.Contains(filePattern))
                {
                    LessonsLearned.Add(filePattern);
                }
            }
        }

        /// <summary>
        /// Marks the plan as failed
        /// </summary>
        public void MarkFailed(string errorMessage)
        {
            Status = PlanStatus.Failed;
            ErrorMessage = errorMessage;
            UpdatedAt = DateTime.UtcNow;
            AddLogEntry($"Plan failed: {errorMessage}");
        }

        /// <summary>
        /// Phase 4: Intelligently reorders steps based on file dependencies.
        /// Uses StepDependencyAnalyzer to ensure steps are in optimal order.
        /// </summary>
        /// <param name="analyzer">The step dependency analyzer</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>True if steps were reordered, false if no changes were needed</returns>
        public bool ReorderSteps(Services.StepDependencyAnalyzer analyzer, Microsoft.Extensions.Logging.ILogger? logger = null)
        {
            if (Steps.Count <= 1)
            {
                return false; // No reordering needed for 0 or 1 steps
            }

            // Store original order for comparison
            var originalOrder = Steps.Select(s => s.Index).ToList();
            var originalTitles = Steps.Select(s => (s.Index, s.Title)).ToDictionary(t => t.Index, t => t.Title);

            try
            {
                // Get the optimal order from the analyzer
                var reorderedSteps = analyzer.SuggestOptimalOrder(Steps);

                // Check if the order actually changed
                bool orderChanged = reorderedSteps.Count != Steps.Count;
                if (!orderChanged)
                {
                    for (int i = 0; i < reorderedSteps.Count; i++)
                    {
                        if (reorderedSteps[i].Index != Steps[i].Index)
                        {
                            orderChanged = true;
                            break;
                        }
                    }
                }

                if (!orderChanged)
                {
                    // No reordering needed
                    return false;
                }

                // Rebuild the steps list with new indices
                Steps.Clear();
                for (int i = 0; i < reorderedSteps.Count; i++)
                {
                    var step = reorderedSteps[i];
                    step.Index = i + 1; // Update to new 1-based index
                    Steps.Add(step);
                }

                // Log the reordering
                var reorderingLog = new System.Text.StringBuilder();
                reorderingLog.AppendLine("Steps reordered for optimal execution:");
                foreach (var step in Steps)
                {
                    var originalIndex = originalOrder.FirstOrDefault(o => originalTitles.ContainsKey(o) && originalTitles[o].Equals(step.Title, StringComparison.OrdinalIgnoreCase));
                    if (originalIndex != 0 && originalIndex != step.Index)
                    {
                        reorderingLog.AppendLine($"  Step {step.Index}: '{step.Title}' (was {originalIndex})");
                    }
                    else
                    {
                        reorderingLog.AppendLine($"  Step {step.Index}: '{step.Title}'");
                    }
                }

                logger?.LogInformation("Plan {PlanFilename}: {Reason}", PlanFilename ?? "unknown", reorderingLog.ToString());
                AddLogEntry("Steps reordered based on dependencies");

                UpdatedAt = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Plan {PlanFilename}: Failed to reorder steps, continuing with original order",
                    PlanFilename ?? "unknown");
                AddLogEntry($"Step reordering failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Phase 4: Validates step ordering for dependency violations.
        /// </summary>
        /// <param name="analyzer">The step dependency analyzer</param>
        /// <returns>List of dependency violations found, or empty if valid</returns>
        public List<string> ValidateStepOrdering(Services.StepDependencyAnalyzer analyzer)
        {
            var violations = new List<string>();

            for (int i = 0; i < Steps.Count; i++)
            {
                var currentStep = Steps[i];

                // Check if this step depends on any later step (violation)
                for (int j = i + 1; j < Steps.Count; j++)
                {
                    var laterStep = Steps[j];
                    if (analyzer.HasDependency(currentStep, laterStep))
                    {
                        // Current step depends on a step that comes after it
                        var dependencyFiles = currentStep.FilesToModify
                            .Where(f => laterStep.FilesToCreate.Contains(f));

                        violations.Add(
                            $"Step {currentStep.Index} ('{currentStep.Title}') depends on " +
                            $"Step {laterStep.Index} ('{laterStep.Title}') " +
                            $"via files: {string.Join(", ", dependencyFiles)}");
                        }
                    }
                }

            return violations;
        }
    }

    /// <summary>
    /// Represents a single step in an implementation plan
    /// </summary>
    public class ImplementationStep
    {
        /// <summary>
        /// Step number (1-based for display)
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Short description of the step
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what to do
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Current status of this step
        /// </summary>
        public StepStatus Status { get; set; } = StepStatus.Pending;

        /// <summary>
        /// Files expected to be created in this step
        /// </summary>
        public List<string> FilesToCreate { get; set; } = new();

        /// <summary>
        /// Files expected to be modified in this step
        /// </summary>
        public List<string> FilesToModify { get; set; } = new();

        /// <summary>
        /// Execution result summary
        /// </summary>
        public string? Output { get; set; }

        /// <summary>
        /// When the step started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the step completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        // Step-level retry tracking

        /// <summary>
        /// Number of retry attempts made for this step
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Maximum retry attempts for transient errors (default: 3)
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Last error message (preserved across retries)
        /// </summary>
        public string? LastErrorMessage { get; set; }

        /// <summary>
        /// Error category from ErrorClassifier (Transient, Permanent, Unknown)
        /// </summary>
        public string? ErrorCategory { get; set; }

        /// <summary>
        /// Key identifiers/strings expected in output files after this step completes.
        /// Used by ContentExpectationValidator during auto-advancement to verify
        /// the step's work was actually done (not just that files were touched).
        /// Populated by KoboldPlannerAgent. Example: ["GetUserById", "async Task&lt;User&gt;"]
        /// </summary>
        public List<string> ExpectedContent { get; set; } = new();

        /// <summary>
        /// Phase 3: Step execution metrics for telemetry
        /// </summary>
        public StepExecutionMetrics Metrics { get; set; } = new();

        /// <summary>
        /// Gets the duration of step execution in seconds (0 if not started/completed)
        /// </summary>
        public double DurationSeconds =>
            StartedAt.HasValue && CompletedAt.HasValue
                ? (CompletedAt.Value - StartedAt.Value).TotalSeconds
                : 0;

        /// <summary>
        /// Marks the step as started
        /// </summary>
        public void Start()
        {
            Status = StepStatus.InProgress;
            StartedAt = DateTime.UtcNow;
            Metrics.StartTime = StartedAt.Value;
        }

        /// <summary>
        /// Marks the step as completed
        /// </summary>
        public void Complete(string? output = null)
        {
            Status = StepStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            Output = output;
            Metrics.EndTime = CompletedAt.Value;
        }

        /// <summary>
        /// Marks the step as failed
        /// </summary>
        public void Fail(string? output = null)
        {
            Status = StepStatus.Failed;
            CompletedAt = DateTime.UtcNow;
            Output = output;
            Metrics.EndTime = CompletedAt.Value;
            Metrics.Failed = true;
        }

        /// <summary>
        /// Marks the step as skipped
        /// </summary>
        public void Skip(string? reason = null)
        {
            Status = StepStatus.Skipped;
            CompletedAt = DateTime.UtcNow;
            Output = reason;
            Metrics.EndTime = CompletedAt;
            Metrics.Skipped = true;
        }
    }

    /// <summary>
    /// Phase 3: Telemetry metrics for step execution
    /// </summary>
    public class StepExecutionMetrics
    {
        /// <summary>
        /// When the step started execution
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// When the step finished execution
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Number of agent iterations used for this step
        /// </summary>
        public int IterationsUsed { get; set; }

        /// <summary>
        /// Estimated token count for this step (input + output)
        /// </summary>
        public int EstimatedTokens { get; set; }

        /// <summary>
        /// Whether the step failed
        /// </summary>
        public bool Failed { get; set; }

        /// <summary>
        /// Whether the step was skipped
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Whether the step was auto-completed (agent didn't mark explicitly)
        /// </summary>
        public bool AutoCompleted { get; set; }

        /// <summary>
        /// Number of validation attempts
        /// </summary>
        public int ValidationAttempts { get; set; }

        /// <summary>
        /// Duration in seconds (calculated property)
        /// </summary>
        public double DurationSeconds =>
            StartTime.HasValue && EndTime.HasValue
                ? (EndTime.Value - StartTime.Value).TotalSeconds
                : 0;
    }

    /// <summary>
    /// Phase 3: Aggregated metrics for entire plan execution
    /// </summary>
    public class PlanExecutionMetrics
    {
        public int TotalSteps { get; set; }
        public int CompletedSteps { get; set; }
        public int FailedSteps { get; set; }
        public int SkippedSteps { get; set; }
        public int TotalIterations { get; set; }
        public int TotalEstimatedTokens { get; set; }
        public double TotalDurationSeconds { get; set; }
        public int AutoCompletedSteps { get; set; }
        public double AverageIterationsPerStep { get; set; }
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// Log entry for plan execution
    /// </summary>
    public class PlanLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status of an implementation plan
    /// </summary>
    public enum PlanStatus
    {
        /// <summary>
        /// Plan is being generated
        /// </summary>
        Planning,

        /// <summary>
        /// Plan is ready for execution
        /// </summary>
        Ready,

        /// <summary>
        /// Plan is being executed
        /// </summary>
        InProgress,

        /// <summary>
        /// Plan has been completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Plan execution failed
        /// </summary>
        Failed
    }

    /// <summary>
    /// Status of an implementation step
    /// </summary>
    public enum StepStatus
    {
        /// <summary>
        /// Step has not started
        /// </summary>
        Pending,

        /// <summary>
        /// Step is currently being executed
        /// </summary>
        InProgress,

        /// <summary>
        /// Step completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Step was skipped
        /// </summary>
        Skipped,

        /// <summary>
        /// Step failed
        /// </summary>
        Failed
    }
}
