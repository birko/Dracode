using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Services;
using DraCode.KoboldLair.Models.Validation;
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
        private readonly StepValidationService _validationService;

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
            _validationService = new StepValidationService(logger as ILogger<StepValidationService>);
            
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

                // Calculate step-aware max iterations if we have a plan
                int effectiveMaxIterations = maxIterations;
                if (ImplementationPlan != null && ImplementationPlan.Steps.Count > 0)
                {
                    var totalSteps = ImplementationPlan.Steps.Count;
                    var maxPerStep = Agent.Options.MaxIterationsPerStep;
                    
                    // Dynamic budget: give each step a fair share plus buffer
                    // Formula: min(MaxIterations / totalSteps + 2, MaxIterationsPerStep)
                    var perStepBudget = Math.Min(maxIterations / totalSteps + 2, maxPerStep);
                    effectiveMaxIterations = perStepBudget * totalSteps;
                    
                    _logger?.LogDebug(
                        "Kobold {KoboldId} using step-aware iteration budget: {PerStepBudget} per step × {TotalSteps} steps = {TotalBudget} iterations",
                        Id.ToString()[..8], perStepBudget, totalSteps, effectiveMaxIterations);
                }

                // Execute the task through the underlying agent
                var messages = await Agent.RunAsync(fullTaskPrompt, effectiveMaxIterations);

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

                // Perform file validation on incomplete steps (advisory logging only)
                if (ImplementationPlan != null)
                {
                    await ValidateAndLogStepCompletionAsync(ImplementationPlan);
                }

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
        /// Starts working on the assigned task with plan awareness and Phase 2 enhancements.
        /// Includes automatic step completion detection and fallback auto-advancement.
        /// </summary>
        /// <param name="planService">Service for plan persistence (can be null to skip plan updates)</param>
        /// <param name="maxIterations">Maximum iterations for agent execution</param>
        /// <returns>Messages from agent execution</returns>
        public async Task<List<Message>> StartWorkingWithPlanEnhancedAsync(
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

            // If no plan, fall back to standard execution
            if (ImplementationPlan == null)
            {
                return await StartWorkingWithPlanAsync(planService, maxIterations);
            }

            Status = KoboldStatus.Working;
            StartedAt = DateTime.UtcNow;

            // Register the plan context for the tool
            UpdatePlanStepTool.RegisterContext(ImplementationPlan, planService, _logger);

            // Add the tool to the agent
            Agent.AddTool(new UpdatePlanStepTool());

            try
            {
                // Build the full task prompt with plan context
                var fullTaskPrompt = BuildFullPromptWithPlan();

                // Update plan status
                ImplementationPlan.Status = PlanStatus.InProgress;
                ImplementationPlan.AddLogEntry($"Kobold {Id.ToString()[..8]} started working (enhanced mode)");
                if (planService != null && !string.IsNullOrEmpty(ProjectId))
                {
                    await planService.SavePlanAsync(ImplementationPlan);
                }

                // Calculate step-aware max iterations
                var totalSteps = ImplementationPlan.Steps.Count;
                var maxPerStep = Agent.Options.MaxIterationsPerStep;
                var perStepBudget = Math.Min(maxIterations / totalSteps + 2, maxPerStep);
                var effectiveMaxIterations = perStepBudget * totalSteps;

                _logger?.LogDebug(
                    "Kobold {KoboldId} using enhanced execution with step-aware budget: {PerStepBudget} per step × {TotalSteps} steps = {TotalBudget} iterations",
                    Id.ToString()[..8], perStepBudget, totalSteps, effectiveMaxIterations);

                // Execute with custom loop for step completion detection
                var messages = await RunWithStepDetectionAsync(fullTaskPrompt, effectiveMaxIterations, planService);

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

                // Perform file validation on incomplete steps (advisory logging only)
                await ValidateAndLogStepCompletionAsync(ImplementationPlan);

                // Check if we have a plan and if all steps are complete
                bool allStepsComplete = ImplementationPlan.Steps.All(s =>
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
                        taskPreview, ImplementationPlan.CompletedStepsCount, ImplementationPlan.Steps.Count);
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
                Agent.RemoveTool("update_plan_step");
                UpdatePlanStepTool.ClearContext();
            }
        }

        /// <summary>
        /// Custom execution loop that detects step completion and auto-advances when appropriate.
        /// This is the core of Phase 2 robustness enhancements.
        /// </summary>
        private async Task<List<Message>> RunWithStepDetectionAsync(
            string initialPrompt,
            int maxIterations,
            KoboldPlanService? planService)
        {
            if (ImplementationPlan == null)
            {
                throw new InvalidOperationException("RunWithStepDetectionAsync requires a plan");
            }

            var conversation = new List<Message>
            {
                new() { Role = "user", Content = initialPrompt }
            };

            var currentStepIndex = ImplementationPlan.CurrentStepIndex;
            var stepIterationCount = 0;

            // Enhanced logging: Step start
            if (currentStepIndex < ImplementationPlan.Steps.Count)
            {
                var step = ImplementationPlan.Steps[currentStepIndex];
                step.Start();
                
                _logger?.LogInformation(
                    "🔷 Kobold {KoboldId} starting step {StepIndex}/{TotalSteps}: {StepTitle}\n" +
                    "  Files to create: {FilesToCreate}\n" +
                    "  Files to modify: {FilesToModify}",
                    Id.ToString()[..8], step.Index, ImplementationPlan.Steps.Count, step.Title,
                    step.FilesToCreate.Count > 0 ? string.Join(", ", step.FilesToCreate) : "none",
                    step.FilesToModify.Count > 0 ? string.Join(", ", step.FilesToModify) : "none");
            }

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                stepIterationCount++;

                if (Agent.Options.Verbose)
                {
                    Agent.Provider.MessageCallback?.Invoke("info", $"ITERATION {iteration} (Step {currentStepIndex + 1}, iter {stepIterationCount})");
                }

                var systemPrompt = (string?)Agent.GetType().GetProperty("SystemPrompt", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(Agent) ?? "";
                
                var response = await Agent.Provider.SendMessageAsync(conversation, Agent.Tools.ToList(), systemPrompt);

                if (Agent.Options.Verbose)
                {
                    Agent.Provider.MessageCallback?.Invoke("info", $"Stop reason: {response.StopReason}");
                }

                // Add assistant response to conversation
                conversation.Add(new Message
                {
                    Role = "assistant",
                    Content = response.Content
                });

                // Handle different stop reasons
                switch (response.StopReason)
                {
                    case "tool_use":
                        var toolResults = new List<object>();
                        var calledUpdatePlanStep = false;

                        foreach (var block in (response.Content ?? Enumerable.Empty<ContentBlock>()).Where(b => b.Type == "tool_use"))
                        {
                            var toolCallMsg = $"Tool: {block.Name}\nInput: {System.Text.Json.JsonSerializer.Serialize(block.Input)}";
                            Agent.Provider.MessageCallback?.Invoke("tool_call", toolCallMsg);

                            var tool = Agent.Tools.FirstOrDefault(t => t.Name == block.Name);
                            var result = tool != null
                                ? tool.Execute(Agent.Options.WorkingDirectory, block.Input ?? new Dictionary<string, object>())
                                : $"Error: Unknown tool '{block.Name}'";

                            var preview = result.Length > 500 ? string.Concat(result.AsSpan(0, 500), "...") : result;
                            Agent.Provider.MessageCallback?.Invoke("tool_result", $"Result from {block.Name}:\n{preview}");

                            // Check if agent called update_plan_step
                            if (block.Name == "update_plan_step")
                            {
                                calledUpdatePlanStep = true;
                                
                                // Enhanced logging: Step completion
                                var newStepIndex = ImplementationPlan.CurrentStepIndex;
                                if (newStepIndex != currentStepIndex && currentStepIndex < ImplementationPlan.Steps.Count)
                                {
                                    var completedStep = ImplementationPlan.Steps[currentStepIndex];
                                    
                                    // Phase 3: Update telemetry
                                    completedStep.Metrics.IterationsUsed = stepIterationCount;
                                    
                                    _logger?.LogInformation(
                                        "✅ Step {StepIndex} completed: {StepTitle} ({IterationCount} iterations, status: {Status})",
                                        completedStep.Index, completedStep.Title, stepIterationCount, completedStep.Status);
                                    
                                    // Reset counter and start next step
                                    stepIterationCount = 0;
                                    currentStepIndex = newStepIndex;
                                    
                                    if (currentStepIndex < ImplementationPlan.Steps.Count)
                                    {
                                        var nextStep = ImplementationPlan.Steps[currentStepIndex];
                                        nextStep.Start();
                                        
                                        _logger?.LogInformation(
                                            "🔷 Starting step {StepIndex}/{TotalSteps}: {StepTitle}\n" +
                                            "  Files to create: {FilesToCreate}\n" +
                                            "  Files to modify: {FilesToModify}",
                                            nextStep.Index, ImplementationPlan.Steps.Count, nextStep.Title,
                                            nextStep.FilesToCreate.Count > 0 ? string.Join(", ", nextStep.FilesToCreate) : "none",
                                            nextStep.FilesToModify.Count > 0 ? string.Join(", ", nextStep.FilesToModify) : "none");
                                    }
                                }
                            }

                            toolResults.Add(new
                            {
                                type = "tool_result",
                                tool_use_id = block.Id,
                                content = result
                            });
                        }

                        conversation.Add(new Message
                        {
                            Role = "user",
                            Content = toolResults
                        });

                        // Phase 2: Automatic step completion detection
                        if (!calledUpdatePlanStep && currentStepIndex < ImplementationPlan.Steps.Count)
                        {
                            var currentStep = ImplementationPlan.Steps[currentStepIndex];
                            
                            // Only check if step is still pending/in-progress
                            if (currentStep.Status == StepStatus.Pending || currentStep.Status == StepStatus.InProgress)
                            {
                                var (isComplete, issues) = await ValidateStepCompletionAsync(currentStep);
                                
                                if (isComplete)
                                {
                                    // Phase 2: Fallback auto-advancement
                                    _logger?.LogWarning(
                                        "⚠️ AUTO-ADVANCE: Step {StepIndex} validated as complete but agent didn't mark it. Auto-advancing.\n" +
                                        "  Step: {StepTitle}\n" +
                                        "  Iterations on this step: {IterationCount}",
                                        currentStep.Index, currentStep.Title, stepIterationCount);
                                    
                                    currentStep.Complete($"Auto-completed by validation (agent didn't mark explicitly)");
                                    
                                    // Phase 3: Update telemetry
                                    currentStep.Metrics.IterationsUsed = stepIterationCount;
                                    currentStep.Metrics.AutoCompleted = true;
                                    
                                    ImplementationPlan.CurrentStepIndex++;
                                    ImplementationPlan.AddLogEntry($"Auto-advanced from step {currentStep.Index} after validation passed");
                                    
                                    if (planService != null && !string.IsNullOrEmpty(ProjectId))
                                    {
                                        await planService.SavePlanAsync(ImplementationPlan);
                                    }
                                    
                                    // Enhanced logging: Step auto-completion
                                    _logger?.LogInformation(
                                        "✅ Step {StepIndex} auto-completed: {StepTitle} ({IterationCount} iterations)",
                                        currentStep.Index, currentStep.Title, stepIterationCount);
                                    
                                    // Reset and start next step
                                    stepIterationCount = 0;
                                    currentStepIndex = ImplementationPlan.CurrentStepIndex;
                                    
                                    if (currentStepIndex < ImplementationPlan.Steps.Count)
                                    {
                                        var nextStep = ImplementationPlan.Steps[currentStepIndex];
                                        nextStep.Start();
                                        
                                        _logger?.LogInformation(
                                            "🔷 Starting step {StepIndex}/{TotalSteps}: {StepTitle}",
                                            nextStep.Index, ImplementationPlan.Steps.Count, nextStep.Title);
                                    }
                                }
                            }
                        }

                        // If we hit max iterations, stop
                        if (iteration >= maxIterations)
                        {
                            Agent.Provider.MessageCallback?.Invoke("warning", $"Maximum iterations ({maxIterations}) reached. Task may be incomplete.");
                            return conversation;
                        }
                        break;

                    case "end_turn":
                        // Agent finished - send response
                        foreach (var block in (response.Content ?? Enumerable.Empty<ContentBlock>()).Where(b => b.Type == "text"))
                        {
                            Agent.Provider.MessageCallback?.Invoke("assistant_final", block.Text ?? "");
                        }
                        return conversation;

                    case "error":
                    case "NotConfigured":
                        // Error occurred - stop immediately
                        Agent.Provider.MessageCallback?.Invoke("error", "Error occurred during LLM request. Stopping.");
                        return conversation;

                    default:
                        // Unexpected stop reason - stop to be safe
                        if (Agent.Options.Verbose)
                        {
                            Agent.Provider.MessageCallback?.Invoke("warning", $"Unexpected stop reason: {response.StopReason ?? "unknown"}. Stopping.");
                        }
                        return conversation;
                }
            }

            // If we exit the loop naturally, we hit max iterations
            if (Agent.Options.Verbose)
            {
                Agent.Provider.MessageCallback?.Invoke("warning", $"Maximum iterations ({maxIterations}) reached.");
            }

            return conversation;
        }

        /// <summary>
        /// Builds the full task prompt including plan context if available.
        /// Phase 3: Supports progressive detail reveal to reduce token usage.
        /// </summary>
        /// <param name="useProgressiveReveal">Whether to use progressive detail reveal (default: true)</param>
        /// <param name="mediumDetailCount">Number of upcoming steps to show medium details for (default: 2)</param>
        private string BuildFullPromptWithPlan(bool useProgressiveReveal = true, int mediumDetailCount = 2)
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
                sb.AppendLine("**REFLECTION REMINDER**: Every few iterations, pause and reflect:");
                sb.AppendLine("1. Did I create/modify the files specified in the current step?");
                sb.AppendLine("2. Is the step truly complete? If yes, call `update_plan_step` immediately.");
                sb.AppendLine("3. If incomplete, what specific work remains? Focus on that.");
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

                // Phase 3: Progressive detail reveal
                if (useProgressiveReveal && ImplementationPlan.Steps.Count > 3)
                {
                    sb.AppendLine("### Step Details (Progressive)");
                    sb.AppendLine();

                    var currentIndex = ImplementationPlan.CurrentStepIndex;
                    
                    // Full details for current step
                    if (currentIndex < ImplementationPlan.Steps.Count)
                    {
                        var currentStep = ImplementationPlan.Steps[currentIndex];
                        sb.AppendLine($"**CURRENT STEP {currentStep.Index}: {currentStep.Title}** ⭐");
                        sb.AppendLine();
                        sb.AppendLine(currentStep.Description);
                        sb.AppendLine();

                        if (currentStep.FilesToCreate.Count > 0)
                        {
                            sb.AppendLine("Files to create:");
                            foreach (var file in currentStep.FilesToCreate)
                            {
                                sb.AppendLine($"- {file}");
                            }
                            sb.AppendLine();
                        }

                        if (currentStep.FilesToModify.Count > 0)
                        {
                            sb.AppendLine("Files to modify:");
                            foreach (var file in currentStep.FilesToModify)
                            {
                                sb.AppendLine($"- {file}");
                            }
                            sb.AppendLine();
                        }
                    }

                    // Medium details for next N steps
                    for (int i = currentIndex + 1; i < Math.Min(currentIndex + 1 + mediumDetailCount, ImplementationPlan.Steps.Count); i++)
                    {
                        var step = ImplementationPlan.Steps[i];
                        sb.AppendLine($"**Upcoming Step {step.Index}: {step.Title}**");
                        sb.AppendLine();
                        sb.AppendLine(step.Description);
                        sb.AppendLine();
                    }

                    // Summary only for remaining steps
                    if (currentIndex + 1 + mediumDetailCount < ImplementationPlan.Steps.Count)
                    {
                        sb.AppendLine("**Later Steps (Summary):**");
                        for (int i = currentIndex + 1 + mediumDetailCount; i < ImplementationPlan.Steps.Count; i++)
                        {
                            var step = ImplementationPlan.Steps[i];
                            var statusIcon = step.Status switch
                            {
                                StepStatus.Completed => "[x]",
                                StepStatus.Failed => "[!]",
                                StepStatus.Skipped => "[-]",
                                _ => "[ ]"
                            };
                            sb.AppendLine($"{statusIcon} Step {step.Index}: {step.Title}");
                        }
                        sb.AppendLine();
                    }
                }
                else
                {
                    // Original behavior: Show full details for all pending/in-progress steps
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
        /// Validates whether a step's file expectations were met.
        /// Checks if files marked for creation exist and files marked for modification were touched.
        /// </summary>
        /// <param name="step">The step to validate</param>
        /// <returns>True if validation passed, false otherwise</returns>
        /// <summary>
        /// Validates whether a step's file expectations were met using the validation service.
        /// </summary>
        /// <param name="step">The step to validate</param>
        /// <returns>True if validation passed, false otherwise with list of issues</returns>
        private async Task<(bool success, List<string> issues)> ValidateStepCompletionAsync(ImplementationStep step)
        {
            // Phase 3: Use validation service
            var result = await _validationService.ValidateStepAsync(step, Agent.Options.WorkingDirectory);
            return (result.Success, result.AllIssues);
        }

        /// <summary>
        /// Validates and logs file completion for steps in the plan.
        /// This is advisory only - doesn't change step status.
        /// </summary>
        private async Task ValidateAndLogStepCompletionAsync(KoboldImplementationPlan plan)
        {
            foreach (var step in plan.Steps.Where(s => s.Status == StepStatus.Completed))
            {
                var (success, issues) = await ValidateStepCompletionAsync(step);
                
                if (!success && issues.Count > 0)
                {
                    var issuesSummary = string.Join("; ", issues.Take(3));
                    _logger?.LogWarning(
                        "⚠ Step {StepIndex} marked complete but validation found issues: {Issues}",
                        step.Index, issuesSummary);
                }
            }
        }

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
