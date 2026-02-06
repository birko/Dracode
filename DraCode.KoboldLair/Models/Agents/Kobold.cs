using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Represents a Kobold worker agent that works on a specific task.
    /// Each Kobold is dedicated to one task and tracks its own status.
    /// Supports implementation plans for resumability and visibility.
    /// </summary>
    public class Kobold
    {
        private readonly ILogger<Kobold>? _logger;
        /// <summary>
        /// Unique identifier for this Kobold
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// The agent instance created by KoboldLairAgentFactory
        /// </summary>
        public Agent.Agents.Agent Agent { get; }

        /// <summary>
        /// Type of agent (e.g., "csharp", "javascript", "react")
        /// </summary>
        public string AgentType { get; }

        /// <summary>
        /// Project identifier this Kobold belongs to
        /// </summary>
        public string? ProjectId { get; private set; }

        /// <summary>
        /// Task identifier this Kobold is assigned to (null if unassigned)
        /// </summary>
        public Guid? TaskId { get; private set; }

        /// <summary>
        /// Task description this Kobold is working on
        /// </summary>
        public string? TaskDescription { get; private set; }

        /// <summary>
        /// Specification context for the task (project requirements, architecture, etc.)
        /// </summary>
        public string? SpecificationContext { get; private set; }

        /// <summary>
        /// Current status of this Kobold
        /// </summary>
        public KoboldStatus Status { get; private set; }

        /// <summary>
        /// Timestamp when the Kobold was created
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Timestamp when the Kobold was assigned to a task
        /// </summary>
        public DateTime? AssignedAt { get; private set; }

        /// <summary>
        /// Timestamp when the Kobold started working
        /// </summary>
        public DateTime? StartedAt { get; private set; }

        /// <summary>
        /// Timestamp when the Kobold completed the task
        /// </summary>
        public DateTime? CompletedAt { get; private set; }

        /// <summary>
        /// Error message if the Kobold encountered an error
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Implementation plan for this task (optional, for resumability)
        /// </summary>
        public KoboldImplementationPlan? ImplementationPlan { get; private set; }

        /// <summary>
        /// Creates a new Kobold with an agent instance
        /// </summary>
        public Kobold(Agent.Agents.Agent agent, string agentType, ILogger<Kobold>? logger = null)
        {
            Id = Guid.NewGuid();
            Agent = agent;
            AgentType = agentType;
            Status = KoboldStatus.Unassigned;
            CreatedAt = DateTime.UtcNow;
            _logger = logger;
            
            _logger?.LogInformation("Kobold {KoboldId} created with agent type {AgentType} at {CreatedAt:o}", 
                Id.ToString()[..8], agentType, CreatedAt);
        }

        /// <summary>
        /// Assigns this Kobold to a specific task
        /// </summary>
        /// <param name="taskId">Task identifier</param>
        /// <param name="taskDescription">Description of the task to execute</param>
        /// <param name="projectId">Project identifier this task belongs to</param>
        /// <param name="specificationContext">Optional specification context for the task</param>
        public void AssignTask(Guid taskId, string taskDescription, string? projectId = null, string? specificationContext = null)
        {
            if (Status != KoboldStatus.Unassigned)
            {
                throw new InvalidOperationException($"Cannot assign task to Kobold {Id} - current status is {Status}");
            }

            TaskId = taskId;
            TaskDescription = taskDescription;
            ProjectId = projectId;
            SpecificationContext = specificationContext;
            Status = KoboldStatus.Assigned;
            AssignedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Starts working on the assigned task and executes it through the underlying agent.
        /// Automatically transitions to Done on success or captures error on failure.
        /// </summary>
        /// <param name="maxIterations">Maximum iterations for agent execution</param>
        /// <returns>Messages from agent execution</returns>
        public async Task<List<Message>> StartWorkingAsync(int maxIterations = 30)
        {
            if (Status != KoboldStatus.Assigned)
            {
                throw new InvalidOperationException($"Cannot start working - Kobold {Id} is not assigned (current status: {Status})");
            }

            if (string.IsNullOrEmpty(TaskDescription))
            {
                throw new InvalidOperationException($"Cannot start working - Kobold {Id} has no task description");
            }

            Status = KoboldStatus.Working;
            StartedAt = DateTime.UtcNow;

            try
            {
                // Build the full task prompt with specification context if available
                var fullTaskPrompt = TaskDescription;
                
                if (!string.IsNullOrEmpty(SpecificationContext))
                {
                    fullTaskPrompt = $@"# Task Context

You are working on a task that is part of a larger project. Below is the project specification that provides important context for this task.

## Project Specification

{SpecificationContext}

---

## Your Task

{TaskDescription}

**Important**: Keep the project specification in mind while completing this task. Ensure your implementation aligns with the overall project requirements and architecture described above.";
                }

                // Execute the task through the underlying agent in non-interactive mode
                var messages = await Agent.RunAsync(fullTaskPrompt, maxIterations);

                // Check if execution encountered errors
                if (HasErrorInMessages(messages))
                {
                    ErrorMessage = "Agent encountered errors during execution. Check logs for details.";
                    Status = KoboldStatus.Failed;
                    CompletedAt = DateTime.UtcNow;
                    
                    var taskPreview = TaskDescription?.Length > 60 
                        ? TaskDescription.Substring(0, 60) + "..." 
                        : TaskDescription ?? "unknown";
                    _logger?.LogWarning(
                        "Kobold {KoboldId} failed with errors\n" +
                        "  Project: {ProjectId}\n" +
                        "  Task ID: {TaskId}\n" +
                        "  Task: {TaskDescription}",
                        Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown", taskPreview);
                    return messages;
                }

                // Task completed successfully - transition to Done
                Status = KoboldStatus.Done;
                CompletedAt = DateTime.UtcNow;

                return messages;
            }
            catch (Exception ex)
            {
                // Task failed - capture error and transition to Failed
                ErrorMessage = ex.Message;
                Status = KoboldStatus.Failed;
                CompletedAt = DateTime.UtcNow;
                
                var taskPreview = TaskDescription?.Length > 60 
                    ? TaskDescription.Substring(0, 60) + "..." 
                    : TaskDescription ?? "unknown";
                _logger?.LogError(ex, 
                    "Kobold {KoboldId} failed with exception\n" +
                    "  Project: {ProjectId}\n" +
                    "  Task ID: {TaskId}\n" +
                    "  Task: {TaskDescription}",
                    Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown", taskPreview);
                throw; // Re-throw so caller knows it failed
            }
        }

        /// <summary>
        /// Ensures an implementation plan exists for this task.
        /// Loads existing plan from disk or creates a new one using the planner agent.
        /// </summary>
        /// <param name="planService">Service for plan persistence</param>
        /// <param name="planner">Planner agent for creating new plans</param>
        /// <returns>The loaded or created implementation plan</returns>
        public async Task<KoboldImplementationPlan> EnsurePlanAsync(
            KoboldPlanService planService,
            KoboldPlannerAgent planner)
        {
            if (string.IsNullOrEmpty(ProjectId) || !TaskId.HasValue)
            {
                throw new InvalidOperationException("Cannot ensure plan - Kobold must be assigned to a task first");
            }

            if (string.IsNullOrEmpty(TaskDescription))
            {
                throw new InvalidOperationException("Cannot ensure plan - Kobold has no task description");
            }

            var taskIdStr = TaskId.Value.ToString();

            // Try to load existing plan
            var existing = await planService.LoadPlanAsync(ProjectId, taskIdStr);
            if (existing != null && existing.Status != PlanStatus.Failed)
            {
                ImplementationPlan = existing;
                return existing;
            }

            // Create new plan using the planner agent
            var plan = await planner.CreatePlanAsync(TaskDescription, SpecificationContext);
            plan.TaskId = taskIdStr;
            plan.ProjectId = ProjectId;

            // Save the plan
            await planService.SavePlanAsync(plan);
            ImplementationPlan = plan;

            return plan;
        }

        /// <summary>
        /// Starts working on the assigned task with plan awareness.
        /// If a plan exists, includes the plan in the prompt and tracks progress.
        /// Injects the update_plan_step tool to allow the agent to mark steps as completed.
        /// </summary>
        /// <param name="planService">Service for plan persistence (can be null to skip plan updates)</param>
        /// <param name="maxIterations">Maximum iterations for agent execution</param>
        /// <returns>Messages from agent execution</returns>
        public async Task<List<Message>> StartWorkingWithPlanAsync(
            KoboldPlanService? planService,
            int maxIterations = 30)
        {
            if (Status != KoboldStatus.Assigned)
            {
                throw new InvalidOperationException($"Cannot start working - Kobold {Id} is not assigned (current status: {Status})");
            }

            if (string.IsNullOrEmpty(TaskDescription))
            {
                throw new InvalidOperationException($"Cannot start working - Kobold {Id} has no task description");
            }

            Status = KoboldStatus.Working;
            StartedAt = DateTime.UtcNow;

            // If we have a plan, inject the update_plan_step tool and register context
            bool toolInjected = false;
            if (ImplementationPlan != null)
            {
                // Register the plan context for the tool
                UpdatePlanStepTool.RegisterContext(ImplementationPlan, planService, _logger);

                // Add the tool to the agent
                Agent.AddTool(new UpdatePlanStepTool());
                toolInjected = true;
            }

            try
            {
                // Build the full task prompt with plan context if available
                var fullTaskPrompt = BuildFullPromptWithPlan();

                // Update plan status
                if (ImplementationPlan != null)
                {
                    ImplementationPlan.Status = PlanStatus.InProgress;
                    ImplementationPlan.AddLogEntry($"Kobold {Id.ToString()[..8]} started working");
                    if (planService != null && !string.IsNullOrEmpty(ProjectId))
                    {
                        await planService.SavePlanAsync(ImplementationPlan);
                    }
                }

                // Execute the task through the underlying agent
                var messages = await Agent.RunAsync(fullTaskPrompt, maxIterations);

                // Check if execution encountered errors
                if (HasErrorInMessages(messages))
                {
                    ErrorMessage = "Agent encountered errors during execution. Check logs for details.";
                    Status = KoboldStatus.Failed;
                    CompletedAt = DateTime.UtcNow;
                    
                    // Update plan status on failure
                    await UpdatePlanStatusAsync(planService, success: false, ErrorMessage);
                    
                    var taskPreview = TaskDescription?.Length > 60 
                        ? TaskDescription.Substring(0, 60) + "..." 
                        : TaskDescription ?? "unknown";
                    _logger?.LogWarning(
                        "Kobold {KoboldId} failed with errors\n" +
                        "  Project: {ProjectId}\n" +
                        "  Task ID: {TaskId}\n" +
                        "  Task: {TaskDescription}",
                        Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown", taskPreview);
                    return messages;
                }

                // Update plan status on completion
                await UpdatePlanStatusAsync(planService, success: true);

                // Check if we have a plan and if all steps are complete
                bool allStepsComplete = ImplementationPlan == null || 
                    ImplementationPlan.Steps.All(s =>
                        s.Status == StepStatus.Completed ||
                        s.Status == StepStatus.Skipped ||
                        s.Status == StepStatus.Failed);

                if (allStepsComplete)
                {
                    // Task completed successfully - transition to Done
                    Status = KoboldStatus.Done;
                    CompletedAt = DateTime.UtcNow;
                    var taskPreview = TaskDescription?.Length > 60 
                        ? TaskDescription.Substring(0, 60) + "..." 
                        : TaskDescription ?? "unknown";
                    _logger?.LogInformation(
                        "✓ Kobold {KoboldId} completed successfully\n" +
                        "  Project: {ProjectId}\n" +
                        "  Task ID: {TaskId}\n" +
                        "  Task: {TaskDescription}\n" +
                        "  Agent Type: {AgentType}",
                        Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown", 
                        taskPreview, AgentType);
                }
                else
                {
                    // Plan has unfinished steps - keep in Working state for resumption
                    // CompletedAt remains null to indicate work is not finished
                    var taskPreview = TaskDescription?.Length > 60 
                        ? TaskDescription.Substring(0, 60) + "..." 
                        : TaskDescription ?? "unknown";
                    _logger?.LogWarning(
                        "⚠ Kobold {KoboldId} incomplete - keeping in Working state\n" +
                        "  Project: {ProjectId}\n" +
                        "  Task ID: {TaskId}\n" +
                        "  Task: {TaskDescription}\n" +
                        "  Plan Progress: {Completed}/{Total} steps",
                        Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown",
                        taskPreview, ImplementationPlan?.CompletedStepsCount ?? 0, ImplementationPlan?.Steps.Count ?? 0);
                }

                return messages;
            }
            catch (Exception ex)
            {
                // Update plan status on failure
                await UpdatePlanStatusAsync(planService, success: false, ex.Message);

                // Task failed - capture error and transition to Failed
                ErrorMessage = ex.Message;
                Status = KoboldStatus.Failed;
                CompletedAt = DateTime.UtcNow;
                
                var taskPreview = TaskDescription?.Length > 60 
                    ? TaskDescription.Substring(0, 60) + "..." 
                    : TaskDescription ?? "unknown";
                _logger?.LogError(ex, 
                    "Kobold {KoboldId} failed with exception\n" +
                    "  Project: {ProjectId}\n" +
                    "  Task ID: {TaskId}\n" +
                    "  Task: {TaskDescription}",
                    Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown", taskPreview);
                throw;
            }
            finally
            {
                // Clean up: remove tool and clear context
                if (toolInjected)
                {
                    Agent.RemoveTool("update_plan_step");
                    UpdatePlanStepTool.ClearContext();
                }
            }
        }

        /// <summary>
        /// Builds the full task prompt including plan context if available
        /// </summary>
        private string BuildFullPromptWithPlan()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("# Task Context");
            sb.AppendLine();
            sb.AppendLine("You are working on a task that is part of a larger project.");
            sb.AppendLine();

            // Add specification context if available
            if (!string.IsNullOrEmpty(SpecificationContext))
            {
                sb.AppendLine("## Project Specification");
                sb.AppendLine();
                sb.AppendLine(SpecificationContext);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Add plan context if available
            if (ImplementationPlan != null && ImplementationPlan.Steps.Count > 0)
            {
                sb.AppendLine("## Implementation Plan");
                sb.AppendLine();
                sb.AppendLine("You have an implementation plan to follow. Execute each step in order.");
                sb.AppendLine();
                sb.AppendLine("**CRITICAL**: After completing each step, you MUST call the `update_plan_step` tool to mark it as completed.");
                sb.AppendLine("This saves your progress and allows the task to be resumed if interrupted.");
                sb.AppendLine();

                // Show plan summary
                foreach (var step in ImplementationPlan.Steps)
                {
                    var statusIcon = step.Status switch
                    {
                        StepStatus.Completed => "[x]",
                        StepStatus.InProgress => "[>]",
                        StepStatus.Failed => "[!]",
                        StepStatus.Skipped => "[-]",
                        _ => "[ ]"
                    };
                    sb.AppendLine($"{statusIcon} Step {step.Index}: {step.Title}");
                }
                sb.AppendLine();

                // Show current step details if resuming
                if (ImplementationPlan.CurrentStepIndex > 0)
                {
                    sb.AppendLine($"**Resume from Step {ImplementationPlan.CurrentStepIndex + 1}**");
                    sb.AppendLine();
                }

                // Show details of pending steps
                sb.AppendLine("### Step Details");
                sb.AppendLine();
                foreach (var step in ImplementationPlan.Steps.Where(s => s.Status == StepStatus.Pending || s.Status == StepStatus.InProgress))
                {
                    sb.AppendLine($"**Step {step.Index}: {step.Title}**");
                    sb.AppendLine();
                    sb.AppendLine(step.Description);
                    sb.AppendLine();

                    if (step.FilesToCreate.Count > 0)
                    {
                        sb.AppendLine("Files to create:");
                        foreach (var file in step.FilesToCreate)
                        {
                            sb.AppendLine($"- {file}");
                        }
                        sb.AppendLine();
                    }

                    if (step.FilesToModify.Count > 0)
                    {
                        sb.AppendLine("Files to modify:");
                        foreach (var file in step.FilesToModify)
                        {
                            sb.AppendLine($"- {file}");
                        }
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Add task description
            sb.AppendLine("## Your Task");
            sb.AppendLine();
            sb.AppendLine(TaskDescription);
            sb.AppendLine();

            if (ImplementationPlan != null)
            {
                sb.AppendLine("**Important**: Follow the implementation plan above. For each step:");
                sb.AppendLine("1. Complete the step's work (create/modify files as specified)");
                sb.AppendLine("2. Verify your work is correct");
                sb.AppendLine("3. Call `update_plan_step` with the step number and status \"completed\"");
                sb.AppendLine("4. Move to the next step");
                sb.AppendLine();
                sb.AppendLine("If a step fails, call `update_plan_step` with status \"failed\" and explain why.");
                sb.AppendLine("If a step should be skipped, call `update_plan_step` with status \"skipped\" and explain why.");
            }
            else if (!string.IsNullOrEmpty(SpecificationContext))
            {
                sb.AppendLine("**Important**: Keep the project specification in mind while completing this task. Ensure your implementation aligns with the overall project requirements and architecture described above.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Updates the plan status after execution
        /// </summary>
        private async Task UpdatePlanStatusAsync(KoboldPlanService? planService, bool success, string? errorMessage = null)
        {
            if (ImplementationPlan == null)
            {
                return;
            }

            if (success)
            {
                // Log current step statuses for debugging
                var statusCounts = ImplementationPlan.Steps
                    .GroupBy(s => s.Status)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                var statusSummary = string.Join(", ", statusCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                _logger?.LogDebug("Kobold {KoboldId} plan step status summary: {StatusSummary} at {Timestamp:o}", 
                    Id.ToString()[..8], statusSummary, DateTime.UtcNow);

                // Only mark the plan as completed if all steps are actually done
                var allStepsFinished = ImplementationPlan.Steps.All(s =>
                    s.Status == StepStatus.Completed ||
                    s.Status == StepStatus.Skipped ||
                    s.Status == StepStatus.Failed);

                if (allStepsFinished)
                {
                    _logger?.LogInformation("Kobold {KoboldId} all steps finished - marking plan as completed at {Timestamp:o}", 
                        Id.ToString()[..8], DateTime.UtcNow);
                    ImplementationPlan.MarkCompleted();
                }
                else
                {
                    // Get list of unfinished steps for detailed logging
                    var pendingSteps = ImplementationPlan.Steps
                        .Where(s => s.Status == StepStatus.Pending || s.Status == StepStatus.InProgress)
                        .Select(s => $"Step {s.Index}: {s.Title}")
                        .ToList();
                    
                    _logger?.LogWarning(
                        "Kobold {KoboldId} plan has {PendingCount} unfinished steps, keeping in InProgress. " +
                        "First steps: {FirstSteps} at {Timestamp:o}",
                        Id.ToString()[..8], pendingSteps.Count, string.Join("; ", pendingSteps.Take(3)), DateTime.UtcNow);
                    
                    // Agent finished but not all steps are done - keep plan in progress for resumption
                    ImplementationPlan.AddLogEntry(
                        $"Kobold {Id.ToString()[..8]} finished work session, " +
                        $"{ImplementationPlan.CompletedStepsCount}/{ImplementationPlan.Steps.Count} steps completed. " +
                        $"Unfinished: {string.Join(", ", pendingSteps.Select(p => p.Split(':')[0]).Take(5))}"
                    );
                }
            }
            else
            {
                ImplementationPlan.MarkFailed(errorMessage ?? "Unknown error");
            }

            if (planService != null && !string.IsNullOrEmpty(ProjectId))
            {
                await planService.SavePlanAsync(ImplementationPlan);
            }
        }

        /// <summary>
        /// Checks if the agent's message history contains errors (stop reason "error" or "NotConfigured")
        /// </summary>
        private bool HasErrorInMessages(List<Message> messages)
        {
            // Check the last assistant message for error indicators
            var lastAssistantMsg = messages.LastOrDefault(m => m.Role == "assistant");
            if (lastAssistantMsg == null) return false;

            // Check if the conversation ended with an error stop reason
            // This is inferred from the agent stopping without completing the task
            // Look for error messages in the message content
            if (lastAssistantMsg.Content is IEnumerable<ContentBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (block.Type == "text" && block.Text != null)
                    {
                        var text = block.Text.ToLowerInvariant();
                        // Check for common error indicators
                        if (text.Contains("error occurred during llm request") ||
                            text.Contains("error:") && text.Contains("llm request") ||
                            text.Contains("provider") && text.Contains("not properly configured") ||
                            text.Contains("error:") && text.Contains("provider"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets whether this Kobold has completed its work (successfully or with error)
        /// </summary>
        public bool IsComplete => Status == KoboldStatus.Done || Status == KoboldStatus.Failed;

        /// <summary>
        /// Gets whether this Kobold completed successfully (no error)
        /// </summary>
        public bool IsSuccess => Status == KoboldStatus.Done && string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Gets whether this Kobold failed (has error or Failed status)
        /// </summary>
        public bool HasError => Status == KoboldStatus.Failed || !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Gets whether this Kobold was marked as stuck (timed out)
        /// </summary>
        public bool IsStuck { get; private set; }

        /// <summary>
        /// Marks this Kobold as stuck due to exceeding the timeout threshold.
        /// Forces transition to Failed status with an error message.
        /// </summary>
        /// <param name="workingDuration">How long the Kobold was working</param>
        /// <param name="timeout">The timeout threshold that was exceeded</param>
        public void MarkAsStuck(TimeSpan workingDuration, TimeSpan timeout)
        {
            if (Status != KoboldStatus.Working)
            {
                return; // Only working Kobolds can be marked as stuck
            }

            IsStuck = true;
            ErrorMessage = $"Kobold timed out after {workingDuration.TotalMinutes:F1} minutes (threshold: {timeout.TotalMinutes:F0} minutes)";
            Status = KoboldStatus.Failed;
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Resets the Kobold to unassigned state (for reuse)
        /// </summary>
        public void Reset()
        {
            TaskId = null;
            TaskDescription = null;
            ProjectId = null;
            SpecificationContext = null;
            ImplementationPlan = null;
            Status = KoboldStatus.Unassigned;
            AssignedAt = null;
            StartedAt = null;
            CompletedAt = null;
            ErrorMessage = null;
            IsStuck = false;
        }

        /// <summary>
        /// Gets a string representation of this Kobold
        /// </summary>
        public override string ToString()
        {
            var taskInfo = TaskId.HasValue ? $"Task: {TaskId}" : "No task";
            return $"Kobold {Id.ToString()[..8]} ({AgentType}) - {Status} - {taskInfo}";
        }
    }
}
