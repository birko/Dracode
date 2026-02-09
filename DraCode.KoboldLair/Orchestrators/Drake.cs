using System.Threading.Channels;
using DraCode.Agent;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Orchestrators
{
    /// <summary>
    /// Drake is a supervisor that manages the lifecycle of Kobolds based on task status.
    /// It monitors tasks, creates Kobolds for execution, and updates task status based on Kobold progress.
    /// Supports implementation plans for task resumability and visibility.
    /// </summary>
    public class Drake
    {
        private readonly KoboldFactory _koboldFactory;
        private readonly TaskTracker _taskTracker;
        private readonly string _outputMarkdownPath;
        private readonly string _defaultProvider;
        private readonly Dictionary<string, string>? _defaultConfig;
        private readonly AgentOptions? _defaultOptions;
        private readonly string? _specificationPath;
        private readonly string? _projectId;
        private readonly ILogger<Drake>? _logger;
        private readonly ProviderConfigurationService? _providerConfigService;
        private readonly ProjectConfigurationService? _projectConfigService;
        private readonly GitService? _gitService;
        private readonly KoboldPlanService? _planService;
        private readonly KoboldPlannerAgent? _plannerAgent;
        private readonly bool _planningEnabled;
        private readonly bool _useEnhancedExecution;
        private readonly bool _allowPlanModifications;
        private readonly bool _autoApproveModifications;
        private readonly bool _filterFilesByPlan;
        private readonly PlanFileFilterService _fileFilterService;
        private Wyvern? _wyvern;

        // Debounced file write support
        private readonly Channel<bool> _saveChannel;
        private readonly Task _saveTask;
        private readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Maps TaskId to KoboldId for tracking active assignments
        /// </summary>
        private readonly Dictionary<string, Guid> _taskToKoboldMap;

        /// <summary>
        /// Creates a new Drake supervisor
        /// </summary>
        /// <param name="koboldFactory">Factory for managing Kobolds</param>
        /// <param name="taskTracker">Tracker for managing tasks</param>
        /// <param name="outputMarkdownPath">Path to markdown output file</param>
        /// <param name="defaultProvider">Default LLM provider for Kobolds</param>
        /// <param name="defaultConfig">Default provider configuration</param>
        /// <param name="defaultOptions">Default agent options</param>
        /// <param name="specificationPath">Optional path to project specification</param>
        /// <param name="projectId">Optional project identifier for resource limiting</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <param name="providerConfigService">Optional provider configuration service for agent-type-specific providers</param>
        /// <param name="projectConfigService">Optional project configuration service for external path access</param>
        /// <param name="gitService">Optional git service for committing changes on task completion</param>
        /// <param name="planService">Optional plan service for implementation plan persistence</param>
        /// <param name="plannerAgent">Optional planner agent for creating implementation plans</param>
        /// <param name="planningEnabled">Whether to enable implementation planning (default: true if planService and plannerAgent are provided)</param>
        /// <param name="useEnhancedExecution">Whether to use Phase 2 enhanced execution with auto-detection (default: true)</param>
        /// <param name="allowPlanModifications">Whether to allow agent-suggested plan modifications (default: false)</param>
        /// <param name="autoApproveModifications">Whether to auto-approve plan modifications (default: false)</param>
        /// <param name="filterFilesByPlan">Whether to filter file structure by plan requirements (default: true)</param>
        public Drake(
            KoboldFactory koboldFactory,
            TaskTracker taskTracker,
            string outputMarkdownPath,
            string defaultProvider = "openai",
            Dictionary<string, string>? defaultConfig = null,
            AgentOptions? defaultOptions = null,
            string? specificationPath = null,
            string? projectId = null,
            ILogger<Drake>? logger = null,
            ProviderConfigurationService? providerConfigService = null,
            ProjectConfigurationService? projectConfigService = null,
            GitService? gitService = null,
            KoboldPlanService? planService = null,
            KoboldPlannerAgent? plannerAgent = null,
            bool? planningEnabled = null,
            bool useEnhancedExecution = true,
            bool allowPlanModifications = false,
            bool autoApproveModifications = false,
            bool filterFilesByPlan = true)
        {
            _koboldFactory = koboldFactory;
            _taskTracker = taskTracker;
            _outputMarkdownPath = outputMarkdownPath;
            _defaultProvider = defaultProvider;
            _defaultConfig = defaultConfig;
            _defaultOptions = defaultOptions;
            _specificationPath = specificationPath;
            _projectId = projectId;
            _logger = logger;
            _providerConfigService = providerConfigService;
            _projectConfigService = projectConfigService;
            _gitService = gitService;
            _planService = planService;
            _plannerAgent = plannerAgent;
            _planningEnabled = planningEnabled ?? (planService != null && plannerAgent != null);
            _useEnhancedExecution = useEnhancedExecution && _planningEnabled; // Only use enhanced if planning is enabled
            _allowPlanModifications = allowPlanModifications;
            _autoApproveModifications = autoApproveModifications;
            _filterFilesByPlan = filterFilesByPlan && _planningEnabled; // Only filter if planning is enabled
            _fileFilterService = new PlanFileFilterService(null); // Logger is optional for filter service
            _taskToKoboldMap = new Dictionary<string, Guid>();

            // Initialize debounced save channel (bounded to 1 to coalesce writes)
            _saveChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite // Discard if already queued
            });
            _saveTask = ProcessSaveQueueAsync();
        }

        /// <summary>
        /// Gets whether implementation planning is enabled
        /// </summary>
        public bool PlanningEnabled => _planningEnabled;

        /// <summary>
        /// Gets the plan service (if configured)
        /// </summary>
        public KoboldPlanService? PlanService => _planService;

        /// <summary>
        /// Gets the project ID this Drake is managing
        /// </summary>
        public string? ProjectId => _projectId;

        /// <summary>
        /// Gets the task file path this Drake is monitoring
        /// </summary>
        public string TaskFilePath => _outputMarkdownPath;

        /// <summary>
        /// Gets the name of this Drake (derived from task file name)
        /// </summary>
        public string Name => Path.GetFileNameWithoutExtension(_outputMarkdownPath);

        /// <summary>
        /// Sets the Wyvern for this Drake to enable feature status updates
        /// </summary>
        public void SetWyvern(Wyvern wyvern)
        {
            _wyvern = wyvern;
        }

        /// <summary>
        /// Summons (creates) a Kobold for a specific task
        /// </summary>
        /// <param name="task">Task to assign</param>
        /// <param name="agentType">Type of agent to create</param>
        /// <param name="provider">LLM provider (optional, resolves by agent type if not specified)</param>
        /// <returns>The summoned Kobold, or null if resource limit reached</returns>
        public Kobold? SummonKobold(TaskRecord task, string agentType, string? provider = null)
        {
            // Resolve provider: explicit override > agent-type-specific > global default
            var effectiveProvider = provider
                ?? _providerConfigService?.GetProviderForKoboldAgentType(agentType)
                ?? _defaultProvider;
            var projectId = task.ProjectId ?? _projectId;

            // Check if we can create a kobold for this project (resource limit check)
            if (!_koboldFactory.CanCreateKoboldForProject(projectId))
            {
                _logger?.LogDebug(
                    "Cannot summon kobold for task {TaskId} - project {ProjectId} has reached its parallel kobold limit",
                    task.Id, projectId ?? "(default)");
                return null;
            }

            // Create options with workspace path and external paths
            var effectiveOptions = _defaultOptions?.Clone() ?? new AgentOptions();

            // Set working directory to project workspace folder
            // Derive from task file path: ./projects/my-project/tasks/backend-tasks.md -> ./projects/my-project/workspace
            var taskFolder = Path.GetDirectoryName(_outputMarkdownPath);
            var projectFolder = !string.IsNullOrEmpty(taskFolder) ? Path.GetDirectoryName(taskFolder) : null;
            if (!string.IsNullOrEmpty(projectFolder))
            {
                var workspacePath = Path.Combine(projectFolder, "workspace");
                effectiveOptions.WorkingDirectory = workspacePath;
                _logger?.LogDebug("Kobold workspace set to {WorkspacePath}", workspacePath);
            }

            // Add external paths if available
            if (!string.IsNullOrEmpty(projectId) && _projectConfigService != null)
            {
                var externalPaths = _projectConfigService.GetAllowedExternalPaths(projectId);
                if (externalPaths.Count > 0)
                {
                    effectiveOptions.AllowedExternalPaths = externalPaths.ToList();
                    _logger?.LogDebug("Kobold for project {ProjectId} has {Count} allowed external paths",
                        projectId, externalPaths.Count);
                }
            }

            // Create the Kobold
            var kobold = _koboldFactory.CreateKobold(
                effectiveProvider,
                agentType,
                effectiveOptions,
                _defaultConfig
            );

            // Load specification context if available
            string? specificationContext = null;
            if (!string.IsNullOrEmpty(_specificationPath) && File.Exists(_specificationPath))
            {
                try
                {
                    specificationContext = File.ReadAllText(_specificationPath);
                    
                    // Append project structure information if available
                    if (_wyvern?.Analysis?.Structure != null)
                    {
                        var structure = _wyvern.Analysis.Structure;
                        var structureInfo = new System.Text.StringBuilder();
                        
                        structureInfo.AppendLine();
                        structureInfo.AppendLine("---");
                        structureInfo.AppendLine();
                        structureInfo.AppendLine("## Project Structure Context");
                        structureInfo.AppendLine();
                        
                        if (!string.IsNullOrEmpty(structure.ArchitectureNotes))
                        {
                            structureInfo.AppendLine("### Architecture Notes");
                            structureInfo.AppendLine(structure.ArchitectureNotes);
                            structureInfo.AppendLine();
                        }
                        
                        if (structure.DirectoryPurposes.Any())
                        {
                            structureInfo.AppendLine("### Directory Organization");
                            foreach (var kvp in structure.DirectoryPurposes)
                            {
                                structureInfo.AppendLine($"- `{kvp.Key}`: {kvp.Value}");
                            }
                            structureInfo.AppendLine();
                        }
                        
                        if (structure.FileLocationGuidelines.Any())
                        {
                            structureInfo.AppendLine("### File Location Guidelines");
                            foreach (var kvp in structure.FileLocationGuidelines)
                            {
                                structureInfo.AppendLine($"- {kvp.Key} files → `{kvp.Value}`");
                            }
                            structureInfo.AppendLine();
                        }
                        
                        if (structure.NamingConventions.Any())
                        {
                            structureInfo.AppendLine("### Naming Conventions");
                            foreach (var kvp in structure.NamingConventions)
                            {
                                structureInfo.AppendLine($"- {kvp.Key}: {kvp.Value}");
                            }
                            structureInfo.AppendLine();
                        }
                        
                        if (structure.ExistingFiles.Any())
                        {
                            structureInfo.AppendLine($"### Existing Files ({structure.ExistingFiles.Count} files)");
                            structureInfo.AppendLine("```");
                            foreach (var file in structure.ExistingFiles.Take(50))
                            {
                                structureInfo.AppendLine(file);
                            }
                            if (structure.ExistingFiles.Count > 50)
                            {
                                structureInfo.AppendLine($"... and {structure.ExistingFiles.Count - 50} more files");
                            }
                            structureInfo.AppendLine("```");
                        }
                        
                        specificationContext += structureInfo.ToString();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not load specification from {Path}", _specificationPath);
                }
            }

            // Extract project structure from Wyvern analysis if available
            var projectStructure = _wyvern?.Analysis?.Structure;

            // Assign the task with description, project, specification context, and structure
            kobold.AssignTask(Guid.Parse(task.Id), task.Task, projectId, specificationContext, projectStructure);

            // Track the mapping
            _taskToKoboldMap[task.Id] = kobold.Id;

            // Update task status
            _taskTracker.UpdateTask(task, TaskStatus.NotInitialized, agentType);
            SaveTasksToFile();

            return kobold;
        }

        /// <summary>
        /// Updates the kobold's specification context with plan-aware file filtering.
        /// Should be called after the implementation plan is created.
        /// </summary>
        /// <param name="kobold">Kobold to update</param>
        public void UpdateKoboldSpecificationWithPlan(Kobold kobold)
        {
            if (!_filterFilesByPlan || kobold.ImplementationPlan == null || _wyvern?.Analysis?.Structure == null)
            {
                return; // Nothing to do
            }

            var structure = _wyvern.Analysis.Structure;
            if (!structure.ExistingFiles.Any())
            {
                return; // No files to filter
            }

            // Load original specification if available
            string? baseSpecification = null;
            if (!string.IsNullOrEmpty(_specificationPath) && File.Exists(_specificationPath))
            {
                try
                {
                    baseSpecification = File.ReadAllText(_specificationPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not reload specification from {Path}", _specificationPath);
                    return; // Can't update without base spec
                }
            }

            // Filter files based on plan
            var filteredFiles = _fileFilterService.FilterRelevantFiles(structure.ExistingFiles, kobold.ImplementationPlan);
            
            _logger?.LogDebug(
                "Updated file list for kobold {KoboldId} with plan: {OriginalCount} → {FilteredCount} files",
                kobold.Id.ToString()[..8], structure.ExistingFiles.Count, filteredFiles.Count);

            // Rebuild structure info with filtered files
            var structureInfo = new System.Text.StringBuilder();
            
            structureInfo.AppendLine();
            structureInfo.AppendLine("---");
            structureInfo.AppendLine();
            structureInfo.AppendLine("## Project Structure Context");
            structureInfo.AppendLine();
            
            if (!string.IsNullOrEmpty(structure.ArchitectureNotes))
            {
                structureInfo.AppendLine("### Architecture Notes");
                structureInfo.AppendLine(structure.ArchitectureNotes);
                structureInfo.AppendLine();
            }
            
            if (structure.DirectoryPurposes.Any())
            {
                structureInfo.AppendLine("### Directory Organization");
                foreach (var kvp in structure.DirectoryPurposes)
                {
                    structureInfo.AppendLine($"- `{kvp.Key}`: {kvp.Value}");
                }
                structureInfo.AppendLine();
            }
            
            if (structure.FileLocationGuidelines.Any())
            {
                structureInfo.AppendLine("### File Location Guidelines");
                foreach (var kvp in structure.FileLocationGuidelines)
                {
                    structureInfo.AppendLine($"- {kvp.Key} files → `{kvp.Value}`");
                }
                structureInfo.AppendLine();
            }
            
            if (structure.NamingConventions.Any())
            {
                structureInfo.AppendLine("### Naming Conventions");
                foreach (var kvp in structure.NamingConventions)
                {
                    structureInfo.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
                structureInfo.AppendLine();
            }
            
            if (filteredFiles.Any())
            {
                structureInfo.AppendLine($"### Relevant Files ({filteredFiles.Count} of {structure.ExistingFiles.Count} total)");
                structureInfo.AppendLine("*(Filtered to show only files relevant to your implementation plan)*");
                structureInfo.AppendLine("```");
                foreach (var file in filteredFiles.Take(50))
                {
                    structureInfo.AppendLine(file);
                }
                if (filteredFiles.Count > 50)
                {
                    structureInfo.AppendLine($"... and {filteredFiles.Count - 50} more files");
                }
                structureInfo.AppendLine("```");
            }
            
            // Update kobold's specification context
            var updatedContext = (baseSpecification ?? "") + structureInfo.ToString();
            kobold.UpdateSpecificationContext(updatedContext);
        }

        /// <summary>
        /// Unsummons (destroys) a Kobold and removes it from the factory
        /// </summary>
        /// <param name="koboldId">ID of the Kobold to unsummon</param>
        /// <returns>True if unsummoned successfully</returns>
        public bool UnsummonKobold(Guid koboldId)
        {
            var kobold = _koboldFactory.GetKobold(koboldId);
            if (kobold == null)
                return false;

            // Remove from task mapping
            if (kobold.TaskId.HasValue)
            {
                var taskIdString = kobold.TaskId.Value.ToString();
                _taskToKoboldMap.Remove(taskIdString);
            }

            // Remove from factory
            return _koboldFactory.RemoveKobold(koboldId);
        }

        /// <summary>
        /// Starts a Kobold working on its assigned task (executes the task automatically).
        /// The Kobold manages its own state transitions during execution.
        /// </summary>
        /// <param name="koboldId">ID of the Kobold to start</param>
        /// <param name="maxIterations">Maximum iterations for agent execution</param>
        /// <returns>Messages from agent execution</returns>
        public async Task<List<Message>> StartKoboldWorkAsync(Guid koboldId, int maxIterations = 30)
        {
            var kobold = _koboldFactory.GetKobold(koboldId);
            if (kobold == null)
                throw new InvalidOperationException($"Kobold {koboldId} not found");

            if (!kobold.TaskId.HasValue)
                throw new InvalidOperationException($"Kobold {koboldId} has no assigned task");

            // Update task status to Working (informational only - Kobold controls its own state)
            var task = _taskTracker.GetTaskById(kobold.TaskId.Value.ToString());
            if (task != null)
            {
                _taskTracker.UpdateTask(task, TaskStatus.Working);
                SaveTasksToFile();
            }

            // Start the Kobold (this executes the task and manages state automatically)
            var messages = await kobold.StartWorkingAsync(maxIterations);

            // Sync task status from Kobold's final state
            await SyncTaskFromKoboldAsync(kobold);

            return messages;
        }

        /// <summary>
        /// Syncs task status from Kobold's current state.
        /// Drake observes Kobold state but does not forcefully change it.
        /// Also commits changes to git when a task completes successfully.
        /// </summary>
        /// <param name="kobold">Kobold to sync from</param>
        private void SyncTaskFromKobold(Kobold kobold)
        {
            SyncTaskFromKoboldAsync(kobold).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Syncs task status from Kobold's current state (async version).
        /// Drake observes Kobold state but does not forcefully change it.
        /// Also commits changes to git when a task completes successfully.
        /// </summary>
        /// <param name="kobold">Kobold to sync from</param>
        private async Task SyncTaskFromKoboldAsync(Kobold kobold)
        {
            if (!kobold.TaskId.HasValue)
                return;

            var task = _taskTracker.GetTaskById(kobold.TaskId.Value.ToString());
            if (task == null)
                return;

            // Store previous status to detect transitions
            var previousStatus = task.Status;

            // Map Kobold status to Task status
            var taskStatus = kobold.Status switch
            {
                KoboldStatus.Unassigned => TaskStatus.Unassigned,
                KoboldStatus.Assigned => TaskStatus.NotInitialized,
                KoboldStatus.Working => TaskStatus.Working,
                KoboldStatus.Done => TaskStatus.Done,
                KoboldStatus.Failed => TaskStatus.Failed,
                _ => task.Status // Keep current if unknown
            };

            // Update task with Kobold's error if present
            if (kobold.HasError)
            {
                _taskTracker.SetError(task, kobold.ErrorMessage ?? "Unknown error");
            }

            _taskTracker.UpdateTask(task, taskStatus);

            // Log status transition if changed
            if (previousStatus != taskStatus)
            {
                var projectInfo = _projectId ?? "unknown project";
                var taskPreview = task.Task.Length > 60 ? task.Task.Substring(0, 60) + "..." : task.Task;
                _logger?.LogInformation(
                    "Task status updated: {OldStatus} → {NewStatus}\n" +
                    "  Project: {ProjectId}\n" +
                    "  Task ID: {TaskId}\n" +
                    "  Task: {TaskDescription}",
                    previousStatus, taskStatus, projectInfo, task.Id[..Math.Min(8, task.Id.Length)], taskPreview);
            }

            // Use immediate save for task completion to prevent race condition with ReloadTasksFromFileAsync
            // (debounced save could be overwritten by reload before it executes)
            if (taskStatus == TaskStatus.Done)
            {
                await SaveTasksToFileAsync();
            }
            else
            {
                SaveTasksToFile();
            }

            // Commit changes to git when task completes successfully
            if (previousStatus != TaskStatus.Done && taskStatus == TaskStatus.Done && kobold.IsSuccess)
            {
                await CommitTaskCompletionAsync(kobold, task);
            }
        }

        /// <summary>
        /// Commits changes to git when a task is completed
        /// </summary>
        private async Task CommitTaskCompletionAsync(Kobold kobold, TaskRecord task)
        {
            if (_gitService == null)
                return;

            try
            {
                // Get the project folder (output path from task file path)
                var projectFolder = Path.GetDirectoryName(_outputMarkdownPath);
                if (string.IsNullOrEmpty(projectFolder))
                    return;

                if (!await _gitService.IsGitInstalledAsync())
                    return;

                if (!await _gitService.IsRepositoryAsync(projectFolder))
                    return;

                // Get current branch - we commit to whatever branch we're on
                // Feature branches are managed by Wyvern when features are assigned
                var currentBranch = await _gitService.GetCurrentBranchAsync(projectFolder);

                // Stage all changes
                await _gitService.StageAllAsync(projectFolder);

                // Create commit message
                var shortTaskDesc = task.Task.Length > 50 ? task.Task[..50] + "..." : task.Task;
                var commitMessage = $"Task completed: {shortTaskDesc}\n\nTask ID: {task.Id}\nAgent: {kobold.AgentType}";

                // Commit with Kobold agent type as author
                var authorName = $"Kobold-{kobold.AgentType}";
                var committed = await _gitService.CommitChangesAsync(projectFolder, commitMessage, authorName);

                if (committed)
                {
                    var projectInfo = _projectId ?? "unknown project";
                    _logger?.LogInformation(
                        "✓ Git commit successful\n" +
                        "  Project: {ProjectId}\n" +
                        "  Branch: {Branch}\n" +
                        "  Task: {Task}\n" +
                        "  Agent: {AgentType}",
                        projectInfo, currentBranch ?? "current branch", shortTaskDesc, kobold.AgentType);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to commit task completion for task {TaskId}", task.Id);
                // Don't fail the workflow if git commit fails
            }
        }

        /// <summary>
        /// Executes a task using a Kobold (summon, work, and automatically sync status).
        /// The Kobold manages its own state - Drake only observes and syncs to TaskTracker.
        /// If planning is enabled, creates or loads an implementation plan before execution.
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <param name="agentType">Type of agent to use</param>
        /// <param name="maxIterations">Maximum iterations for agent</param>
        /// <param name="provider">LLM provider (optional)</param>
        /// <param name="messageCallback">Callback for progress messages</param>
        /// <returns>Task result and associated Kobold, or null if resource limit reached</returns>
        public async Task<(List<Message> messages, Kobold kobold)?> ExecuteTaskAsync(
            TaskRecord task,
            string agentType,
            int maxIterations = 30,
            string? provider = null,
            Action<string, string>? messageCallback = null)
        {
            // Summon Kobold (may return null if resource limit reached)
            var kobold = SummonKobold(task, agentType, provider);

            if (kobold == null)
            {
                // Resource limit reached - task remains in queue for retry on next cycle
                var projectId = task.ProjectId ?? _projectId ?? "(default)";
                messageCallback?.Invoke("info", $"⏸️ Drake cannot summon kobold for task {task.Id.ToString()[..8]} - project {projectId} at parallel limit. Will retry.");
                return null;
            }

            messageCallback?.Invoke("info", $"🐉 Drake summoned Kobold {kobold.Id.ToString()[..8]} for task {task.Id.ToString()[..8]}");

            List<Message> messages;

            try
            {
                // Set message callback on agent
                if (messageCallback != null)
                {
                    kobold.Agent.SetMessageCallback(messageCallback);
                }

                // Ensure implementation plan exists if planning is enabled
                if (_planningEnabled && _planService != null && _plannerAgent != null)
                {
                    try
                    {
                        messageCallback?.Invoke("info", $"📋 Creating implementation plan for task {task.Id.ToString()[..8]}...");
                        var plan = await kobold.EnsurePlanAsync(_planService, _plannerAgent);
                        var isResume = plan.CurrentStepIndex > 0;
                        var planMsg = isResume
                            ? $"📋 Resuming from step {plan.CurrentStepIndex + 1}/{plan.Steps.Count}"
                            : $"📋 Plan ready with {plan.Steps.Count} steps";
                        messageCallback?.Invoke("info", planMsg);

                        // Update kobold specification context with plan-filtered files
                        UpdateKoboldSpecificationWithPlan(kobold);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail - Kobold can still work without a plan
                        _logger?.LogWarning(ex, "Failed to create implementation plan for task {TaskId}", task.Id);
                        messageCallback?.Invoke("warning", $"⚠️ Could not create implementation plan: {ex.Message}. Proceeding without plan.");
                    }
                }

                // Start work (Kobold automatically manages its state transitions)
                var projectInfo = kobold.ProjectId ?? _projectId ?? "unknown";
                var taskPreview = kobold.TaskDescription?.Length > 80 
                    ? kobold.TaskDescription.Substring(0, 80) + "..." 
                    : kobold.TaskDescription ?? "unknown task";
                messageCallback?.Invoke("info", 
                    $"⚡ Kobold {kobold.Id.ToString()[..8]} started working\n" +
                    $"  Project: {projectInfo}\n" +
                    $"  Task ID: {task.Id.ToString()[..8]}\n" +
                    $"  Task: {taskPreview}");

                // Use plan-aware execution if we have a plan
                if (kobold.ImplementationPlan != null)
                {
                    // Use enhanced execution if enabled (Phase 2 auto-detection)
                    if (_useEnhancedExecution)
                    {
                        messages = await kobold.StartWorkingWithPlanEnhancedAsync(
                            _planService, 
                            maxIterations,
                            _allowPlanModifications,
                            _autoApproveModifications);
                    }
                    else
                    {
                        messages = await kobold.StartWorkingWithPlanAsync(_planService, maxIterations);
                    }
                }
                else
                {
                    messages = await StartKoboldWorkAsync(kobold.Id, maxIterations);
                }

                // Check Kobold's final state
                if (kobold.IsSuccess)
                {
                    messageCallback?.Invoke("success", 
                        $"✓ Kobold {kobold.Id.ToString()[..8]} completed task successfully\n" +
                        $"  Project: {projectInfo}\n" +
                        $"  Task ID: {task.Id.ToString()[..8]}\n" +
                        $"  Task: {taskPreview}");
                }
                else if (kobold.HasError)
                {
                    messageCallback?.Invoke("error", 
                        $"✗ Kobold {kobold.Id.ToString()[..8]} failed: {kobold.ErrorMessage}\n" +
                        $"  Project: {projectInfo}\n" +
                        $"  Task ID: {task.Id.ToString()[..8]}\n" +
                        $"  Task: {taskPreview}");
                }
                else if (kobold.Status == KoboldStatus.Working && kobold.ImplementationPlan != null)
                {
                    var completedCount = kobold.ImplementationPlan.CompletedStepsCount;
                    var totalCount = kobold.ImplementationPlan.Steps.Count;
                    messageCallback?.Invoke("warning", 
                        $"⚠ Kobold {kobold.Id.ToString()[..8]} reached iteration limit - work incomplete ({completedCount}/{totalCount} steps done)\n" +
                        $"  Project: {projectInfo}\n" +
                        $"  Task ID: {task.Id.ToString()[..8]}\n" +
                        $"  Task: {taskPreview}");
                }
            }
            catch (Exception ex)
            {
                messageCallback?.Invoke("error", $"✗ Kobold {kobold.Id.ToString()[..8]} execution error: {ex.Message}");
                messages = new List<Message>();

                // Sync status from Kobold (it should have captured the error)
                await SyncTaskFromKoboldAsync(kobold);
                throw;
            }

            // Final sync of task status from Kobold
            await SyncTaskFromKoboldAsync(kobold);

            // Update feature status if Wyvern is available
            UpdateFeatureStatus();

            return (messages, kobold);
        }

        /// <summary>
        /// Monitors all tasks and syncs their status from associated Kobolds (async version).
        /// Drake observes Kobold states but does not force state changes.
        /// Also updates feature status through Wyvern if available.
        /// </summary>
        public async Task MonitorTasksAsync()
        {
            var allTasks = _taskTracker.GetAllTasks();

            foreach (var task in allTasks)
            {
                // Check if task has an associated Kobold
                if (_taskToKoboldMap.TryGetValue(task.Id, out var koboldId))
                {
                    var kobold = _koboldFactory.GetKobold(koboldId);
                    if (kobold != null)
                    {
                        // Sync task status from Kobold's current state
                        await SyncTaskFromKoboldAsync(kobold);
                    }
                    else
                    {
                        // Kobold was removed but task still mapped - clean up
                        _taskToKoboldMap.Remove(task.Id);
                    }
                }
            }

            SaveTasksToFile();

            // Update feature status if Wyvern is available
            UpdateFeatureStatus();
        }

        /// <summary>
        /// Monitors all tasks and syncs their status from associated Kobolds.
        /// Drake observes Kobold states but does not force state changes.
        /// Also updates feature status through Wyvern if available.
        /// Note: Prefer MonitorTasksAsync() for non-blocking operation.
        /// </summary>
        public void MonitorTasks()
        {
            MonitorTasksAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Updates feature status based on current task statuses
        /// </summary>
        private void UpdateFeatureStatus()
        {
            if (_wyvern == null)
                return;

            var allTasks = _taskTracker.GetAllTasks();
            var taskStatuses = allTasks.ToDictionary(t => t.Id, t => t.Status);

            _wyvern.UpdateFeatureStatus(taskStatuses);
        }

        /// <summary>
        /// Unsummons all completed Kobolds (Done or Failed)
        /// </summary>
        /// <returns>Number of Kobolds unsummoned</returns>
        public int UnsummonCompletedKobolds()
        {
            var completedKobolds = _koboldFactory.GetKoboldsByStatus(KoboldStatus.Done)
                .Concat(_koboldFactory.GetKoboldsByStatus(KoboldStatus.Failed))
                .ToList();
            int count = 0;

            foreach (var kobold in completedKobolds)
            {
                if (UnsummonKobold(kobold.Id))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Detects and handles Kobolds that have been working longer than the specified timeout (async version).
        /// Stuck Kobolds are marked as failed and their tasks are updated accordingly.
        /// </summary>
        /// <param name="timeout">Maximum allowed working duration before a Kobold is considered stuck</param>
        /// <returns>List of stuck Kobold info (ID, task ID, working duration)</returns>
        public async Task<List<(Guid KoboldId, string? TaskId, TimeSpan WorkingDuration)>> HandleStuckKoboldsAsync(TimeSpan timeout)
        {
            var stuckKobolds = _koboldFactory.GetStuckKobolds(timeout);
            var result = new List<(Guid KoboldId, string? TaskId, TimeSpan WorkingDuration)>();

            foreach (var (kobold, workingDuration) in stuckKobolds)
            {
                var taskPreview = kobold.TaskDescription?.Length > 60 
                    ? kobold.TaskDescription.Substring(0, 60) + "..." 
                    : kobold.TaskDescription ?? "unknown";
                var projectInfo = kobold.ProjectId ?? _projectId ?? "unknown";
                
                _logger?.LogWarning(
                    "⚠️ Stuck Kobold detected\n" +
                    "  Project: {ProjectId}\n" +
                    "  Kobold ID: {KoboldId}\n" +
                    "  Task ID: {TaskId}\n" +
                    "  Duration: {Duration:F1} minutes\n" +
                    "  Task: {TaskDescription}",
                    projectInfo,
                    kobold.Id.ToString()[..8],
                    kobold.TaskId?.ToString()[..8] ?? "unknown",
                    workingDuration.TotalMinutes,
                    taskPreview);

                // Mark the Kobold as stuck (transitions to Done with error)
                kobold.MarkAsStuck(workingDuration, timeout);

                // Sync the task status from the now-failed Kobold
                await SyncTaskFromKoboldAsync(kobold);

                result.Add((kobold.Id, kobold.TaskId?.ToString(), workingDuration));
            }

            if (result.Count > 0)
            {
                SaveTasksToFile();
            }

            return result;
        }

        /// <summary>
        /// Detects and handles Kobolds that have been working longer than the specified timeout.
        /// Stuck Kobolds are marked as failed and their tasks are updated accordingly.
        /// Note: Prefer HandleStuckKoboldsAsync() for non-blocking operation.
        /// </summary>
        /// <param name="timeout">Maximum allowed working duration before a Kobold is considered stuck</param>
        /// <returns>List of stuck Kobold info (ID, task ID, working duration)</returns>
        public List<(Guid KoboldId, string? TaskId, TimeSpan WorkingDuration)> HandleStuckKobolds(TimeSpan timeout)
        {
            return HandleStuckKoboldsAsync(timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets working Kobolds that have exceeded the specified timeout
        /// </summary>
        /// <param name="timeout">Maximum allowed working duration</param>
        /// <returns>List of stuck Kobolds with their working duration</returns>
        public IReadOnlyCollection<(Kobold Kobold, TimeSpan WorkingDuration)> GetStuckKobolds(TimeSpan timeout)
        {
            return _koboldFactory.GetStuckKobolds(timeout)
                .Select(x => (x.Kobold, x.WorkingDuration))
                .ToList();
        }

        /// <summary>
        /// Gets statistics about the Drake's managed Kobolds
        /// </summary>
        public DrakeStatistics GetStatistics()
        {
            var koboldStats = _koboldFactory.GetStatistics();
            var allTasks = _taskTracker.GetAllTasks().ToList();

            return new DrakeStatistics
            {
                TotalKobolds = koboldStats.Total,
                UnassignedKobolds = koboldStats.Unassigned,
                AssignedKobolds = koboldStats.Assigned,
                WorkingKobolds = koboldStats.Working,
                DoneKobolds = koboldStats.Done,
                FailedKobolds = koboldStats.Failed,
                TotalTasks = allTasks.Count,
                UnassignedTasks = allTasks.Count(t => t.Status == TaskStatus.Unassigned),
                WorkingTasks = allTasks.Count(t => t.Status == TaskStatus.Working),
                DoneTasks = allTasks.Count(t => t.Status == TaskStatus.Done),
                FailedTasks = allTasks.Count(t => t.Status == TaskStatus.Failed),
                BlockedTasks = allTasks.Count(t => t.Status == TaskStatus.BlockedByFailure),
                ActiveAssignments = _taskToKoboldMap.Count
            };
        }

        /// <summary>
        /// Gets the Kobold assigned to a specific task
        /// </summary>
        public Kobold? GetKoboldForTask(string taskId)
        {
            if (_taskToKoboldMap.TryGetValue(taskId, out var koboldId))
            {
                return _koboldFactory.GetKobold(koboldId);
            }
            return null;
        }

        /// <summary>
        /// Updates a task's status. Public wrapper for external task management.
        /// </summary>
        /// <param name="task">Task to update</param>
        /// <param name="newStatus">New status for the task</param>
        public void UpdateTask(TaskRecord task, TaskStatus newStatus)
        {
            _taskTracker.UpdateTask(task, newStatus);
            SaveTasksToFile();
        }

        /// <summary>
        /// Queues a debounced save of the current task state.
        /// Writes are coalesced - multiple rapid calls result in a single write after the debounce interval.
        /// </summary>
        private void SaveTasksToFile()
        {
            // Queue a save request (non-blocking, drops if already queued)
            _saveChannel.Writer.TryWrite(true);
        }

        /// <summary>
        /// Processes the save queue, debouncing rapid writes
        /// </summary>
        private async Task ProcessSaveQueueAsync()
        {
            try
            {
                while (await _saveChannel.Reader.WaitToReadAsync())
                {
                    // Drain any pending requests (coalesce)
                    while (_saveChannel.Reader.TryRead(out _)) { }

                    // Wait for debounce interval to allow more writes to coalesce
                    await Task.Delay(_debounceInterval);

                    // Drain again in case more came in during the delay
                    while (_saveChannel.Reader.TryRead(out _)) { }

                    // Perform the actual save
                    try
                    {
                        await _taskTracker.SaveToFileAsync(_outputMarkdownPath, "Drake Task Report");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to save task file: {Path}", _outputMarkdownPath);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Channel was closed, exit gracefully
            }
        }

        /// <summary>
        /// Forces an immediate save of the current task state (async)
        /// </summary>
        public async Task SaveTasksToFileAsync()
        {
            await _taskTracker.SaveToFileAsync(_outputMarkdownPath, "Drake Task Report");
        }

        /// <summary>
        /// Forces a save of the current task state (queues debounced write)
        /// </summary>
        public void UpdateTasksFile()
        {
            SaveTasksToFile();
        }

        /// <summary>
        /// Forces an immediate save of the current task state (async, bypasses debounce)
        /// </summary>
        public async Task UpdateTasksFileAsync()
        {
            await SaveTasksToFileAsync();
        }

        /// <summary>
        /// Reloads tasks from the markdown file, refreshing in-memory state.
        /// Preserves active Kobold mappings but updates task statuses from file.
        /// Should be called before processing tasks to ensure state is current.
        /// </summary>
        /// <returns>Number of tasks loaded from file</returns>
        public int ReloadTasksFromFile()
        {
            _logger?.LogDebug("🔄 Reloading tasks from {FilePath}", _outputMarkdownPath);

            // Clear and reload from file
            _taskTracker.Clear();
            var tasksLoaded = _taskTracker.LoadFromFile(_outputMarkdownPath);

            _logger?.LogDebug("📂 Reloaded {Count} task(s) from file", tasksLoaded);

            return tasksLoaded;
        }

        /// <summary>
        /// Reloads tasks from the markdown file asynchronously, refreshing in-memory state.
        /// Preserves active Kobold mappings but updates task statuses from file.
        /// Detects and recovers orphaned tasks (NotInitialized/Working with no active Kobold).
        /// Should be called before processing tasks to ensure state is current.
        /// </summary>
        /// <returns>Number of tasks loaded from file</returns>
        public async Task<int> ReloadTasksFromFileAsync()
        {
            _logger?.LogDebug("🔄 Reloading tasks from {FilePath}", _outputMarkdownPath);

            // Clear and reload from file
            _taskTracker.Clear();
            var tasksLoaded = await _taskTracker.LoadFromFileAsync(_outputMarkdownPath);

            _logger?.LogDebug("📂 Reloaded {Count} task(s) from file", tasksLoaded);

            // Recover orphaned tasks (NotInitialized or Working with no active Kobold)
            // Tasks with completed plans are marked Done; others are reset to Unassigned
            var orphansRecovered = await RecoverOrphanedTasksAsync();
            if (orphansRecovered > 0)
            {
                _logger?.LogInformation("🔧 Recovered {Count} orphaned task(s)", orphansRecovered);
                await SaveTasksToFileAsync();
            }

            return tasksLoaded;
        }

        /// <summary>
        /// Detects and recovers orphaned tasks.
        /// A task is orphaned if it has status NotInitialized or Working but no active Kobold is working on it.
        /// Orphaned tasks with completed plans are marked Done; others are reset to Unassigned.
        /// </summary>
        /// <returns>Number of tasks recovered</returns>
        public async Task<int> RecoverOrphanedTasksAsync()
        {
            var recovered = 0;
            var allTasks = _taskTracker.GetAllTasks();

            // Get IDs of tasks that have active Kobolds
            var activeTaskIds = _taskToKoboldMap.Keys.ToHashSet();

            foreach (var task in allTasks)
            {
                // Check if task is in a "working" state but has no active Kobold
                if ((task.Status == TaskStatus.NotInitialized || task.Status == TaskStatus.Working)
                    && !activeTaskIds.Contains(task.Id))
                {
                    // Also verify the Kobold doesn't exist in the factory
                    if (_taskToKoboldMap.TryGetValue(task.Id, out var koboldId))
                    {
                        var kobold = _koboldFactory.GetKobold(koboldId);
                        if (kobold != null)
                        {
                            // Kobold exists, not orphaned
                            continue;
                        }
                        // Kobold mapping exists but Kobold is gone - clean up mapping
                        _taskToKoboldMap.Remove(task.Id);
                    }

                    // Check if the task has a completed plan - if so, mark as Done
                    var newStatus = TaskStatus.Unassigned;
                    _logger?.LogDebug("🔍 Checking plan for orphaned task {TaskId}, planService={HasPlanService}, projectId={ProjectId}",
                        task.Id[..Math.Min(8, task.Id.Length)], _planService != null, _projectId ?? "(null)");

                    if (_planService != null && !string.IsNullOrEmpty(_projectId))
                    {
                        try
                        {
                            var plan = await _planService.LoadPlanAsync(_projectId, task.Id);
                            _logger?.LogDebug("🔍 Plan lookup result for task {TaskId}: found={Found}, status={Status}",
                                task.Id[..Math.Min(8, task.Id.Length)], plan != null, plan?.Status.ToString() ?? "(null)");

                            if (plan != null && plan.Status == PlanStatus.Completed)
                            {
                                newStatus = TaskStatus.Done;
                                _logger?.LogInformation("✅ Orphaned task {TaskId} has completed plan - marking as Done",
                                    task.Id[..Math.Min(8, task.Id.Length)]);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Could not load plan for task {TaskId}", task.Id);
                        }
                    }
                    else
                    {
                        _logger?.LogDebug("🔍 Cannot check plan for task {TaskId}: planService={HasPlanService}, projectId={ProjectId}",
                            task.Id[..Math.Min(8, task.Id.Length)], _planService != null, _projectId ?? "(null)");
                    }

                    if (newStatus == TaskStatus.Unassigned)
                    {
                        _logger?.LogDebug("🔧 Recovering orphaned task {TaskId} (was {Status}) - resetting to Unassigned",
                            task.Id[..Math.Min(8, task.Id.Length)], task.Status);
                    }

                    _taskTracker.UpdateTask(task, newStatus, task.AssignedAgent);
                    recovered++;
                }
            }

            return recovered;
        }

        /// <summary>
        /// Gets all unassigned tasks that are ready for execution.
        /// Returns tasks with their recommended agent type parsed from the task description.
        /// </summary>
        /// <returns>List of (TaskRecord, AgentType) tuples for tasks ready to execute</returns>
        /// <summary>
        /// Gets unassigned tasks that are ready to execute (dependencies met).
        /// Also detects and marks tasks blocked by failed dependencies.
        /// </summary>
        public List<(TaskRecord Task, string AgentType)> GetUnassignedTasks()
        {
            var result = new List<(TaskRecord, string)>();
            var allTasks = _taskTracker.GetAllTasks();
            var doneTasks = allTasks.Where(t => t.Status == TaskStatus.Done).Select(t => t.Task).ToHashSet();

            // Load done tasks from ALL task files in the project directory to check cross-area dependencies
            var projectDoneTasks = LoadDoneTasksFromProject();
            doneTasks.UnionWith(projectDoneTasks);

            // Load failed tasks from ALL task files to detect blocked dependencies
            var failedTasks = LoadFailedTasksFromProject();
            failedTasks.UnionWith(allTasks.Where(t => t.Status == TaskStatus.Failed).Select(t => t.Task));

            foreach (var task in allTasks.Where(t => t.Status == TaskStatus.Unassigned || t.Status == TaskStatus.BlockedByFailure))
            {
                // Check if dependencies are met (parse from task description)
                // Format: [task-id] Task name (depends on: dep1, dep2)
                var dependenciesMet = true;
                var unmeetDependencies = new List<string>();
                var failedDependencies = new List<string>();
                var dependsOnMatch = System.Text.RegularExpressions.Regex.Match(
                    task.Task, @"\(depends on:\s*([^)]+)\)");

                if (dependsOnMatch.Success)
                {
                    var dependencies = dependsOnMatch.Groups[1].Value
                        .Split(',')
                        .Select(d => d.Trim())
                        .Where(d => !string.IsNullOrEmpty(d));

                    foreach (var dep in dependencies)
                    {
                        // Check if dependency is done
                        var depMet = doneTasks.Any(doneTask =>
                            doneTask.Contains($"[{dep}]", StringComparison.OrdinalIgnoreCase));

                        if (depMet)
                        {
                            continue;
                        }

                        // Check if dependency has failed
                        var depFailed = failedTasks.Any(failedTask =>
                            failedTask.Contains($"[{dep}]", StringComparison.OrdinalIgnoreCase));

                        if (depFailed)
                        {
                            failedDependencies.Add(dep);
                        }
                        else
                        {
                            unmeetDependencies.Add(dep);
                        }

                        dependenciesMet = false;
                    }
                }

                // Mark as blocked if any dependencies have failed
                if (failedDependencies.Count > 0)
                {
                    if (task.Status != TaskStatus.BlockedByFailure)
                    {
                        _logger?.LogWarning(
                            "🟠 Task {TaskId} blocked by failed dependencies: {FailedDeps}",
                            task.Id.ToString()[..Math.Min(8, task.Id.Length)],
                            string.Join(", ", failedDependencies)
                        );
                        _taskTracker.UpdateTask(task, TaskStatus.BlockedByFailure);
                        SaveTasksToFile();
                    }
                    continue;
                }

                // If previously blocked but dependencies are now resolved, unblock it
                if (task.Status == TaskStatus.BlockedByFailure && dependenciesMet)
                {
                    _logger?.LogInformation(
                        "✅ Task {TaskId} unblocked - failed dependencies have been resolved",
                        task.Id.ToString()[..Math.Min(8, task.Id.Length)]
                    );
                    _taskTracker.UpdateTask(task, TaskStatus.Unassigned);
                    SaveTasksToFile();
                }

                if (!dependenciesMet)
                {
                    _logger?.LogDebug(
                        "Task {TaskId} has unmet dependencies: {Dependencies}",
                        task.Id.ToString()[..Math.Min(8, task.Id.Length)],
                        string.Join(", ", unmeetDependencies)
                    );
                    continue;
                }

                // Get agent type from AssignedAgent field (set during task creation)
                var agentType = !string.IsNullOrEmpty(task.AssignedAgent)
                    ? task.AssignedAgent
                    : "coding"; // Default fallback

                result.Add((task, agentType));
            }

            return result;
        }

        /// <summary>
        /// Loads done tasks from all task files in the project directory.
        /// This enables cross-area dependency checking.
        /// </summary>
        private HashSet<string> LoadDoneTasksFromProject()
        {
            var doneTasks = new HashSet<string>();

            // Get project directory from task file path: ./projects/my-project/tasks/backend-tasks.md -> ./projects/my-project
            var taskFolder = Path.GetDirectoryName(_outputMarkdownPath);
            var projectDir = !string.IsNullOrEmpty(taskFolder) ? Path.GetDirectoryName(taskFolder) : null;
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
            {
                return doneTasks;
            }

            // Find all *-tasks.md files in the project task directory
            var taskDir = Path.Combine(projectDir, "tasks");
            if (!Directory.Exists(taskDir))
            {
                return doneTasks;
            }

            var taskFiles = Directory.GetFiles(taskDir, "*-tasks.md");

            foreach (var taskFile in taskFiles)
            {
                // Skip the current task file (already loaded)
                if (Path.GetFullPath(taskFile) == Path.GetFullPath(_outputMarkdownPath))
                {
                    continue;
                }

                try
                {
                    var tracker = new TaskTracker();
                    tracker.LoadFromFile(taskFile);
                    var tasks = tracker.GetAllTasks()
                        .Where(t => t.Status == TaskStatus.Done)
                        .Select(t => t.Task);

                    foreach (var task in tasks)
                    {
                        doneTasks.Add(task);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load tasks from {TaskFile} for dependency checking", taskFile);
                }
            }

            return doneTasks;
        }

        /// <summary>
        /// Loads failed tasks from all task files in the project directory.
        /// This enables detection of cross-area blocked dependencies.
        /// </summary>
        private HashSet<string> LoadFailedTasksFromProject()
        {
            var failedTasks = new HashSet<string>();

            // Get project directory from task file path: ./projects/my-project/tasks/backend-tasks.md -> ./projects/my-project
            var taskFolder = Path.GetDirectoryName(_outputMarkdownPath);
            var projectDir = !string.IsNullOrEmpty(taskFolder) ? Path.GetDirectoryName(taskFolder) : null;
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
            {
                return failedTasks;
            }

            // Find all *-tasks.md files in the project task directory
            var taskDir = Path.Combine(projectDir, "tasks");
            if (!Directory.Exists(taskDir))
            {
                return failedTasks;
            }

            var taskFiles = Directory.GetFiles(taskDir, "*-tasks.md");

            foreach (var taskFile in taskFiles)
            {
                // Skip the current task file (already loaded)
                if (Path.GetFullPath(taskFile) == Path.GetFullPath(_outputMarkdownPath))
                {
                    continue;
                }

                try
                {
                    var tracker = new TaskTracker();
                    tracker.LoadFromFile(taskFile);
                    var tasks = tracker.GetAllTasks()
                        .Where(t => t.Status == TaskStatus.Failed)
                        .Select(t => t.Task);

                    foreach (var task in tasks)
                    {
                        failedTasks.Add(task);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load failed tasks from {TaskFile} for dependency checking", taskFile);
                }
            }

            return failedTasks;
        }

        /// <summary>
        /// Gets all tasks from the tracker
        /// </summary>
        public List<TaskRecord> GetAllTasks()
        {
            return _taskTracker.GetAllTasks();
        }
    }
}
