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
        private SharedPlanningContextService? _sharedPlanningContext;

        /// <summary>
        /// Phase 4: Indices of steps assigned to this Kobold for parallel execution (0-based).
        /// Null means this Kobold executes all steps (sequential mode).
        /// </summary>
        private List<int>? _assignedStepIndices;

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
        /// Project structure guidelines (naming conventions, directory organization, etc.)
        /// </summary>
        public ProjectStructure? ProjectStructure { get; private set; }

        /// <summary>
        /// Specification version when this Kobold was created
        /// </summary>
        private int _assignedSpecificationVersion = 1;

        /// <summary>
        /// Path to specification file for version checking
        /// </summary>
        private string? _specificationPath;

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
        /// Timestamp of the last successful LLM response
        /// Used for detecting truly stuck agents (no response for extended period)
        /// </summary>
        public DateTime? LastLlmResponseAt { get; private set; }

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
        public KoboldImplementationPlan? ImplementationPlan { get; internal set; }

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
            
            // Set up LLM response callback to track activity and prevent false timeouts
            Agent.Options.OnLlmResponseReceived = () =>
            {
                LastLlmResponseAt = DateTime.UtcNow;
            };
            
            _logger?.LogInformation("Kobold {KoboldId} created with agent type {AgentType} at {CreatedAt:o}", 
                Id.ToString()[..8], agentType, CreatedAt);
        }

        /// <summary>
        /// Sets the shared planning context service for workspace awareness
        /// </summary>
        public void SetSharedPlanningContext(SharedPlanningContextService? sharedPlanningContext)
        {
            _sharedPlanningContext = sharedPlanningContext;
        }

        /// <summary>
        /// Assigns this Kobold to a specific task
        /// </summary>
        /// <param name="taskId">Task identifier</param>
        /// <param name="taskDescription">Description of the task to execute</param>
        /// <param name="projectId">Project identifier this task belongs to</param>
        /// <param name="specificationContext">Optional specification context for the task</param>
        /// <param name="projectStructure">Project structure guidelines</param>
        /// <param name="specificationVersion">Specification version when task was created</param>
        /// <param name="specificationPath">Path to specification file for version checking</param>
        public void AssignTask(
            Guid taskId,
            string taskDescription,
            string? projectId = null,
            string? specificationContext = null,
            ProjectStructure? projectStructure = null,
            int specificationVersion = 1,
            string? specificationPath = null)
        {
            if (Status != KoboldStatus.Unassigned)
            {
                throw new InvalidOperationException($"Cannot assign task to Kobold {Id} - current status is {Status}");
            }

            TaskId = taskId;
            TaskDescription = taskDescription;
            ProjectId = projectId;
            SpecificationContext = specificationContext;
            ProjectStructure = projectStructure;
            _assignedSpecificationVersion = specificationVersion;
            _specificationPath = specificationPath;
            Status = KoboldStatus.Assigned;
            AssignedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the specification context for this Kobold.
        /// Can be called after plan creation to provide more focused context.
        /// </summary>
        /// <param name="specificationContext">New specification context</param>
        public void UpdateSpecificationContext(string? specificationContext)
        {
            if (Status == KoboldStatus.Working)
            {
                throw new InvalidOperationException($"Cannot update specification context while Kobold {Id} is working");
            }

            SpecificationContext = specificationContext;
        }

        /// <summary>
        /// Phase 4: Sets the indices of steps this Kobold should execute in parallel mode.
        /// When set, the Kobold will only work on the assigned steps instead of all steps.
        /// </summary>
        /// <param name="stepIndices">List of 0-based step indices to execute</param>
        public void SetAssignedStepIndices(List<int> stepIndices)
        {
            _assignedStepIndices = stepIndices;

            // Update the plan with this assignment
            if (ImplementationPlan != null)
            {
                ImplementationPlan.AssignStepsToKobold(Id.ToString(), stepIndices);
            }

            _logger?.LogDebug(
                "Kobold {KoboldId} assigned {Count} steps for parallel execution: {StepIndices}",
                Id.ToString()[..8], stepIndices.Count, string.Join(", ", stepIndices));
        }

        /// <summary>
        /// Phase 4: Gets the indices of steps assigned to this Kobold.
        /// Returns null if this Kobold should execute all steps (sequential mode).
        /// </summary>
        public List<int>? GetAssignedStepIndices()
        {
            return _assignedStepIndices;
        }

        /// <summary>
        /// Phase 4: Clears any step assignment, reverting to sequential execution of all steps.
        /// </summary>
        public void ClearAssignedStepIndices()
        {
            _assignedStepIndices = null;

            if (ImplementationPlan != null)
            {
                ImplementationPlan.ClearStepAssignments(Id.ToString());
            }

            _logger?.LogDebug("Kobold {KoboldId} step assignment cleared", Id.ToString()[..8]);
        }

        /// <summary>
        /// Phase 4: Filters steps based on assigned step indices.
        /// When _assignedStepIndices is set, returns only the assigned steps.
        /// Otherwise, returns all steps for backward compatibility.
        /// </summary>
        /// <param name="allSteps">All steps in the plan</param>
        /// <returns>Filtered list of steps to display/execute</returns>
        private List<ImplementationStep> FilterAssignedSteps(List<ImplementationStep> allSteps)
        {
            if (_assignedStepIndices == null || _assignedStepIndices.Count == 0)
            {
                // No assignment - return all steps (backward compatible)
                return allSteps;
            }

            // Return only assigned steps
            return _assignedStepIndices
                .Where(i => i >= 0 && i < allSteps.Count)
                .Select(i => allSteps[i])
                .ToList();
        }

        /// <summary>
        /// Checks if specification has changed and reloads if necessary
        /// </summary>
        private async Task EnsureCurrentSpecificationAsync()
        {
            if (string.IsNullOrEmpty(_specificationPath) || !File.Exists(_specificationPath))
            {
                return;
            }

            try
            {
                var currentContent = await File.ReadAllTextAsync(_specificationPath);
                var currentHash = DraCode.KoboldLair.Models.Projects.Specification.ComputeHash(currentContent);

                // Read persisted version from specification.features.json (wrapped format)
                var featuresPath = Path.Combine(Path.GetDirectoryName(_specificationPath) ?? "", "specification.features.json");
                int persistedVersion = _assignedSpecificationVersion;

                if (File.Exists(featuresPath))
                {
                    try
                    {
                        var featuresJson = await File.ReadAllTextAsync(featuresPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(featuresJson);
                        // New format has "specificationVersion" property
                        if (doc.RootElement.TryGetProperty("specificationVersion", out var versionProp))
                        {
                            persistedVersion = versionProp.GetInt32();
                        }
                    }
                    catch { }
                }

                if (persistedVersion > _assignedSpecificationVersion)
                {
                    _logger?.LogWarning(
                        "Kobold {KoboldId}: Specification version changed: {OldVersion} → {NewVersion}. Reloading context.",
                        Id.ToString()[..8], _assignedSpecificationVersion, persistedVersion);

                    SpecificationContext = currentContent;
                    _assignedSpecificationVersion = persistedVersion;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Kobold {KoboldId}: Failed to check specification version", Id.ToString()[..8]);
            }
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
            LastLlmResponseAt = DateTime.UtcNow; // Initialize to start time

            // Check if specification version changed before starting work
            if (!string.IsNullOrEmpty(_specificationPath))
            {
                await EnsureCurrentSpecificationAsync();
            }

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
                var executionError = ExtractErrorFromMessages(messages);
                if (executionError != null)
                {
                    ErrorMessage = executionError;
                    Status = KoboldStatus.Failed;
                    CompletedAt = DateTime.UtcNow;

                    var taskPreview = TaskDescription?.Length > 60
                        ? TaskDescription.Substring(0, 60) + "..."
                        : TaskDescription ?? "unknown";
                    _logger?.LogWarning(
                        "Kobold {KoboldId} failed with errors\n" +
                        "  Project: {ProjectId}\n" +
                        "  Task ID: {TaskId}\n" +
                        "  Task: {TaskDescription}\n" +
                        "  Error: {ErrorMessage}",
                        Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown", taskPreview, executionError);
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
        /// <param name="sharedPlanningContext">Optional shared planning context for workspace awareness</param>
        /// <returns>The loaded or created implementation plan</returns>
        public async Task<KoboldImplementationPlan> EnsurePlanAsync(
            KoboldPlanService planService,
            KoboldPlannerAgent planner,
            SharedPlanningContextService? sharedPlanningContext = null)
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

            // Gather workspace state for planning
            List<string>? workspaceFiles = null;
            HashSet<string>? filesInUse = null;
            Dictionary<string, string>? fileMetadata = null;
            List<KoboldImplementationPlan>? relatedPlans = null;
            List<PlanningInsight>? similarTaskInsights = null;
            Dictionary<string, string>? bestPractices = null;

            try
            {
                // Get list of existing files in workspace
                var workspacePath = Agent.Options.WorkingDirectory;
                if (Directory.Exists(workspacePath))
                {
                    workspaceFiles = Directory.GetFiles(workspacePath, "*.*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(workspacePath, f).Replace('\\', '/'))
                        .Where(f => !f.StartsWith(".git/") && !f.StartsWith("bin/") && !f.StartsWith("obj/") && !f.StartsWith("node_modules/"))
                        .ToList();
                }

                // Get files currently being worked on by other agents
                if (sharedPlanningContext != null && !string.IsNullOrEmpty(ProjectId))
                {
                    filesInUse = await sharedPlanningContext.GetFilesInUseAsync(ProjectId);

                    // Get file metadata with purposes
                    var metadataDict = await sharedPlanningContext.GetFileMetadataAsync(ProjectId);
                    fileMetadata = metadataDict.ToDictionary(
                        kvp => kvp.Key,
                        kvp => string.IsNullOrWhiteSpace(kvp.Value.Purpose)
                            ? $"{kvp.Value.Category} file"
                            : kvp.Value.Purpose
                    );

                    // GAP 1 FIX: Get learning context from SharedPlanningContextService

                    // Get related plans that touched similar files
                    if (workspaceFiles != null && workspaceFiles.Any())
                    {
                        relatedPlans = await sharedPlanningContext.GetRelatedPlansAsync(
                            ProjectId,
                            taskIdStr,
                            workspaceFiles);

                        if (relatedPlans.Any())
                        {
                            _logger?.LogDebug("Found {Count} related plans for task {TaskId}",
                                relatedPlans.Count, taskIdStr[..8]);
                        }
                    }

                    // Get insights from similar task executions (same agent type)
                    similarTaskInsights = await sharedPlanningContext.GetSimilarTaskInsightsAsync(
                        ProjectId,
                        AgentType);

                    if (similarTaskInsights.Any())
                    {
                        _logger?.LogDebug("Found {Count} similar task insights for agent type {AgentType}",
                            similarTaskInsights.Count, AgentType);
                    }

                    // Get best practices learned for this agent type
                    bestPractices = await sharedPlanningContext.GetBestPracticesAsync(AgentType);

                    // Also get cross-project insights if within-project insights are sparse
                    if (similarTaskInsights.Count < 3)
                    {
                        var crossProjectInsights = await sharedPlanningContext.GetCrossProjectInsightsAsync(
                            ProjectId,
                            AgentType);

                        if (crossProjectInsights.Any())
                        {
                            _logger?.LogDebug("Added {Count} cross-project insights for agent type {AgentType}",
                                crossProjectInsights.Count, AgentType);
                            similarTaskInsights.AddRange(crossProjectInsights);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to gather workspace state for planning, proceeding without it");
            }

            // Create new plan using the planner agent with workspace awareness and learning context
            var plan = await planner.CreatePlanAsync(
                TaskDescription,
                SpecificationContext,
                ProjectStructure,
                workspaceFiles,
                filesInUse,
                fileMetadata,
                relatedPlans,
                similarTaskInsights,
                bestPractices);
            
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
            int maxIterations = 30,
            CancellationToken cancellationToken = default)
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
            LastLlmResponseAt = DateTime.UtcNow; // Initialize to start time

            // If we have a plan, inject the update_plan_step tool and register context
            bool toolInjected = false;
            if (ImplementationPlan != null)
            {
                // Register the plan context for the tool (including shared planning context for file tracking)
                UpdatePlanStepTool.RegisterContext(
                    ImplementationPlan,
                    planService,
                    _logger,
                    _sharedPlanningContext,
                    ProjectId,
                    TaskId?.ToString());

                // Add the tool to the agent
                Agent.AddTool(new UpdatePlanStepTool());
                toolInjected = true;
            }

            try
            {
                // Build the full task prompt with plan context if available
                var fullTaskPrompt = await BuildFullPromptWithPlanAsync(sharedPlanningContext: _sharedPlanningContext);

                // Update plan status
                if (ImplementationPlan != null)
                {
                    ImplementationPlan.Status = PlanStatus.InProgress;
                    ImplementationPlan.AddLogEntry($"Kobold {Id.ToString()[..8]} started working");
                    if (planService != null && !string.IsNullOrEmpty(ProjectId))
                    {
                        await planService.SavePlanDebouncedAsync(ImplementationPlan);
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
                var planExecutionError = ExtractErrorFromMessages(messages);
                if (planExecutionError != null)
                {
                    ErrorMessage = planExecutionError;
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
                        "  Task: {TaskDescription}\n" +
                        "  Error: {ErrorMessage}",
                        Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown", taskPreview, planExecutionError);
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
        /// Phase 4: Supports plan modifications if enabled.
        /// </summary>
        /// <param name="planService">Service for plan persistence (can be null to skip plan updates)</param>
        /// <param name="maxIterations">Maximum iterations for agent execution</param>
        /// <param name="allowPlanModifications">Whether to allow agent to suggest plan modifications</param>
        /// <param name="autoApproveModifications">Whether to auto-approve modifications</param>
        /// <returns>Messages from agent execution</returns>
        public async Task<List<Message>> StartWorkingWithPlanEnhancedAsync(
            KoboldPlanService? planService,
            int maxIterations = 30,
            bool allowPlanModifications = false,
            bool autoApproveModifications = false,
            CancellationToken cancellationToken = default)
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

            // Register the plan context for the tool (including shared planning context for file tracking)
            UpdatePlanStepTool.RegisterContext(
                ImplementationPlan,
                planService,
                _logger,
                _sharedPlanningContext,
                ProjectId,
                TaskId?.ToString());

            // Add the tool to the agent
            Agent.AddTool(new UpdatePlanStepTool());

            // Phase 4: Add modify_plan tool if enabled
            if (allowPlanModifications)
            {
                ModifyPlanTool.RegisterContext(ImplementationPlan, planService, allowPlanModifications, autoApproveModifications, _logger);
                Agent.AddTool(new ModifyPlanTool());
            }

            try
            {
                // Build the full task prompt with plan context
                var fullTaskPrompt = await BuildFullPromptWithPlanAsync(sharedPlanningContext: _sharedPlanningContext);

                // Update plan status
                ImplementationPlan.Status = PlanStatus.InProgress;
                ImplementationPlan.AddLogEntry($"Kobold {Id.ToString()[..8]} started working (enhanced mode)");
                if (planService != null && !string.IsNullOrEmpty(ProjectId))
                {
                    await planService.SavePlanDebouncedAsync(ImplementationPlan);
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
                var messages = await RunWithStepDetectionAsync(fullTaskPrompt, effectiveMaxIterations, planService, cancellationToken);

                // Check if execution encountered errors
                var enhancedExecutionError = ExtractErrorFromMessages(messages);
                if (enhancedExecutionError != null)
                {
                    ErrorMessage = enhancedExecutionError;
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
                        "  Task: {TaskDescription}\n" +
                        "  Error: {ErrorMessage}",
                        Id.ToString()[..8], ProjectId ?? "unknown", TaskId?.ToString()[..8] ?? "unknown", taskPreview, enhancedExecutionError);
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
                
                // Phase 4: Clean up modify_plan tool if it was added
                if (allowPlanModifications)
                {
                    Agent.RemoveTool("modify_plan");
                    ModifyPlanTool.ClearContext();
                }
            }
        }

        /// <summary>
        /// Custom execution loop that detects step completion and auto-advances when appropriate.
        /// This is the core of Phase 2 robustness enhancements.
        /// </summary>
        private async Task<List<Message>> RunWithStepDetectionAsync(
            string initialPrompt,
            int maxIterations,
            KoboldPlanService? planService,
            CancellationToken cancellationToken = default)
        {
            if (ImplementationPlan == null)
            {
                throw new InvalidOperationException("RunWithStepDetectionAsync requires a plan");
            }

            var currentStepIndex = ImplementationPlan.CurrentStepIndex;
            var stepIterationCount = 0;

            // Try to restore conversation from checkpoint if resuming
            List<Message> conversation;
            if (currentStepIndex > 0 && planService != null && !string.IsNullOrEmpty(ProjectId) && TaskId.HasValue)
            {
                var checkpoint = await planService.LoadConversationCheckpointAsync(ProjectId, TaskId.Value.ToString());
                if (checkpoint != null)
                {
                    conversation = KoboldPlanService.RestoreConversation(checkpoint);
                    // Append a resumption message so the LLM knows what happened
                    conversation.Add(new Message
                    {
                        Role = "user",
                        Content = $"You were previously working on this task and completed steps up to step {checkpoint.StepIndex + 1}. " +
                                  $"The server was restarted. Continue from step {currentStepIndex + 1} of {ImplementationPlan.Steps.Count}. " +
                                  "Do NOT redo already-completed steps."
                    });
                    _logger?.LogInformation(
                        "Kobold {KoboldId} resumed with conversation checkpoint ({MessageCount} messages, step {StepIndex})",
                        Id.ToString()[..8], checkpoint.Messages.Count, checkpoint.StepIndex);
                }
                else
                {
                    conversation = new List<Message> { new() { Role = "user", Content = initialPrompt } };
                }
            }
            else
            {
                conversation = new List<Message> { new() { Role = "user", Content = initialPrompt } };
            }

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
                // Graceful shutdown check: save state and return without throwing
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogInformation(
                        "Graceful shutdown requested for Kobold {KoboldId} at iteration {Iteration}, step {StepIndex}/{TotalSteps}",
                        Id.ToString()[..8], iteration, currentStepIndex + 1, ImplementationPlan.Steps.Count);

                    // Save plan state and conversation checkpoint
                    if (planService != null && !string.IsNullOrEmpty(ProjectId))
                    {
                        ImplementationPlan.AddLogEntry($"Graceful shutdown at iteration {iteration}, step {currentStepIndex + 1}");
                        await planService.SavePlanDebouncedAsync(ImplementationPlan);
                        await planService.SaveConversationCheckpointAsync(ImplementationPlan, conversation);
                        _logger?.LogInformation(
                            "Plan and conversation saved for Kobold {KoboldId} (step {StepIndex}/{TotalSteps})",
                            Id.ToString()[..8], currentStepIndex + 1, ImplementationPlan.Steps.Count);
                    }

                    // Return gracefully - this flows to the "not all steps complete" path
                    // keeping Kobold in Working state and plan in InProgress for resumption
                    return conversation;
                }

                stepIterationCount++;

                if (Agent.Options.Verbose)
                {
                    Agent.Provider.MessageCallback?.Invoke("info", $"ITERATION {iteration} (Step {currentStepIndex + 1}, iter {stepIterationCount})");
                }

                // Inject checkpoint reminder every N iterations (P1 self-reflection)
                var checkpointInterval = Agent.Options.CheckpointInterval;
                if (checkpointInterval > 0 && stepIterationCount > 1 && stepIterationCount % checkpointInterval == 0)
                {
                    var currentStep = ImplementationPlan.Steps[currentStepIndex];
                    var checkpointPrompt = $@"
---
**CHECKPOINT REQUIRED** (Iteration {iteration}, step iteration {stepIterationCount})

You've spent {stepIterationCount} iterations on step {currentStepIndex + 1}: '{currentStep.Title}'

Output a CHECKPOINT block now:
```
CHECKPOINT (iteration {iteration}):
- Progress: [X%] toward '{currentStep.Title}'
- Files done: [list]
- Blockers: [any, or 'none']
- Confidence: [0-100%]
- Decision: [continue|pivot|escalate]
```

If step is complete, call `update_plan_step` with status 'completed'.
---";

                    conversation.Add(new Message { Role = "user", Content = checkpointPrompt });
                    _logger?.LogDebug("Checkpoint reminder injected at iteration {Iteration}", iteration);
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

                                    // Save conversation checkpoint after step completion
                                    if (planService != null && !string.IsNullOrEmpty(ProjectId))
                                    {
                                        await planService.SaveConversationCheckpointAsync(ImplementationPlan, conversation);
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
                                        await planService.SavePlanDebouncedAsync(ImplementationPlan);
                                        await planService.SaveConversationCheckpointAsync(ImplementationPlan, conversation);
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
        /// Phase 4: Filters steps based on assigned step indices for parallel execution.
        /// </summary>
        /// <param name="useProgressiveReveal">Whether to use progressive detail reveal (default: true)</param>
        /// <param name="mediumDetailCount">Number of upcoming steps to show medium details for (default: 2)</param>
        /// <param name="sharedPlanningContext">Optional shared planning context for workspace awareness</param>
        private async Task<string> BuildFullPromptWithPlanAsync(
            bool useProgressiveReveal = true,
            int mediumDetailCount = 2,
            SharedPlanningContextService? sharedPlanningContext = null)
        {
            var sb = new System.Text.StringBuilder();

            // Phase 4: Filter steps based on assigned step indices
            var stepsToDisplay = FilterAssignedSteps(ImplementationPlan?.Steps ?? new List<ImplementationStep>());

            sb.AppendLine("# Task Context");
            sb.AppendLine();
            sb.AppendLine("You are working on a task that is part of a larger project.");
            sb.AppendLine();

            // Add file location guidelines - CRITICAL for correct file placement
            AppendFileLocationGuidelines(sb);

            // Add workspace state section - CRITICAL for execution context
            await AppendWorkspaceStateAsync(sb, sharedPlanningContext);

            // Add execution insights from similar tasks - helps avoid repeating mistakes
            await AppendExecutionInsightsAsync(sb, sharedPlanningContext);

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
                // Phase 4: Check if this is parallel execution mode (assigned steps subset)
                bool isParallelMode = _assignedStepIndices != null && _assignedStepIndices.Count > 0
                    && _assignedStepIndices.Count < ImplementationPlan.Steps.Count;

                if (isParallelMode)
                {
                    // Parallel execution mode - show only assigned steps
                    sb.AppendLine("## Implementation Plan (Parallel Execution Mode)");
                    sb.AppendLine();
                    sb.AppendLine($"You are executing a SUBSET of steps from a larger implementation plan.");
                    sb.AppendLine($"Your assigned steps: {_assignedStepIndices.Count} out of {ImplementationPlan.Steps.Count} total steps.");
                    sb.AppendLine();
                    sb.AppendLine("**CRITICAL**: After completing each step, you MUST call the `update_plan_step` tool to mark it as completed.");
                    sb.AppendLine("This saves your progress and allows the task to be resumed if interrupted.");
                    sb.AppendLine();

                    // Show only assigned steps in summary
                    sb.AppendLine("**Your Assigned Steps:**");
                    foreach (var step in stepsToDisplay)
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
                }
                else
                {
                    // Sequential mode - show full plan
                    sb.AppendLine("## Implementation Plan");
                    sb.AppendLine();
                    sb.AppendLine("You have an implementation plan to follow. Execute each step in order.");
                    sb.AppendLine();
                    sb.AppendLine("**CRITICAL**: After completing each step, you MUST call the `update_plan_step` tool to mark it as completed.");
                    sb.AppendLine("This saves your progress and allows the task to be resumed if interrupted.");
                    sb.AppendLine();

                    // Self-Reflection Protocol (P1 - Prompt-based)
                    sb.AppendLine("## SELF-REFLECTION PROTOCOL");
                    sb.AppendLine();
                    sb.AppendLine("You MUST output a CHECKPOINT block every 3 iterations. This is mandatory.");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine("CHECKPOINT (iteration N):");
                    sb.AppendLine("- Progress: [X%] toward current step completion");
                    sb.AppendLine("- Files done: [list files created/modified so far]");
                    sb.AppendLine("- Blockers: [any obstacles, or 'none']");
                    sb.AppendLine("- Confidence: [0-100%] this approach will succeed");
                    sb.AppendLine("- Decision: [continue|pivot|escalate]");
                    sb.AppendLine("```");
                    sb.AppendLine();
                    sb.AppendLine("## ERROR HANDLING PROTOCOL");
                    sb.AppendLine();
                    sb.AppendLine("When encountering errors, output an ERROR ANALYSIS block:");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine("ERROR ANALYSIS:");
                    sb.AppendLine("- What happened: [specific error]");
                    sb.AppendLine("- Root cause: [code issue|external service|config|missing dependency]");
                    sb.AppendLine("- Strategy adjustment: [what to change before retry]");
                    sb.AppendLine("```");
                    sb.AppendLine();
                    sb.AppendLine("**CRITICAL**: After completing each step, call `update_plan_step` immediately.");
                    sb.AppendLine("Before marking complete, verify:");
                    sb.AppendLine("1. Did I create/modify the files specified?");
                    sb.AppendLine("2. Does the code actually work (no obvious errors)?");
                    sb.AppendLine("3. Is the step truly complete?");
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
                }

                // Phase 4: For parallel mode, show details of assigned steps
                if (IsParallelMode())
                {
                    sb.AppendLine("### Your Assigned Step Details");
                    sb.AppendLine();
                    sb.AppendLine("Execute the following steps in order. Each step is independent and can be worked on separately.");
                    sb.AppendLine();

                    var firstPending = stepsToDisplay.FirstOrDefault(s => s.Status == StepStatus.Pending);
                    if (firstPending != null)
                    {
                        sb.AppendLine($"**START WITH: Step {firstPending.Index}: {firstPending.Title}** ⭐");
                        sb.AppendLine();
                        sb.AppendLine(firstPending.Description);
                        sb.AppendLine();

                        if (firstPending.FilesToCreate.Count > 0)
                        {
                            sb.AppendLine("Files to create:");
                            foreach (var file in firstPending.FilesToCreate)
                            {
                                sb.AppendLine($"- {file}");
                            }
                            sb.AppendLine();
                        }

                        if (firstPending.FilesToModify.Count > 0)
                        {
                            sb.AppendLine("Files to modify:");
                            foreach (var file in firstPending.FilesToModify)
                            {
                                sb.AppendLine($"- {file}");
                            }
                            sb.AppendLine();
                        }

                        // Show remaining assigned steps
                        var remainingSteps = stepsToDisplay.Where(s => s != firstPending && (s.Status == StepStatus.Pending || s.Status == StepStatus.InProgress)).ToList();
                        if (remainingSteps.Count > 0)
                        {
                            sb.AppendLine("**Remaining Steps:**");
                            foreach (var step in remainingSteps)
                            {
                                sb.AppendLine($"- Step {step.Index}: {step.Title}");
                                sb.AppendLine($"  {step.Description}");
                                sb.AppendLine();
                            }
                        }
                    }
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Add task description
            sb.AppendLine("## Your Task");
            sb.AppendLine();
            sb.AppendLine(TaskDescription ?? "");
            sb.AppendLine();

            if (ImplementationPlan != null)
            {
                if (IsParallelMode())
                {
                    // Instructions for parallel mode
                    sb.AppendLine("**Important**: You are executing in PARALLEL MODE.");
                    sb.AppendLine("1. Work ONLY on your assigned steps listed above");
                    sb.AppendLine("2. Complete each step and call `update_plan_step` with status \"completed\"");
                    sb.AppendLine("3. If a step fails, call `update_plan_step` with status \"failed\" and explain why");
                    sb.AppendLine("4. Other Kobolds are working on different steps - do NOT modify files outside your assigned steps");
                }
                else
                {
                    // Instructions for sequential mode
                    sb.AppendLine("**Important**: Follow the implementation plan above. For each step:");
                    sb.AppendLine("1. Complete the step's work (create/modify files as specified)");
                    sb.AppendLine("2. Verify your work is correct");
                    sb.AppendLine("3. Call `update_plan_step` with the step number and status \"completed\"");
                    sb.AppendLine("4. Move to the next step");
                    sb.AppendLine();
                    sb.AppendLine("If a step fails, call `update_plan_step` with status \"failed\" and explain why.");
                    sb.AppendLine("If a step should be skipped, call `update_plan_step` with status \"skipped\" and explain why.");
                }
            }
            else if (!string.IsNullOrEmpty(SpecificationContext))
            {
                sb.AppendLine("**Important**: Keep the project specification in mind while completing this task. Ensure your implementation aligns with the overall project requirements and architecture described above.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Phase 4: Checks if this Kobold is in parallel execution mode
        /// </summary>
        private bool IsParallelMode()
        {
            return _assignedStepIndices != null && _assignedStepIndices.Count > 0
                && ImplementationPlan != null && _assignedStepIndices.Count < ImplementationPlan.Steps.Count;
        }

        /// <summary>
        /// Appends workspace state information to the prompt
        /// </summary>
        private async Task AppendWorkspaceStateAsync(System.Text.StringBuilder sb, SharedPlanningContextService? sharedPlanningContext)
        {
            sb.AppendLine("## Workspace State");
            sb.AppendLine();

            try
            {
                var workspacePath = Agent.Options.WorkingDirectory;
                if (!Directory.Exists(workspacePath))
                {
                    sb.AppendLine("Workspace directory does not exist yet. You will be creating it.");
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                    return;
                }

                // Get existing files
                var files = Directory.GetFiles(workspacePath, "*.*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(workspacePath, f).Replace('\\', '/'))
                    .Where(f => !f.StartsWith(".git/") && 
                               !f.StartsWith("bin/") && 
                               !f.StartsWith("obj/") && 
                               !f.StartsWith("node_modules/") &&
                               !f.StartsWith(".vs/"))
                    .ToList();

                if (!files.Any())
                {
                    sb.AppendLine("The workspace is currently empty.");
                    sb.AppendLine();
                }
                else
                {
                    // Get files in use by other agents
                    HashSet<string>? filesInUse = null;
                    Dictionary<string, FileMetadata>? fileMetadata = null;
                    
                    if (sharedPlanningContext != null && !string.IsNullOrEmpty(ProjectId))
                    {
                        filesInUse = await sharedPlanningContext.GetFilesInUseAsync(ProjectId);
                        fileMetadata = await sharedPlanningContext.GetFileMetadataAsync(ProjectId);
                    }

                    sb.AppendLine("**Existing files in workspace:**");
                    sb.AppendLine();

                    // Categorize by type
                    var filesByCategory = files
                        .GroupBy(f => GetFileCategoryForDisplay(f))
                        .OrderBy(g => g.Key);

                    foreach (var category in filesByCategory)
                    {
                        sb.AppendLine($"**{category.Key}:**");
                        foreach (var file in category.OrderBy(f => f))
                        {
                            var inUseMarker = filesInUse != null && filesInUse.Contains(file) ? " 🔒 (being modified by another agent)" : "";
                            
                            // Add file purpose if available
                            var purpose = "";
                            if (fileMetadata != null && fileMetadata.TryGetValue(file, out var meta))
                            {
                                if (!string.IsNullOrWhiteSpace(meta.Purpose))
                                {
                                    var purposeText = meta.Purpose.Length > 100 
                                        ? meta.Purpose.Substring(0, 100) + "..." 
                                        : meta.Purpose;
                                    purpose = $"\n  Purpose: {purposeText}";
                                }
                            }
                            
                            sb.AppendLine($"- {file}{inUseMarker}{purpose}");
                        }
                        sb.AppendLine();
                    }

                    sb.AppendLine("**CRITICAL FILE RULES**: ");
                    sb.AppendLine("- Files listed above ALREADY EXIST - you MUST use `read_file` first, then `edit_file` to modify them");
                    sb.AppendLine("- **NEVER use `edit_file` without reading the file first** - you must see the actual content to provide the correct `old_text`");
                    sb.AppendLine("- **NEVER guess what's in a file** - always `read_file` to see the real content before editing");
                    sb.AppendLine("- Files marked 🔒 are currently being worked on - avoid modifying them to prevent conflicts");
                    sb.AppendLine("- Check the plan's `filesToCreate` vs `filesToModify` to know which operation to use");
                    sb.AppendLine("- Consider each file's purpose when deciding how to modify it");
                    sb.AppendLine("- Before using `write_file`, verify the file doesn't exist above");
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ Could not read workspace state: {ex.Message}");
                sb.AppendLine("Proceed with caution - use list_files tool to check before creating files.");
                sb.AppendLine();
                _logger?.LogWarning(ex, "Failed to build workspace state for Kobold {KoboldId}", Id.ToString()[..8]);
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        /// <summary>
        /// Appends execution insights from similar tasks to help avoid repeating mistakes
        /// and benefit from past successes
        /// </summary>
        private async Task AppendExecutionInsightsAsync(
            System.Text.StringBuilder sb,
            SharedPlanningContextService? sharedPlanningContext)
        {
            if (sharedPlanningContext == null || string.IsNullOrEmpty(ProjectId))
            {
                return;
            }

            try
            {
                var hasAnyInsights = false;
                var insights = new System.Text.StringBuilder();

                // Get similar task insights from this project
                var similarTasks = await sharedPlanningContext.GetSimilarTaskInsightsAsync(
                    ProjectId, AgentType, maxResults: 5);

                if (similarTasks.Any())
                {
                    hasAnyInsights = true;
                    insights.AppendLine("### Similar Tasks in This Project");
                    insights.AppendLine();
                    insights.AppendLine("Recent tasks of your type completed successfully:");
                    insights.AppendLine();

                    foreach (var task in similarTasks.Take(3))
                    {
                        var duration = task.DurationSeconds < 60
                            ? $"{task.DurationSeconds:F0}s"
                            : $"{task.DurationSeconds / 60:F1}m";
                        insights.AppendLine($"- Completed in {duration} ({task.CompletedSteps}/{task.StepCount} steps, {task.TotalIterations} iterations)");

                        // CONTEXT SLIPPING FIX: Include lessons learned from similar tasks
                        if (task.ResolvedIssues.Any())
                        {
                            insights.AppendLine("  - Issues resolved:");
                            foreach (var issue in task.ResolvedIssues.Take(2))
                            {
                                var issueSummary = issue.Length > 80 ? issue.Substring(0, 80) + "..." : issue;
                                insights.AppendLine($"    • {issueSummary}");
                            }
                        }

                        if (task.SuccessfulPatterns.Any())
                        {
                            insights.AppendLine("  - What worked:");
                            foreach (var pattern in task.SuccessfulPatterns.Take(2))
                            {
                                var patternSummary = pattern.Length > 80 ? pattern.Substring(0, 80) + "..." : pattern;
                                insights.AppendLine($"    • {patternSummary}");
                            }
                        }
                    }
                    insights.AppendLine();
                }

                // Get cross-project insights
                var crossProjectInsights = await sharedPlanningContext.GetCrossProjectInsightsAsync(
                    ProjectId, AgentType, maxResults: 3);

                if (crossProjectInsights.Any())
                {
                    hasAnyInsights = true;
                    insights.AppendLine("### Cross-Project Patterns");
                    insights.AppendLine();
                    insights.AppendLine("Approaches that worked in similar projects:");
                    insights.AppendLine();

                    var avgSteps = crossProjectInsights.Any() ? crossProjectInsights.Average(i => i.StepCount) : 0;
                    var avgIterations = crossProjectInsights.Any() ? crossProjectInsights.Average(i => i.TotalIterations) : 0;

                    insights.AppendLine($"- Typical complexity: {avgSteps:F1} steps, {avgIterations:F0} iterations");

                    if (crossProjectInsights.Any(i => i.DurationSeconds < 30))
                    {
                        insights.AppendLine("- Quick tasks are often solvable in under 30 seconds");
                    }

                    insights.AppendLine();
                }

                // Get best practices for this agent type
                var bestPractices = await sharedPlanningContext.GetBestPracticesAsync(AgentType);
                if (bestPractices.Any())
                {
                    hasAnyInsights = true;
                    insights.AppendLine("### Best Practices for Your Agent Type");
                    insights.AppendLine();

                    foreach (var practice in bestPractices)
                    {
                        insights.AppendLine($"- **{practice.Key}**: {practice.Value}");
                    }
                    insights.AppendLine();
                }

                // Get related plans that worked on similar files
                if (ImplementationPlan != null && TaskId.HasValue)
                {
                    var relatedFiles = new List<string>();
                    foreach (var step in ImplementationPlan.Steps)
                    {
                        relatedFiles.AddRange(step.FilesToCreate);
                        relatedFiles.AddRange(step.FilesToModify);
                    }

                    if (relatedFiles.Any())
                    {
                        var relatedPlans = await sharedPlanningContext.GetRelatedPlansAsync(
                            ProjectId ?? "", TaskId.Value.ToString(), relatedFiles);

                        if (relatedPlans.Any())
                        {
                            hasAnyInsights = true;
                            insights.AppendLine("### Related Completed Tasks");
                            insights.AppendLine();
                            insights.AppendLine("These tasks worked on similar files. Review their approaches:");

                            foreach (var plan in relatedPlans.Take(3))
                            {
                                var taskDesc = plan.TaskDescription.Length > 50
                                    ? plan.TaskDescription.Substring(0, 50) + "..."
                                    : plan.TaskDescription;

                                insights.AppendLine($"- **{taskDesc}**");
                                insights.AppendLine($"  - Status: {plan.Status}");
                                insights.AppendLine($"  - Steps: {plan.CompletedStepsCount}/{plan.Steps.Count} completed");

                                // Show shared files
                                var sharedFiles = new HashSet<string>();
                                foreach (var step in plan.Steps)
                                {
                                    foreach (var file in step.FilesToCreate.Concat(step.FilesToModify))
                                    {
                                        if (relatedFiles.Contains(file))
                                        {
                                            sharedFiles.Add(file);
                                        }
                                    }
                                }

                                if (sharedFiles.Any())
                                {
                                    insights.AppendLine($"  - Shared files: {string.Join(", ", sharedFiles.Take(3))}{(sharedFiles.Count > 3 ? "..." : "")}");
                                }

                                insights.AppendLine();
                            }
                        }
                    }
                }

                // Only add the section if we have insights
                if (hasAnyInsights)
                {
                    sb.AppendLine("## Execution Insights");
                    sb.AppendLine();
                    sb.AppendLine("This section shows patterns from similar completed tasks. Use this information to:");
                    sb.AppendLine("- Avoid approaches that have failed before");
                    sb.AppendLine("- Follow patterns that have worked well");
                    sb.AppendLine("- Estimate task complexity realistically");
                    sb.AppendLine();
                    sb.AppendLine(insights.ToString());
                    sb.AppendLine("---");
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                // Don't fail the prompt build if insights can't be loaded
                _logger?.LogDebug(ex, "Could not load execution insights for Kobold {KoboldId}", Id.ToString()[..8]);
            }
        }

        /// <summary>
        /// Appends file location guidelines to ensure files are created in correct directories
        /// </summary>
        private void AppendFileLocationGuidelines(System.Text.StringBuilder sb)
        {
            sb.AppendLine("## File Location Guidelines");
            sb.AppendLine();

            // Use project-specific structure if available
            var hasProjectGuidelines = ProjectStructure != null &&
                (ProjectStructure.DirectoryPurposes.Any() || ProjectStructure.FileLocationGuidelines.Any());

            if (hasProjectGuidelines && ProjectStructure != null)
            {
                if (ProjectStructure.NamingConventions.Any())
                {
                    sb.AppendLine("**Naming Conventions:**");
                    foreach (var convention in ProjectStructure.NamingConventions)
                    {
                        sb.AppendLine($"- {convention.Key}: {convention.Value}");
                    }
                    sb.AppendLine();
                }

                if (ProjectStructure.DirectoryPurposes.Any())
                {
                    sb.AppendLine("**Directory Organization:**");
                    foreach (var dir in ProjectStructure.DirectoryPurposes)
                    {
                        sb.AppendLine($"- `{dir.Key}`: {dir.Value}");
                    }
                    sb.AppendLine();
                }

                if (ProjectStructure.FileLocationGuidelines.Any())
                {
                    sb.AppendLine("**File Placement Rules (MUST FOLLOW):**");
                    foreach (var guideline in ProjectStructure.FileLocationGuidelines)
                    {
                        sb.AppendLine($"- {guideline.Key} files → `{guideline.Value}`");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                // Default file location guidelines when no project-specific structure is provided
                sb.AppendLine("**Default File Placement Rules (MUST FOLLOW):**");
                sb.AppendLine();
                sb.AppendLine("**Web Projects (HTML/JS/CSS):**");
                sb.AppendLine("- `index.html` → root folder (ONLY the main HTML entry point goes in root)");
                sb.AppendLine("- JavaScript/TypeScript files → `js/` or `src/` folder (e.g., `js/app.js`) - **NEVER in root**");
                sb.AppendLine("- CSS/Stylesheets → `css/` folder (e.g., `css/styles.css`) - **NEVER in root**");
                sb.AppendLine("- Images/Assets → `assets/` or `assets/images/` folder");
                sb.AppendLine("- Components → `components/` folder for reusable UI pieces");
                sb.AppendLine();
                sb.AppendLine("**Backend Projects:**");
                sb.AppendLine("- Source code → `src/` folder");
                sb.AppendLine("- Tests → `tests/` folder");
                sb.AppendLine("- Configuration → `config/` folder");
                sb.AppendLine();
            }

            sb.AppendLine("**CRITICAL**: `.js`, `.ts`, and `.css` files must **NEVER** be placed in the root folder.");
            sb.AppendLine("Only config files (package.json, tsconfig.json) and HTML entry points (index.html) belong in root.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        /// <summary>
        /// Categorizes a file by its type for display in prompts
        /// </summary>
        private string GetFileCategoryForDisplay(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".cs" => "C# Source Files",
                ".csproj" => "Project Files",
                ".sln" or ".slnx" => "Solution Files",
                ".json" => "Configuration Files",
                ".js" or ".ts" => "JavaScript/TypeScript",
                ".jsx" or ".tsx" => "React Components",
                ".css" or ".scss" or ".sass" => "Stylesheets",
                ".html" or ".htm" => "HTML Files",
                ".md" => "Documentation",
                ".xml" => "XML Files",
                ".yml" or ".yaml" => "YAML Configuration",
                ".txt" => "Text Files",
                ".py" => "Python Files",
                ".java" => "Java Files",
                ".cpp" or ".hpp" or ".h" or ".c" => "C/C++ Files",
                ".go" => "Go Files",
                ".rs" => "Rust Files",
                _ => "Other Files"
            };
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
                // Flush to ensure final state is saved immediately (bypasses debouncing)
                await planService.FlushPlanAsync(ProjectId, ImplementationPlan.TaskId);
            }
        }

        /// <summary>
        /// Checks if the agent's message history contains errors (stop reason "error" or "NotConfigured").
        /// Returns the actual error message if found, null otherwise.
        /// </summary>
        private string? ExtractErrorFromMessages(List<Message> messages)
        {
            // Check the last assistant message for error indicators
            var lastAssistantMsg = messages.LastOrDefault(m => m.Role == "assistant");
            if (lastAssistantMsg == null) return null;

            // Check if the conversation ended with an error stop reason
            // Look for error messages in the message content
            if (lastAssistantMsg.Content is IEnumerable<ContentBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (block.Type == "text" && block.Text != null)
                    {
                        var text = block.Text.ToLowerInvariant();
                        // Check for common error indicators
                        if (text.Contains("error: llm request failed:") ||
                            text.Contains("error occurred during llm request") ||
                            text.Contains("error:") && text.Contains("llm request") ||
                            text.Contains("provider") && text.Contains("not properly configured") ||
                            text.Contains("error:") && text.Contains("provider"))
                        {
                            // Return the actual error text (not lowercased) for display
                            return block.Text;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the agent's message history contains errors (stop reason "error" or "NotConfigured")
        /// </summary>
        private bool HasErrorInMessages(List<Message> messages)
        {
            return ExtractErrorFromMessages(messages) != null;
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
            LastLlmResponseAt = null;
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
