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
