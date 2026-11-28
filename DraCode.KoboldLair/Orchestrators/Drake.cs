using Birko.AI.Orchestration.Models;
using System.Threading.Channels;
using Birko.AI.Models;
using Birko.EventBus;
using Birko.AI;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Events;
using DraCode.KoboldLair.Factories;
using Birko.AI.Resilience.Services;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;
using Microsoft.Extensions.Logging;

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
        private readonly IProjectRepository? _projectRepository;
        private readonly GitService? _gitService;
        private readonly KoboldPlanService? _planService;
        private readonly KoboldPlannerAgent? _plannerAgent;
        private readonly ProviderCircuitBreaker? _circuitBreaker;
        private readonly SharedPlanningContextService? _sharedPlanningContext;
        private readonly bool _planningEnabled;
        private readonly ProjectImplementationService? _implementationService;
        private readonly bool _useEnhancedExecution;
        private readonly bool _allowPlanModifications;
        private readonly bool _autoApproveModifications;
        private readonly bool _filterFilesByPlan;
        private readonly PlanFileFilterService _fileFilterService;
        private readonly Models.Configuration.PlanningConfiguration? _planningConfig;
        private readonly Action<string, string, string>? _onFeatureBranchReady;

        /// <summary>The acceptance check's settings; null when it is switched off (TASK-025, FEATURE-031 D5).</summary>
        private readonly Models.Configuration.VerificationConfiguration? _acceptance;
        private readonly TaskAcceptanceService _acceptanceService = new();
        private readonly Action<string, EscalationAlert, string>? _onEscalationNotify;
        private readonly IEventBus? _eventBus;
        private readonly SqlPendingDecisionRepository? _pendingDecisions;
        private readonly IEscalationDispositionPolicy _dispositionPolicy;
        private readonly IAskHumanPolicy _askHumanPolicy;
        private readonly Action<string, PendingDecision>? _onDecisionRequired;
        private readonly Action<string, PendingDecision>? _onDecisionResolved;

        // The cancellation of each task's current run: parking the task cancels it (TASK-020)
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CancellationTokenSource> _runCancellations = new();

        // Parking checks then writes; two escalations for one task (a Kobold's reflect and the monitor) must not both park it
        private readonly SemaphoreSlim _parkLock = new(1, 1);
        private Wyvern? _wyvern;
        private WyrmRecommendation? _wyrmRecommendation;

        // Debounced file write support
        private readonly Channel<bool> _saveChannel;
        private readonly Task _saveTask;
        private readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(2);
        private readonly TaskStateWal _wal;

        /// <summary>
        /// Maps TaskId to KoboldId for tracking active assignments
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Guid> _taskToKoboldMap;

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
        /// <param name="projectRepository">Optional project repository for accessing project-level timeout configuration</param>
        /// <param name="gitService">Optional git service for committing changes on task completion</param>
        /// <param name="planService">Optional plan service for implementation plan persistence</param>
        /// <param name="plannerAgent">Optional planner agent for creating implementation plans</param>
        /// <param name="circuitBreaker">Optional circuit breaker for provider failure tracking</param>
        /// <param name="planningEnabled">Whether to enable implementation planning (default: true if planService and plannerAgent are provided)</param>
        /// <param name="useEnhancedExecution">Whether to use Phase 2 enhanced execution with auto-detection (default: true)</param>
        /// <param name="allowPlanModifications">Whether to allow agent-suggested plan modifications (default: false)</param>
        /// <param name="autoApproveModifications">Whether to auto-approve plan modifications (default: false)</param>
        /// <param name="filterFilesByPlan">Whether to filter file structure by plan requirements (default: true)</param>
        /// <param name="sharedPlanningContext">Optional shared planning context service for cross-agent coordination</param>
        /// <param name="planningConfig">Optional planning configuration for parallel execution settings</param>
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
            IProjectRepository? projectRepository = null,
            GitService? gitService = null,
            KoboldPlanService? planService = null,
            KoboldPlannerAgent? plannerAgent = null,
            ProviderCircuitBreaker? circuitBreaker = null,
            bool? planningEnabled = null,
            bool useEnhancedExecution = true,
            bool allowPlanModifications = false,
            bool autoApproveModifications = false,
            bool filterFilesByPlan = true,
            SharedPlanningContextService? sharedPlanningContext = null,
            ProjectImplementationService? implementationService = null,
            Models.Configuration.PlanningConfiguration? planningConfig = null,
            Action<string, string, string>? onFeatureBranchReady = null,
            Action<string, EscalationAlert, string>? onEscalationNotify = null,
            IEventBus? eventBus = null,
            SqlPendingDecisionRepository? pendingDecisions = null,
            IEscalationDispositionPolicy? dispositionPolicy = null,
            IAskHumanPolicy? askHumanPolicy = null,
            Action<string, PendingDecision>? onDecisionRequired = null,
            Action<string, PendingDecision>? onDecisionResolved = null,
            Models.Configuration.VerificationConfiguration? acceptance = null)
        {
            _acceptance = acceptance is { TaskAcceptanceEnabled: true } ? acceptance : null;
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
            _projectRepository = projectRepository;
            _gitService = gitService;
            _planService = planService;
            _plannerAgent = plannerAgent;
            _circuitBreaker = circuitBreaker;
            _sharedPlanningContext = sharedPlanningContext;
            _implementationService = implementationService;
            _planningConfig = planningConfig;
            _onFeatureBranchReady = onFeatureBranchReady;
            _onEscalationNotify = onEscalationNotify;
            _eventBus = eventBus;
            _pendingDecisions = pendingDecisions;
            _dispositionPolicy = dispositionPolicy ?? AutoResolveEscalationPolicy.Instance;
            _askHumanPolicy = askHumanPolicy ?? UnrestrictedAskHumanPolicy.Instance;
            _onDecisionRequired = onDecisionRequired;
            _onDecisionResolved = onDecisionResolved;
            _planningEnabled = planningEnabled ?? (planService != null && plannerAgent != null);
            _useEnhancedExecution = useEnhancedExecution && _planningEnabled; // Only use enhanced if planning is enabled
            _allowPlanModifications = allowPlanModifications;
            _autoApproveModifications = autoApproveModifications;
            _filterFilesByPlan = filterFilesByPlan && _planningEnabled; // Only filter if planning is enabled
            _fileFilterService = new PlanFileFilterService(null); // Logger is optional for filter service
            _taskToKoboldMap = new System.Collections.Concurrent.ConcurrentDictionary<string, Guid>();

            // Initialize WAL for crash-safe state persistence
            _wal = new TaskStateWal(_outputMarkdownPath, _logger);

            // Check for uncommitted WAL entries and recover if needed
            if (_wal.HasUncommittedChanges())
            {
                _logger?.LogWarning("Found uncommitted WAL entries, recovering task state...");
                try
                {
                    RecoverFromWalAsync().GetAwaiter().GetResult();
                    _logger?.LogInformation("WAL recovery completed successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "WAL recovery failed - task state may be inconsistent");
                }
            }

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
        /// Gets the LLM provider the planner agent calls (null when planning is off)
        /// </summary>
        public Birko.AI.Providers.ILlmProvider? PlannerProvider => _plannerAgent?.Provider;

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
            // Also load Wyrm recommendations when Wyvern is set, before it returns: a Kobold summoned next reads them
            LoadWyrmRecommendations();
        }

        /// <summary>
        /// Loads Wyrm recommendations from the project folder (GAP 5 FIX)
        /// </summary>
        private void LoadWyrmRecommendations()
        {
            if (string.IsNullOrEmpty(_specificationPath))
                return;

            try
            {
                // Derive project folder from specification path: ./projects/my-project/specification.md -> ./projects/my-project
                var projectFolder = Path.GetDirectoryName(_specificationPath);
                if (string.IsNullOrEmpty(projectFolder))
                    return;

                var wyrmPath = Path.Combine(projectFolder, "wyrm-recommendation.json");
                if (!File.Exists(wyrmPath))
                {
                    _logger?.LogDebug("No Wyrm recommendations found at {Path}", wyrmPath);
                    return;
                }

                var json = File.ReadAllText(wyrmPath);
                _wyrmRecommendation = System.Text.Json.JsonSerializer.Deserialize<WyrmRecommendation>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger?.LogDebug("Loaded Wyrm recommendations for project: Complexity={Complexity}, Languages={Languages}",
                    _wyrmRecommendation?.Complexity ?? "unknown",
                    string.Join(", ", _wyrmRecommendation?.RecommendedLanguages ?? new List<string>()));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load Wyrm recommendations");
            }
        }

        /// <summary>
        /// Updates task status with WAL logging for crash safety
        /// </summary>
        private async Task UpdateTaskWithWalAsync(TaskRecord task, TaskStatus newStatus, string? assignedAgent = null, string? errorMessage = null)
        {
            var previousStatus = task.Status.ToString();
            
            // Log to WAL before updating in-memory state
            await _wal.AppendAsync(new WalEntry(
                DateTime.UtcNow,
                task.Id,
                previousStatus,
                newStatus.ToString(),
                assignedAgent,
                errorMessage));

            // Update in-memory state
            _taskTracker.UpdateTask(task, newStatus, assignedAgent);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                _taskTracker.SetError(task, errorMessage);
            }
            else if (newStatus == TaskStatus.Done)
            {
                // Clear any previous error state when task completes successfully
                // This handles cases where a task had transient errors but eventually succeeded
                _taskTracker.ClearError(task);
            }
        }

        /// <summary>
        /// Summons (creates) a Kobold for a specific task
        /// </summary>
        /// <param name="task">Task to assign</param>
        /// <param name="agentType">Type of agent to create</param>
        /// <param name="provider">LLM provider (optional, resolves by agent type if not specified)</param>
        /// <returns>The summoned Kobold, or null if resource limit reached</returns>
        public async Task<Kobold?> SummonKoboldAsync(TaskRecord task, string agentType, string? provider = null)
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

            // An answer the person overturned while no Drake held the task: its plan is revised before the run (TASK-208)
            await ReviseOverturnedPlansAsync(task);

            // Read before a Kobold exists: an unreadable answer holds the task, like the limit above
            var humanAnswers = await LoadHumanAnswersAsync(task.Id);
            if (humanAnswers == null)
                return null;

            // Create options with workspace path and external paths
            var effectiveOptions = _defaultOptions?.Clone() ?? new AgentOptions();

            // Set working directory: existing projects use source path, new projects use workspace subfolder
            var resolvedWorkspace = ResolveWorkspacePath();
            if (!string.IsNullOrEmpty(resolvedWorkspace))
            {
                effectiveOptions.WorkingDirectory = resolvedWorkspace;
                _logger?.LogDebug("Kobold workspace set to {WorkspacePath}", resolvedWorkspace);
            }

            // Add external paths if available
            if (!string.IsNullOrEmpty(projectId))
            {
                var externalPaths = _projectRepository?.GetAllowedExternalPaths(projectId);
                if (externalPaths != null && externalPaths.Count > 0)
                {
                    effectiveOptions.AllowedExternalPaths = externalPaths.ToList();
                    _logger?.LogDebug("Kobold for project {ProjectId} has {Count} allowed external paths",
                        projectId, externalPaths.Count);
                }
            }

            // Resolve the provider *type* + its config (decrypted key, model, baseUrl) — NOT the provider
            // name: a provider's name (e.g. "pi-zai") is a user-facing label and can differ from its factory
            // type (e.g. "zai"); the LLM factory is keyed by type. Falls back to the resolved name + default
            // config when no provider-config service is wired (legacy/tests).
            var providerForFactory = effectiveProvider;
            var configForFactory = _defaultConfig;
            if (_providerConfigService != null)
            {
                try
                {
                    var (resolvedType, resolvedConfig, _) = provider != null
                        ? _providerConfigService.GetProviderSettingsByName(provider, resolvedWorkspace)
                        : _providerConfigService.GetProviderSettingsForKoboldAgentType(agentType, resolvedWorkspace);
                    providerForFactory = resolvedType;
                    configForFactory = resolvedConfig;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex,
                        "Provider settings resolution failed for agent type {AgentType}; falling back to '{Provider}'",
                        agentType, effectiveProvider);
                }
            }

            // Create the Kobold
            var kobold = _koboldFactory.CreateKobold(
                providerForFactory,
                agentType,
                effectiveOptions,
                configForFactory,
                projectId
            );

            // Set shared planning context for workspace awareness
            if (_sharedPlanningContext != null)
            {
                kobold.SetSharedPlanningContext(_sharedPlanningContext);
            }

            // Register with shared planning context if available
            if (!string.IsNullOrEmpty(projectId) && _sharedPlanningContext != null)
            {
                _ = _sharedPlanningContext.RegisterAgentAsync(kobold.Id.ToString(), projectId, task.Id.ToString(), agentType);
            }

            // Publish Kobold lifecycle started event
            if (_eventBus != null)
            {
                try
                {
                    _ = _eventBus.PublishAsync(new KoboldLifecycleEvent
                    {
                        ProjectId = projectId ?? "unknown",
                        TaskId = task.Id,
                        AgentType = agentType,
                        KoboldId = kobold.Id,
                        Action = KoboldLifecycleAction.Started
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to publish KoboldLifecycleEvent.Started for kobold {KoboldId}", kobold.Id);
                }
            }

            // Load specification context if available
            string? specificationContext = null;
            int specificationVersion = 1;
            if (!string.IsNullOrEmpty(_specificationPath) && File.Exists(_specificationPath))
            {
                try
                {
                    specificationContext = File.ReadAllTextAsync(_specificationPath).GetAwaiter().GetResult();

                    // Read specification version from features file (wrapped format)
                    var featuresPath = Path.Combine(Path.GetDirectoryName(_specificationPath) ?? "", "specification.features.json");
                    if (File.Exists(featuresPath))
                    {
                        try
                        {
                            var featuresJson = File.ReadAllTextAsync(featuresPath).GetAwaiter().GetResult();
                            using var doc = System.Text.Json.JsonDocument.Parse(featuresJson);
                            // New format has "specificationVersion" property at root
                            if (doc.RootElement.TryGetProperty("specificationVersion", out var versionProp))
                            {
                                specificationVersion = versionProp.GetInt32();
                            }
                        }
                        catch { }
                    }

                    // CONTEXT SLIPPING FIX: Prepend Wyrm recommendations at the TOP (not append at bottom)
                    // This ensures architectural guidance is seen first, before the full specification
                    if (_wyrmRecommendation != null)
                    {
                        var wyrmInfo = new System.Text.StringBuilder();
                        wyrmInfo.AppendLine("# 🐉 WYRM ARCHITECTURAL GUIDANCE");
                        wyrmInfo.AppendLine();
                        wyrmInfo.AppendLine("**IMPORTANT**: Read this section FIRST before the full specification below.");
                        wyrmInfo.AppendLine("These recommendations were generated by pre-analyzing your project requirements.");
                        wyrmInfo.AppendLine();

                        if (!string.IsNullOrEmpty(_wyrmRecommendation.AnalysisSummary))
                        {
                            wyrmInfo.AppendLine("## Project Summary");
                            wyrmInfo.AppendLine();
                            wyrmInfo.AppendLine(_wyrmRecommendation.AnalysisSummary);
                            wyrmInfo.AppendLine();
                        }

                        wyrmInfo.AppendLine("## Technical Approach");
                        wyrmInfo.AppendLine();
                        wyrmInfo.AppendLine($"**Complexity Level**: {_wyrmRecommendation.Complexity}");
                        wyrmInfo.AppendLine();

                        if (_wyrmRecommendation.RecommendedLanguages.Any())
                        {
                            wyrmInfo.AppendLine($"**Primary Languages**: {string.Join(", ", _wyrmRecommendation.RecommendedLanguages)}");
                            wyrmInfo.AppendLine();
                        }

                        if (_wyrmRecommendation.TechnicalStack.Any())
                        {
                            wyrmInfo.AppendLine("**Recommended Technology Stack**:");
                            foreach (var tech in _wyrmRecommendation.TechnicalStack)
                            {
                                wyrmInfo.AppendLine($"  - {tech}");
                            }
                            wyrmInfo.AppendLine();
                        }

                        if (_wyrmRecommendation.RecommendedAgentTypes.Any())
                        {
                            wyrmInfo.AppendLine("**Recommended Agent Types**:");
                            foreach (var recommendedType in _wyrmRecommendation.RecommendedAgentTypes)
                            {
                                wyrmInfo.AppendLine($"  - {recommendedType}");
                            }
                            wyrmInfo.AppendLine();
                        }

                        // Collect constraints from both Wyrm and Wyvern sources
                        var allConstraints = new List<string>();
                        var allOutOfScope = new List<string>();

                        if (_wyrmRecommendation.Constraints.Any())
                            allConstraints.AddRange(_wyrmRecommendation.Constraints);
                        if (_wyrmRecommendation.OutOfScope.Any())
                            allOutOfScope.AddRange(_wyrmRecommendation.OutOfScope);

                        if (_wyvern?.Analysis?.Constraints?.Any() == true)
                            allConstraints.AddRange(_wyvern.Analysis.Constraints.Where(c => !allConstraints.Contains(c)));
                        if (_wyvern?.Analysis?.OutOfScope?.Any() == true)
                            allOutOfScope.AddRange(_wyvern.Analysis.OutOfScope.Where(o => !allOutOfScope.Contains(o)));

                        if (allConstraints.Any() || allOutOfScope.Any())
                        {
                            wyrmInfo.AppendLine("## ⛔ PROJECT CONSTRAINTS (MUST NOT VIOLATE)");
                            wyrmInfo.AppendLine();
                            if (allConstraints.Any())
                            {
                                foreach (var constraint in allConstraints)
                                {
                                    wyrmInfo.AppendLine($"- **{constraint}**");
                                }
                                wyrmInfo.AppendLine();
                            }
                            if (allOutOfScope.Any())
                            {
                                wyrmInfo.AppendLine("**OUT OF SCOPE (do NOT implement):**");
                                foreach (var item in allOutOfScope)
                                {
                                    wyrmInfo.AppendLine($"- {item}");
                                }
                                wyrmInfo.AppendLine();
                            }
                        }

                        wyrmInfo.AppendLine("---");
                        wyrmInfo.AppendLine();
                        wyrmInfo.AppendLine("# Full Project Specification");
                        wyrmInfo.AppendLine();

                        // Prepend Wyrm info to specification (not append)
                        specificationContext = wyrmInfo.ToString() + specificationContext;
                    }

                    // GAP 3 FIX: Append full Wyvern analysis context (not just structure)
                    if (_wyvern?.Analysis != null)
                    {
                        var analysis = _wyvern.Analysis;
                        var analysisInfo = new System.Text.StringBuilder();

                        analysisInfo.AppendLine();
                        analysisInfo.AppendLine("---");
                        analysisInfo.AppendLine();
                        analysisInfo.AppendLine("## Wyvern Analysis Context");
                        analysisInfo.AppendLine();

                        analysisInfo.AppendLine($"**Project Name**: {analysis.ProjectName}");
                        analysisInfo.AppendLine($"**Total Tasks**: {analysis.TotalTasks}");
                        analysisInfo.AppendLine($"**Estimated Complexity**: {analysis.EstimatedComplexity}");
                        analysisInfo.AppendLine();

                        if (analysis.Areas.Any())
                        {
                            analysisInfo.AppendLine("**Task Areas**:");
                            foreach (var area in analysis.Areas)
                            {
                                analysisInfo.AppendLine($"- **{area.Name}**: {area.Tasks.Count} tasks");
                            }
                            analysisInfo.AppendLine();
                        }

                        specificationContext += analysisInfo.ToString();
                    }

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

            // Add external project context for existing projects
            if (_projectRepository != null && !string.IsNullOrEmpty(projectId))
            {
                var project = _projectRepository.GetById(projectId);
                if (project?.Metadata.TryGetValue("IsExistingProject", out var isExistingProject) == true &&
                    isExistingProject == "true")
                {
                    var extInfo = new System.Text.StringBuilder();
                    extInfo.AppendLine();
                    extInfo.AppendLine("---");
                    extInfo.AppendLine();
                    extInfo.AppendLine("## External Referenced Projects");
                    extInfo.AppendLine();

                    if (project.Metadata.TryGetValue("SourcePath", out var srcPath))
                    {
                        extInfo.AppendLine($"Working directory: `{srcPath}`");
                    }
                    if (project.Metadata.TryGetValue("ProjectType", out var projType))
                    {
                        extInfo.AppendLine($"Project type: {projType}");
                    }
                    extInfo.AppendLine();

                    if (project.ExternalProjectReferences.Count > 0)
                    {
                        extInfo.AppendLine("Referenced projects (accessible via relative paths):");
                        foreach (var extRef in project.ExternalProjectReferences)
                        {
                            extInfo.AppendLine($"  `{extRef.RelativePath}/` - {extRef.Name}");
                        }
                        extInfo.AppendLine();
                    }
                    else if (project.Metadata.TryGetValue("ExternalProjectPaths", out var extPathsJson))
                    {
                        // Fallback for legacy projects with data still in metadata
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(extPathsJson);
                            extInfo.AppendLine("Referenced projects (accessible via relative paths):");
                            foreach (var item in doc.RootElement.EnumerateArray())
                            {
                                var relPath = item.GetProperty("relativePath").GetString() ?? "";
                                var name = item.GetProperty("name").GetString() ?? "";
                                extInfo.AppendLine($"  `{relPath}/` - {name}");
                            }
                            extInfo.AppendLine();
                        }
                        catch { }
                    }

                    extInfo.AppendLine("IMPORTANT: If a task requires creating a NEW project or directory outside");
                    extInfo.AppendLine("the workspace and referenced projects, use the ask_user tool to confirm");
                    extInfo.AppendLine("the location with the user before creating files there.");
                    extInfo.AppendLine();

                    specificationContext = (specificationContext ?? "") + extInfo.ToString();
                }
            }

            // Extract project structure from Wyvern analysis if available
            var projectStructure = _wyvern?.Analysis?.Structure;

            // Assign the task with description, project, specification context, structure, and version
            kobold.AssignTask(
                Guid.Parse(task.Id),
                task.Task,
                projectId,
                specificationContext,
                projectStructure,
                specificationVersion,
                _specificationPath);

            // Pass combined project constraints to Kobold for constraint-aware planning
            var combinedConstraints = CombinedConstraints();
            if (combinedConstraints.Any())
            {
                kobold.SetProjectConstraints(combinedConstraints);
                _logger?.LogInformation("Kobold {KoboldId} gets {Count} project constraint(s) for task {TaskId}",
                    kobold.Id.ToString()[..8], combinedConstraints.Count, task.Id[..Math.Min(8, task.Id.Length)]);
            }

            // Add dependency context if task has dependencies

            // GAP FIX 1 & 4: Load full Specification and update feature context
            Specification? specification = null;
            if (!string.IsNullOrEmpty(_specificationPath))
            {
                specification = await LoadSpecificationAsync();
            }

            if (!string.IsNullOrEmpty(task.FeatureId) && specification != null)
            {
                try
                {
                    var (featureId, featureName, featureDesc, acceptanceCriteria) =
                        GetFeatureFromSpecification(specification, task.FeatureId);

                    kobold.UpdateFeatureContext(featureId, featureName, featureDesc);

                    // Also store the specification for plan creation
                    kobold.SetSpecification(specification);

                    _logger?.LogDebug(
                        "Set feature context for Kobold {KoboldId}: {FeatureName} with {AcceptanceCriteriaCount} acceptance criteria",
                        kobold.Id.ToString()[..8], featureName ?? task.FeatureId, acceptanceCriteria.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load feature context for task {TaskId}", task.Id);
                }
            }
            var dependencyContext = await BuildDependencyContextAsync(task.Task);
            if (!string.IsNullOrEmpty(dependencyContext))
            {
                // Append dependency information to specification context
                var currentContext = kobold.SpecificationContext ?? string.Empty;
                kobold.UpdateSpecificationContext(currentContext + dependencyContext);
                
                _logger?.LogDebug(
                    "Added dependency context to Kobold {KoboldId} for task {TaskId}",
                    kobold.Id.ToString()[..8], task.Id.ToString()[..8]);
            }

            // Gaps 1-3 & 7 fix: Pass task metadata (name, priority, complexity, structured dependencies) to Kobold
            var wyvernTask = FindWyvernTask(task);
            kobold.SetTaskMetadata(
                taskName: wyvernTask?.Name,
                taskPriority: wyvernTask?.Priority ?? task.Priority.ToString().ToLower(),
                taskComplexity: wyvernTask?.Complexity,
                taskDependencies: task.Dependencies?.Count > 0 ? task.Dependencies : null);

            // Track the mapping
            _taskToKoboldMap[task.Id] = kobold.Id;

            // Wire escalation callback so Kobold reflections route to Drake
            kobold.OnEscalation = alert =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleEscalationAsync(alert);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Unhandled error in escalation handler for Kobold {KoboldId}",
                            ShortWorkerId(alert));
                    }
                });
            };

            kobold.OnAskHuman = (question, _) => AskHumanAsync(task, question);
            kobold.IsTaskParked = () => IsTaskParked(task.Id);
            kobold.HumanAnswers = humanAnswers;

            // Track provider for circuit breaker and retry logic
            task.Provider = effectiveProvider;

            // Update task status
            _taskTracker.UpdateTask(task, TaskStatus.NotInitialized, agentType);
            SaveTasksToFile();

            return kobold;
        }

        private static string ShortWorkerId(EscalationAlert alert) =>
            alert.WorkerId is { Length: > 8 } id ? id[..8] : alert.WorkerId ?? "unknown";

        /// <summary>
        /// Handles an escalation alert from a Kobold or the ReasoningMonitor.
        /// Routes to the appropriate upstream agent based on escalation type.
        /// </summary>
        private static KoboldImplementationPlan CopyOfPlan(KoboldImplementationPlan plan) =>
            System.Text.Json.JsonSerializer.Deserialize<KoboldImplementationPlan>(System.Text.Json.JsonSerializer.Serialize(plan))!;

        /// <summary>Carries a revision made on a copy (<see cref="CopyOfPlan"/>) into the live plan.</summary>
        private static void ApplyRevision(KoboldImplementationPlan live, KoboldImplementationPlan revised)
        {
            live.Steps = revised.Steps;
            live.CurrentStepIndex = revised.CurrentStepIndex;
            live.Status = revised.Status;
            live.ErrorMessage = revised.ErrorMessage;
            live.ExecutionLog = revised.ExecutionLog;
            live.UpdatedAt = revised.UpdatedAt;
        }

        private bool EscalatedTaskIsDone(EscalationAlert alert) =>
            !string.IsNullOrEmpty(alert.TaskId) && _taskTracker.GetTaskById(alert.TaskId)?.Status == TaskStatus.Done;

        /// <summary>Closes an escalation nothing acted on: logged, with no plan change and no notification.</summary>
        private void DropEscalation(EscalationAlert alert, string why)
        {
            alert.Status = EscalationStatus.Resolved;
            alert.Resolution = $"Dropped: {why}";
            alert.ResolvedAt = DateTime.UtcNow;
            _logger?.LogInformation("Dropped escalation {AlertId} ({Type}) for task {TaskId}: {Why}",
                alert.Id[..8], alert.Type, alert.TaskId?[..Math.Min(8, alert.TaskId.Length)], why);
        }

        public async Task HandleEscalationAsync(EscalationAlert alert, EscalationDisposition? disposition = null)
        {
            // A run's last reflections can escalate after its task finished: nothing is left to act on, so neither the
            // plan nor the person is troubled with it (TASK-182)
            if (EscalatedTaskIsDone(alert))
            {
                DropEscalation(alert, "the task was already Done");
                return;
            }

            if (await TryRouteToHumanAsync(alert, disposition))
                return;

            alert.Status = EscalationStatus.InProgress;
            var resolution = "";

            try
            {
                _logger?.LogWarning(
                    "Handling escalation {AlertId}: {Type} from Kobold {KoboldId} for task {TaskId}",
                    alert.Id[..8], alert.Type, ShortWorkerId(alert), alert.TaskId?[..8] ?? "unknown");

                switch (alert.Type)
                {
                    case EscalationType.WrongApproach:
                        // Route to Planner for plan revision
                        if (_plannerAgent != null && _planService != null)
                        {
                            var kobold = Guid.TryParse(alert.WorkerId, out var workerId) ? _koboldFactory.GetKobold(workerId) : null;
                            // With no live Kobold holding a plan, the saved one is revised: a parked task whose decision
                            // timed out, a task reopened by orphan recovery, or one reopened after its run ended (TASK-173)
                            var currentPlan = kobold?.ImplementationPlan
                                ?? (!string.IsNullOrEmpty(alert.TaskId)
                                    ? await _planService.LoadPlanAsync(alert.ProjectId, alert.TaskId)
                                    : null);
                            if (currentPlan != null)
                            {
                                // The planner revises a copy: a revision dropped below leaves the live plan untouched
                                var revisedPlan = await _plannerAgent.RevisePlanAsync(
                                    CopyOfPlan(currentPlan),
                                    alert.ReflectionHistory,
                                    alert);
                                // The run went on during the revision and finished the task: the revision would
                                // replace the plan of finished work, so it is dropped with the escalation (TASK-182)
                                if (EscalatedTaskIsDone(alert))
                                {
                                    DropEscalation(alert, "the task finished while its plan was being revised");
                                    return;
                                }
                                if (revisedPlan != null)
                                {
                                    // In place: a running Kobold's plan tools hold the live plan object
                                    ApplyRevision(currentPlan, revisedPlan);
                                    if (kobold != null)
                                        kobold.ImplementationPlan = currentPlan;
                                    await _planService.SavePlanDebouncedAsync(currentPlan);
                                    resolution = $"Plan revised with {revisedPlan.Steps.Count(s => s.Status == StepStatus.Pending)} new/revised steps";
                                }
                                else
                                {
                                    resolution = "Plan revision returned no changes";
                                }
                            }
                            else
                            {
                                resolution = "No active plan to revise";
                            }
                        }
                        else
                        {
                            resolution = "Planner not available for plan revision";
                        }
                        break;

                    case EscalationType.TaskInfeasible:
                    case EscalationType.NeedsSplit:
                    case EscalationType.MissingDependency:
                        // Route to Wyvern for task refinement
                        if (_wyvern != null && !string.IsNullOrEmpty(alert.TaskId))
                        {
                            var task = _taskTracker.GetTaskById(alert.TaskId);
                            if (task != null)
                            {
                                // Extract area from task file path (e.g., "frontend-tasks.md" → "frontend")
                                var area = Path.GetFileNameWithoutExtension(_outputMarkdownPath)
                                    .Replace("-tasks", "", StringComparison.OrdinalIgnoreCase);
                                var result = await _wyvern.RefineTaskAsync(
                                    alert.TaskId,
                                    area,
                                    alert,
                                    alert.ReflectionHistory);

                                if (result != null)
                                {
                                    resolution = $"Wyvern refinement: {result.Action} - {result.Summary}";

                                    // Mark current task as failed so it doesn't keep running
                                    if (result.Action != RefinementAction.NoChange)
                                    {
                                        _taskTracker.UpdateTask(task, TaskStatus.Failed, task.AssignedAgent);
                                        task.ErrorMessage = $"Escalated: {alert.Type} - {alert.Summary}";
                                        SaveTasksToFile();
                                    }
                                }
                                else
                                {
                                    resolution = "Wyvern refinement returned no result";
                                }
                            }
                            else
                            {
                                resolution = $"Task {alert.TaskId} not found in tracker";
                            }
                        }
                        else
                        {
                            resolution = "Wyvern not available for task refinement";
                        }
                        break;

                    case EscalationType.WrongAgentType:
                        // Mark task for reassignment with different agent type
                        if (!string.IsNullOrEmpty(alert.TaskId))
                        {
                            var task = _taskTracker.GetTaskById(alert.TaskId);
                            if (task != null)
                            {
                                _taskTracker.UpdateTask(task, TaskStatus.Unassigned, null);
                                task.ErrorMessage = null;
                                SaveTasksToFile();
                                resolution = $"Task reset to Unassigned for reassignment with different agent type";
                            }
                            else
                            {
                                resolution = $"Task {alert.TaskId} not found";
                            }
                        }
                        break;
                }

                alert.Status = EscalationStatus.Resolved;
                alert.Resolution = resolution;
                alert.ResolvedAt = DateTime.UtcNow;

                _logger?.LogInformation(
                    "Escalation {AlertId} resolved: {Resolution}",
                    alert.Id[..8], resolution);
            }
            catch (Exception ex)
            {
                alert.Status = EscalationStatus.Failed;
                alert.Resolution = $"Error: {ex.Message}";
                alert.ResolvedAt = DateTime.UtcNow;
                resolution = alert.Resolution;

                _logger?.LogError(ex,
                    "Failed to handle escalation {AlertId}: {Type}",
                    alert.Id[..8], alert.Type);
            }

            // Persist task state immediately after escalation to prevent data loss on crash
            try
            {
                SaveTasksToFile();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist task state after escalation {AlertId}", alert.Id[..8]);
            }

            // Notify Dragon about the escalation
            try
            {
                var projectName = _projectRepository?.GetById(_projectId ?? "")?.Name ?? _projectId ?? "unknown";
                _onEscalationNotify?.Invoke(projectName, alert, resolution);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to send escalation notification");
            }
        }

        /// <summary>
        /// Resolves the workspace path for Kobold execution.
        /// For existing projects, uses the original source path.
        /// For new projects, derives workspace from task file path.
        /// </summary>
        private string? ResolveWorkspacePath()
        {
            if (_projectRepository != null && !string.IsNullOrEmpty(_projectId))
            {
                var project = _projectRepository.GetById(_projectId);
                if (project?.Metadata.TryGetValue("IsExistingProject", out var isExisting) == true &&
                    isExisting == "true" &&
                    project.Metadata.TryGetValue("SourcePath", out var sourcePath) == true &&
                    Directory.Exists(sourcePath))
                {
                    return sourcePath;
                }
            }

            // Default: derive workspace from task file path
            var taskFolder = Path.GetDirectoryName(_outputMarkdownPath);
            var projectFolder = !string.IsNullOrEmpty(taskFolder) ? Path.GetDirectoryName(taskFolder) : null;
            return !string.IsNullOrEmpty(projectFolder) ? Path.Combine(projectFolder, "workspace") : null;
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
                    baseSpecification = File.ReadAllTextAsync(_specificationPath).GetAwaiter().GetResult();
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
                _taskToKoboldMap.TryRemove(taskIdString, out _);
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
        internal async Task SyncTaskFromKoboldAsync(Kobold kobold, string? worktreePath = null)
        {
            if (!kobold.TaskId.HasValue)
                return;

            var task = _taskTracker.GetTaskById(kobold.TaskId.Value.ToString());
            if (task == null)
                return;

            // A parked task leaves its state only through its pending decision, never from a Kobold's status
            if (task.Status == TaskStatus.AwaitingHumanDecision)
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

            // The acceptance check decides before Done reaches the WAL, the event bus or the circuit breaker (TASK-025)
            if (_acceptance != null && previousStatus != TaskStatus.Done && taskStatus == TaskStatus.Done && kobold.IsSuccess
                && !await PassesAcceptanceAsync(task, kobold, worktreePath))
                return;

            // Update task with WAL logging for crash safety
            await UpdateTaskWithWalAsync(task, taskStatus, task.AssignedAgent, kobold.HasError ? (kobold.ErrorMessage ?? "Unknown error") : null);

            // Unregister agent from shared planning context when done or failed
            if ((taskStatus == TaskStatus.Done || taskStatus == TaskStatus.Failed) &&
                !string.IsNullOrEmpty(_projectId) && _sharedPlanningContext != null)
            {
                _ = _sharedPlanningContext.UnregisterAgentAsync(
                    kobold.Id.ToString(),
                    taskStatus == TaskStatus.Done,
                    kobold.ErrorMessage);
            }

            // Publish Kobold lifecycle terminal event (Completed/Failed/TimedOut)
            if ((taskStatus == TaskStatus.Done || taskStatus == TaskStatus.Failed) && _eventBus != null)
            {
                var action = taskStatus == TaskStatus.Done
                    ? KoboldLifecycleAction.Completed
                    : (kobold.ErrorMessage?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true
                        ? KoboldLifecycleAction.TimedOut
                        : KoboldLifecycleAction.Failed);

                try
                {
                    _ = _eventBus.PublishAsync(new KoboldLifecycleEvent
                    {
                        ProjectId = _projectId ?? "unknown",
                        TaskId = task.Id,
                        AgentType = task.AssignedAgent ?? "unknown",
                        KoboldId = kobold.Id,
                        Action = action
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to publish KoboldLifecycleEvent.{Action} for kobold {KoboldId}", action, kobold.Id);
                }
            }

            // Clean up conversation checkpoint when task reaches terminal state
            if ((taskStatus == TaskStatus.Done || taskStatus == TaskStatus.Failed) && 
                !string.IsNullOrEmpty(_projectId) && _planService != null)
            {
                _ = _planService.DeleteConversationCheckpointAsync(_projectId, task.Id);
                _logger?.LogDebug(
                    "Cleaned up conversation checkpoint for completed task {TaskId} (status: {Status})",
                    task.Id[..Math.Min(8, task.Id.Length)], taskStatus);
            }

            // Report failure to circuit breaker
            if (taskStatus == TaskStatus.Failed && !string.IsNullOrWhiteSpace(task.Provider))
            {
                _circuitBreaker?.RecordFailure(task.Provider);
                _logger?.LogDebug(
                    "Reported failure to circuit breaker for provider {Provider} (task {TaskId})",
                    task.Provider, task.Id[..Math.Min(8, task.Id.Length)]);
            }
            // Report success to circuit breaker to reset failure counter
            else if (taskStatus == TaskStatus.Done && !string.IsNullOrWhiteSpace(task.Provider))
            {
                _circuitBreaker?.RecordSuccess(task.Provider);
            }

            // Log status transition if changed and publish event
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

                // Publish task status changed event
                if (_eventBus != null)
                {
                    try
                    {
                        _ = _eventBus.PublishAsync(new TaskStatusChangedEvent
                        {
                            ProjectId = _projectId ?? "unknown",
                            TaskId = task.Id,
                            OldStatus = previousStatus.ToString(),
                            NewStatus = taskStatus.ToString(),
                            ErrorMessage = kobold.HasError ? kobold.ErrorMessage : null
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Failed to publish TaskStatusChangedEvent for task {TaskId}", task.Id[..Math.Min(8, task.Id.Length)]);
                    }
                }
            }

            // Bug A fix — run side-effects (verification + commit) BEFORE persisting Done to disk.
            // The WAL entry from UpdateTaskWithWalAsync above gives us crash-recovery semantics if we
            // crash between here and the final save; the WAL replay path can re-run the verify+commit.
            // If we saved Done to disk before commit ran, a mid-flow crash would leave a permanent Done
            // status with no commit in git history and no recovery trigger.
            if (previousStatus != TaskStatus.Done && taskStatus == TaskStatus.Done && kobold.IsSuccess)
            {
                var verificationPassed = await RunPostTaskVerificationAsync(kobold, task, worktreePath);
                if (!verificationPassed)
                {
                    _logger?.LogWarning(
                        "Post-task verification FAILED for task {TaskId} - task remains Done but verification errors were logged",
                        task.Id[..Math.Min(8, task.Id.Length)]);
                }

                // CommitTaskCompletionAsync may demote task.Status to Failed on commit failure (Option 1).
                await CommitTaskCompletionAsync(kobold, task, worktreePath);
            }

            // Persist final state. Read task.Status (not the local taskStatus) since side-effects above
            // may have demoted Done -> Failed. Immediate save for terminal states prevents race with
            // ReloadTasksFromFileAsync; debounced save is fine for in-flight transitions.
            if (task.Status == TaskStatus.Done || task.Status == TaskStatus.Failed)
            {
                await SaveTasksToFileAsync();
            }
            else
            {
                SaveTasksToFile();
            }
        }

        /// <summary>
        /// Runs post-task verification checks (build, lint, etc.) after a task completes.
        /// Uses verification steps from Wyrm recommendations if available.
        /// Returns true if verification passes (or no verification steps configured).
        /// </summary>
        private async Task<bool> RunPostTaskVerificationAsync(Kobold kobold, TaskRecord task, string? worktreePath = null)
        {
            // Get verification steps from Wyrm recommendations
            var verificationSteps = _wyrmRecommendation?.VerificationSteps?
                .Where(v => v.Priority == "Critical")
                .ToList();

            if (verificationSteps == null || !verificationSteps.Any())
                return true;

            var workspacePath = worktreePath ?? ResolveWorkspacePath();
            if (string.IsNullOrEmpty(workspacePath) || !Directory.Exists(workspacePath))
                return true;

            var allPassed = true;

            foreach (var step in verificationSteps)
            {
                try
                {
                    var workDir = !string.IsNullOrEmpty(step.WorkingDirectory)
                        ? Path.Combine(workspacePath, step.WorkingDirectory)
                        : workspacePath;

                    // Only run if the working directory exists and has relevant files
                    if (!Directory.Exists(workDir))
                        continue;

                    // Check if this verification is relevant (e.g., don't run tsc if no .ts files)
                    if (step.CheckType == "build" && step.Command.Contains("tsc"))
                    {
                        var hasTsFiles = Directory.GetFiles(workDir, "*.ts", SearchOption.AllDirectories)
                            .Any(f => !f.Contains("node_modules"));
                        if (!hasTsFiles) continue;

                        // Also need tsconfig.json
                        if (!File.Exists(Path.Combine(workDir, "tsconfig.json"))) continue;
                    }

                    _logger?.LogDebug("Running verification: {Description} ({Command}) in {WorkDir}",
                        step.Description, step.Command, workDir);

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = OperatingSystem.IsWindows() ? "cmd" : "sh",
                        Arguments = OperatingSystem.IsWindows() ? $"/c {step.Command}" : $"-c \"{step.Command}\"",
                        WorkingDirectory = workDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(psi);
                    if (process == null) continue;

                    var timeoutMs = step.TimeoutSeconds > 0 ? step.TimeoutSeconds * 1000 : 60000;
                    var exited = process.WaitForExit(timeoutMs);

                    if (!exited)
                    {
                        process.Kill();
                        _logger?.LogWarning("Verification timed out: {Description}", step.Description);
                        continue; // Don't fail on timeout — just skip
                    }

                    var stdout = await process.StandardOutput.ReadToEndAsync();
                    var stderr = await process.StandardError.ReadToEndAsync();

                    var success = step.SuccessCriteria switch
                    {
                        "exit_code_0" => process.ExitCode == 0,
                        var s when s.StartsWith("contains:") => (stdout + stderr).Contains(s["contains:".Length..]),
                        _ => process.ExitCode == 0
                    };

                    if (!success)
                    {
                        allPassed = false;
                        var errorOutput = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                        var truncated = errorOutput.Length > 500 ? errorOutput[..500] + "..." : errorOutput;
                        _logger?.LogWarning(
                            "Verification FAILED: {Description}\n  Command: {Command}\n  Exit code: {ExitCode}\n  Output: {Output}",
                            step.Description, step.Command, process.ExitCode, truncated);
                    }
                    else
                    {
                        _logger?.LogDebug("Verification PASSED: {Description}", step.Description);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Verification step skipped due to error: {Description}", step.Description);
                    // Don't fail the task for verification infrastructure errors
                }
            }

            return allPassed;
        }

        /// <summary>
        /// Commits changes to git when a task is completed
        /// </summary>
        /// <summary>
        /// Sets up a git worktree for the task's feature branch, enabling parallel work across branches.
        /// Returns (branchName, worktreePath) if a worktree was created, (null, null) otherwise.
        /// </summary>
        /// <summary>
        /// The git branch of the feature a task belongs to (its <see cref="TaskRecord.FeatureId"/>), or null
        /// </summary>
        public static string? ResolveFeatureBranch(TaskRecord task, IEnumerable<Feature> features)
        {
            if (string.IsNullOrEmpty(task.FeatureId))
                return null;
            var branch = features.FirstOrDefault(f => string.Equals(f.Id, task.FeatureId, StringComparison.Ordinal))?.GitBranch;
            return string.IsNullOrWhiteSpace(branch) ? null : branch;
        }

        private async Task<(string? BranchName, string? WorktreePath)> SetupFeatureBranchWorktreeAsync(TaskRecord task)
        {
            if (_gitService == null)
                return (null, null);

            try
            {
                var projectFolder = GetProjectFolder();
                if (projectFolder == null) return (null, null);

                if (!await _gitService.IsGitInstalledAsync() || !await _gitService.IsRepositoryAsync(projectFolder))
                    return (null, null);

                // Find the feature branch for this task
                string? featureBranch = null;

                // First try: the task's own FeatureId against the features sidecar (no Wyvern instance needed)
                if (!string.IsNullOrEmpty(task.FeatureId))
                {
                    var specification = await LoadSpecificationAsync();
                    featureBranch = ResolveFeatureBranch(task, specification?.Features ?? new List<Feature>())
                        ?? _wyvern?.GetFeatureBranch(task.FeatureId);
                }

                // Second try: find via Wyvern analysis task-to-feature mapping
                if (featureBranch == null && _wyvern?.Analysis != null)
                {
                    foreach (var area in _wyvern.Analysis.Areas)
                    {
                        var wyvernTask = area.Tasks.FirstOrDefault(t =>
                            task.Task.Contains(t.Description, StringComparison.OrdinalIgnoreCase) ||
                            t.Description.Contains(task.Task.Split('(')[0].Trim(), StringComparison.OrdinalIgnoreCase));

                        if (wyvernTask?.FeatureId != null)
                        {
                            featureBranch = _wyvern.GetFeatureBranch(wyvernTask.FeatureId);
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(featureBranch))
                    return (null, null);

                // Create a worktree for this feature branch (reuses existing if available)
                var worktreePath = await _gitService.CreateWorktreeAsync(projectFolder, featureBranch);
                if (worktreePath != null)
                {
                    _logger?.LogInformation("Worktree ready at {Path} for branch {Branch} (task {TaskId})",
                        worktreePath, featureBranch, task.Id[..Math.Min(8, task.Id.Length)]);
                    return (featureBranch, worktreePath);
                }

                _logger?.LogWarning("Failed to create worktree for branch {Branch} (task {TaskId})",
                    featureBranch, task.Id[..Math.Min(8, task.Id.Length)]);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error setting up worktree for task {TaskId}", task.Id);
            }

            return (null, null);
        }

        /// <summary>Runs of this Drake using each feature worktree right now (TASK-168): a worktree is shared by its feature's tasks.</summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _worktreeUsers =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>A run starts using <paramref name="worktreePath"/>.</summary>
        internal void AcquireWorktree(string worktreePath) =>
            _worktreeUsers.AddOrUpdate(worktreePath, 1, (_, n) => n + 1);

        /// <summary>
        /// A run is done with its feature worktree (TASK-168). The worktree is removed only when nothing would be lost:
        /// it is kept while its commit failed (manual recovery), while its task waits on a decision, while another run of
        /// the feature still uses it, and while it holds uncommitted work — a run cancelled by a shutdown, a failed run,
        /// or any run that ended without committing, so a resumed run continues on its files. A worktree kept that way
        /// is reused by the feature's next run (<see cref="GitService.CreateWorktreeAsync"/>) and removed after the run
        /// that commits and leaves it clean. Returns why it was kept, or null when it was removed.
        /// </summary>
        internal async Task<string?> ReleaseWorktreeAsync(TaskRecord task, string? worktreePath)
        {
            if (worktreePath == null)
                return null;

            var others = _worktreeUsers.AddOrUpdate(worktreePath, 0, (_, n) => Math.Max(0, n - 1));
            if (others == 0)
                _worktreeUsers.TryRemove(new KeyValuePair<string, int>(worktreePath, 0));

            string? keep = null;
            if (task.CommitFailed)
                keep = "its commit failed — manual recovery required";
            else if (task.Status == TaskStatus.AwaitingHumanDecision)
                keep = "its task waits on a decision";
            else if (others > 0)
                keep = "another task of this feature is still running in it";
            else if (_gitService != null && await _gitService.HasUncommittedChangesAsync(worktreePath) != false)
                keep = "it holds work no commit has (the run ended without committing)";

            if (keep != null)
            {
                _logger?.LogInformation("Keeping worktree {Path} after task {TaskId}: {Reason}",
                    worktreePath, task.Id[..Math.Min(8, task.Id.Length)], keep);
                return keep;
            }

            await CleanupWorktreeAsync(worktreePath);
            return null;
        }

        /// <summary>
        /// Removes a git worktree after task execution (<see cref="ReleaseWorktreeAsync"/> decides whether).
        /// </summary>
        private async Task CleanupWorktreeAsync(string worktreePath)
        {
            if (_gitService == null) return;

            try
            {
                var projectFolder = GetProjectFolder();
                if (projectFolder == null) return;

                await _gitService.RemoveWorktreeAsync(projectFolder, worktreePath);
                _logger?.LogDebug("Cleaned up worktree at {Path}", worktreePath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to cleanup worktree at {Path}", worktreePath);
            }
        }

        /// <summary>
        /// Gets the folder where the git repository lives.
        /// For external projects: returns the SourcePath (where the real .git is)
        /// For new projects: returns the KoboldLair project folder (parent of tasks/)
        /// </summary>
        private string? GetProjectFolder()
        {
            // For external projects, git operations target the external source directory
            if (_projectRepository != null && !string.IsNullOrEmpty(_projectId))
            {
                var project = _projectRepository.GetById(_projectId);
                if (project?.Metadata.TryGetValue("IsExistingProject", out var isExisting) == true &&
                    isExisting == "true" &&
                    project.Metadata.TryGetValue("SourcePath", out var sourcePath) == true &&
                    Directory.Exists(sourcePath))
                {
                    return sourcePath;
                }
            }

            // For new projects: go up from tasks/ to the project root
            var tasksDir = Path.GetDirectoryName(_outputMarkdownPath);
            if (string.IsNullOrEmpty(tasksDir)) return null;

            var projectRoot = Path.GetDirectoryName(tasksDir);
            return !string.IsNullOrEmpty(projectRoot) && Directory.Exists(projectRoot) ? projectRoot : tasksDir;
        }

        private async Task CommitTaskCompletionAsync(Kobold kobold, TaskRecord task, string? worktreePath = null)
        {
            if (_gitService == null)
                return;

            try
            {
                // Use worktree path if available, otherwise fall back to project folder
                var commitFolder = worktreePath ?? GetProjectFolder();
                if (string.IsNullOrEmpty(commitFolder))
                    return;

                if (!await _gitService.IsGitInstalledAsync())
                    return;

                if (!await _gitService.IsRepositoryAsync(commitFolder))
                    return;

                var currentBranch = await _gitService.GetCurrentBranchAsync(commitFolder);

                // Stage all changes
                await _gitService.StageAllAsync(commitFolder);

                // Create detailed commit message with context
                var commitMessage = BuildDetailedCommitMessage(kobold, task, currentBranch);

                // Commit with Kobold agent type as author
                var authorName = $"Kobold-{kobold.AgentType}";
                var commitResult = await _gitService.CommitChangesAsync(commitFolder, commitMessage, authorName);

                if (commitResult == CommitResult.Committed)
                {
                    var projectInfo = _projectId ?? "unknown project";
                    var taskSummary = task.Task.Length > 50 ? task.Task[..50] + "..." : task.Task;
                    _logger?.LogInformation(
                        "✓ Git commit successful\n" +
                        "  Project: {ProjectId}\n" +
                        "  Branch: {Branch}\n" +
                        "  Task: {Task}\n" +
                        "  Agent: {AgentType}",
                        projectInfo, currentBranch ?? "current branch", taskSummary, kobold.AgentType);

                    // Track commit SHA and output files for dependency context
                    var commitSha = await _gitService.GetLastCommitShaAsync(commitFolder);
                    if (!string.IsNullOrEmpty(commitSha))
                    {
                        task.CommitSha = commitSha;
                        task.OutputFiles = await _gitService.GetFilesFromCommitAsync(commitFolder, commitSha);

                        _logger?.LogDebug(
                            "Tracked {FileCount} output files for task {TaskId} (commit: {CommitSha})",
                            task.OutputFiles.Count, task.Id.ToString()[..8], commitSha[..8]);

                        // GAP 4 FIX: Register output files with SharedPlanningContextService
                        await RegisterOutputFilesWithContextAsync(kobold, task);
                    }
                }
                else if (commitResult == CommitResult.NoChanges)
                {
                    // Kobold reported success but produced no diff — this is wrong_approach territory.
                    // Demote the task to Failed so it gets retried / escalated instead of treated as Done.
                    _logger?.LogError(
                        "Task {TaskId} marked Done by Kobold but produced no committable changes — demoting to Failed",
                        task.Id[..Math.Min(8, task.Id.Length)]);
                    task.CommitFailed = true;
                    task.Status = TaskStatus.Failed;
                    task.ErrorMessage = "Kobold reported success but workspace produced no diff. The task description may be impossible to satisfy or the Kobold wrote to the wrong location.";
                    await SaveTasksToFileAsync();
                }
                else // CommitResult.Failed
                {
                    _logger?.LogError(
                        "git commit failed for task {TaskId} — code is staged but uncommitted; demoting to Failed",
                        task.Id[..Math.Min(8, task.Id.Length)]);
                    task.CommitFailed = true;
                    task.Status = TaskStatus.Failed;
                    task.ErrorMessage = "git commit failed. Code may exist in the worktree (preserved for manual recovery) but is not in git history.";
                    await SaveTasksToFileAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Git commit threw for task {TaskId} — demoting to Failed", task.Id);
                task.CommitFailed = true;
                task.Status = TaskStatus.Failed;
                task.ErrorMessage = $"git commit threw an exception: {ex.Message}";
                await SaveTasksToFileAsync();
            }
        }

        /// <summary>
        /// Registers output files from a completed task with SharedPlanningContextService (GAP 4 FIX)
        /// </summary>
        private async Task RegisterOutputFilesWithContextAsync(Kobold kobold, TaskRecord task)
        {
            if (_sharedPlanningContext == null || string.IsNullOrEmpty(_projectId))
                return;

            if (task.OutputFiles == null || !task.OutputFiles.Any())
                return;

            try
            {
                foreach (var filePath in task.OutputFiles)
                {
                    // Determine if this was a creation or modification
                    var isCreation = true; // Assume creation for now, could be enhanced with git diff analysis

                    // Infer purpose from implementation plan if available
                    var purpose = _sharedPlanningContext.InferFilePurpose(filePath, kobold.ImplementationPlan);

                    // If no plan, create a basic purpose from the task description
                    if (kobold.ImplementationPlan == null && !string.IsNullOrEmpty(task.Task))
                    {
                        var taskPreview = task.Task.Length > 100 ? task.Task.Substring(0, 100) + "..." : task.Task;
                        purpose = $"Part of task: {taskPreview}";
                    }

                    await _sharedPlanningContext.UpdateFileMetadataAsync(
                        _projectId,
                        filePath,
                        purpose,
                        task.Id,
                        isCreation);
                }

                _logger?.LogDebug(
                    "Registered {Count} output files with SharedPlanningContext for task {TaskId}",
                    task.OutputFiles.Count, task.Id[..Math.Min(8, task.Id.Length)]);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to register output files with SharedPlanningContext for task {TaskId}", task.Id);
                // Non-critical, don't fail the workflow
            }
        }

        /// <summary>
        /// Builds a detailed commit message with task context, feature info, dependencies, and priority.
        /// Format follows conventional commit style with trailers for traceability.
        /// </summary>
        private string BuildDetailedCommitMessage(Kobold kobold, TaskRecord task, string? currentBranch)
        {
            var sb = new System.Text.StringBuilder();

            // Subject line: short description (max ~72 chars for git best practices)
            var taskDesc = task.Task;

            // Remove task ID prefix if present (e.g., "[abc123] Task name")
            var taskIdPrefixMatch = System.Text.RegularExpressions.Regex.Match(taskDesc, @"^\[[^\]]+\]\s*");
            if (taskIdPrefixMatch.Success)
            {
                taskDesc = taskDesc[taskIdPrefixMatch.Length..];
            }

            // Remove dependency suffix if present (e.g., "Task name (depends on: X)")
            var dependsSuffixMatch = System.Text.RegularExpressions.Regex.Match(taskDesc, @"\s*\(depends on:[^)]+\)\s*$");
            if (dependsSuffixMatch.Success)
            {
                taskDesc = taskDesc[..dependsSuffixMatch.Index];
            }

            // Truncate for subject line
            var subject = taskDesc.Length > 60 ? taskDesc[..57] + "..." : taskDesc;
            sb.AppendLine($"feat({kobold.AgentType}): {subject}");
            sb.AppendLine();

            // Body: full task description if different from subject
            if (taskDesc.Length > 60)
            {
                sb.AppendLine(taskDesc);
                sb.AppendLine();
            }

            // Trailers for traceability (git trailer format)
            sb.AppendLine($"Task-Id: {task.Id}");
            sb.AppendLine($"Agent-Type: {kobold.AgentType}");
            sb.AppendLine($"Priority: {task.Priority}");

            // Extract and add feature info
            var (featureId, featureName) = GetFeatureInfoForTask(task, currentBranch);
            if (!string.IsNullOrEmpty(featureId))
            {
                var featureDisplay = !string.IsNullOrEmpty(featureName)
                    ? $"{featureName} ({featureId[..Math.Min(8, featureId.Length)]})"
                    : featureId[..Math.Min(8, featureId.Length)];
                sb.AppendLine($"Feature: {featureDisplay}");
            }

            // Extract and add dependencies
            var dependencies = ExtractDependencies(task.Task);
            if (dependencies.Any())
            {
                sb.AppendLine($"Depends-On: {string.Join(", ", dependencies)}");
            }

            // Add project context if available
            if (!string.IsNullOrEmpty(_projectId))
            {
                sb.AppendLine($"Project: {_projectId[..Math.Min(8, _projectId.Length)]}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets feature information for a task from Wyvern analysis or branch name.
        /// </summary>
        private (string? featureId, string? featureName) GetFeatureInfoForTask(TaskRecord task, string? currentBranch)
        {
            // The task's own feature link wins; the branch name supplies a readable feature name
            if (!string.IsNullOrEmpty(task.FeatureId))
            {
                var name = string.IsNullOrEmpty(currentBranch)
                    ? null
                    : System.Text.RegularExpressions.Regex.Match(currentBranch, @"^feature/[^-]+-(.+)$") is { Success: true } m
                        ? m.Groups[1].Value.Replace("-", " ")
                        : null;
                return (task.FeatureId, _wyvern?.GetFeatureNameById(task.FeatureId) ?? name);
            }

            // Try to get feature info from Wyvern analysis first
            if (_wyvern?.Analysis != null)
            {
                foreach (var area in _wyvern.Analysis.Areas)
                {
                    // Match by task description (WyvernTask.Id may differ from TaskRecord.Id)
                    var wyvernTask = area.Tasks.FirstOrDefault(t =>
                        task.Task.Contains(t.Description, StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains(task.Task.Split('(')[0].Trim(), StringComparison.OrdinalIgnoreCase));

                    if (wyvernTask?.FeatureId != null)
                    {
                        // Look up feature name from specification
                        var featureName = _wyvern.GetFeatureNameById(wyvernTask.FeatureId);
                        return (wyvernTask.FeatureId, featureName);
                    }
                }
            }

            // Fall back to extracting from branch name (feature/{id}-{name})
            if (!string.IsNullOrEmpty(currentBranch))
            {
                var branchMatch = System.Text.RegularExpressions.Regex.Match(
                    currentBranch, @"^feature/([^-]+)-(.+)$");
                if (branchMatch.Success)
                {
                    var featureId = branchMatch.Groups[1].Value;
                    var featureName = branchMatch.Groups[2].Value.Replace("-", " ");
                    return (featureId, featureName);
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Finds the original WyvernTask for a TaskRecord by matching the task ID embedded in the description.
        /// Task descriptions are formatted as "[AREA-001] Task name (depends on: ...)" by Wyvern.
        /// Returns null if Wyvern analysis is not available or task is not found.
        /// </summary>
        private WyvernTask? FindWyvernTask(TaskRecord task)
        {
            if (_wyvern?.Analysis == null)
                return null;

            // Extract WyvernTask ID from task description: [AREA-001] Task name...
            var idMatch = System.Text.RegularExpressions.Regex.Match(task.Task, @"^\[([a-zA-Z]+-\d+)\]");
            if (idMatch.Success)
            {
                var wyvernTaskId = idMatch.Groups[1].Value;
                foreach (var area in _wyvern.Analysis.Areas)
                {
                    var found = area.Tasks.FirstOrDefault(t =>
                        string.Equals(t.Id, wyvernTaskId, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts dependency task IDs from a task description.
        /// Format: "Task name (depends on: dep1, dep2)"
        /// </summary>
        private static List<string> ExtractDependencies(string taskDescription)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                taskDescription, @"\(depends on:\s*([^)]+)\)");

            if (!match.Success)
                return new List<string>();

            return match.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();
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
            Action<string, string>? messageCallback = null,
            CancellationToken cancellationToken = default,
            Guid? runId = null)
        {
            // Set up a worktree for the feature branch (safe for parallel execution)
            var (featureBranch, worktreePath) = await SetupFeatureBranchWorktreeAsync(task);

            // Summon Kobold (may return null if resource limit reached)
            var kobold = await SummonKoboldAsync(task, agentType, provider);

            if (kobold == null)
            {
                // Resource limit reached, task remains in queue for retry
                var projectId = task.ProjectId ?? _projectId ?? "(default)";
                messageCallback?.Invoke("info", $"⏸️ Drake cannot summon kobold for task {task.Id.ToString()[..8]} - project {projectId} at parallel limit. Will retry.");
                return null;
            }

            // Adopt an externally-supplied run id (TASK-040 /kobold project mode) so a transport already
            // subscribed to this run's telemetry receives it under the id it is watching.
            if (runId.HasValue)
                kobold.AssignRunId(runId.Value);

            // If we have a worktree, redirect the Kobold's workspace to it
            if (worktreePath != null)
            {
                var worktreeWorkspace = Path.Combine(worktreePath, "workspace");
                if (!Directory.Exists(worktreeWorkspace))
                    Directory.CreateDirectory(worktreeWorkspace);
                kobold.Agent.Options.WorkingDirectory = worktreeWorkspace;
            }

            if (featureBranch != null)
                messageCallback?.Invoke("info", $"🐉 Drake summoned Kobold {kobold.Id.ToString()[..8]} on branch `{featureBranch}` (worktree) for task {task.Id.ToString()[..8]}");
            else
                messageCallback?.Invoke("info", $"🐉 Drake summoned Kobold {kobold.Id.ToString()[..8]} for task {task.Id.ToString()[..8]}");

            List<Message> messages;
            bool koboldRemovedByParallel = false;

            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            BeginRun(task.Id, runCancellation);
            cancellationToken = runCancellation.Token;
            if (worktreePath != null)
                AcquireWorktree(worktreePath);

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
                        var plan = await kobold.EnsurePlanAsync(_planService, _plannerAgent, _sharedPlanningContext);
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
                    // Phase 4: Check if parallel execution mode is enabled
                    bool useParallelExecution = _planningConfig?.ExecutionMode == "parallel" && _useEnhancedExecution;

                    if (useParallelExecution)
                    {
                        // Parallel execution: Remove the initial Kobold and use parallel orchestration
                        messageCallback?.Invoke("info", "🔄 Parallel execution mode enabled");

                        // Sync current state from the initial Kobold
                        await SyncTaskFromKoboldAsync(kobold);

                        // Get the plan before removing the Kobold
                        var plan = kobold.ImplementationPlan;
                        var taskIdStr = task.Id; // TaskId is used to load plans

                        // Remove the initial Kobold (we'll create new ones for each batch)
                        _koboldFactory.RemoveKobold(kobold.Id);
                        koboldRemovedByParallel = true;

                        // Use parallel execution
                        var analyzer = new Birko.AI.Orchestration.Services.StepDependencyAnalyzer();
                        messages = await ExecuteTaskParallelAsync(
                            task, plan, analyzer, agentType, maxIterations, provider,
                            messageCallback, cancellationToken) ?? new List<Message>();

                        // Sync final task state
                        SaveTasksToFile();
                        UpdateFeatureStatus();

                        // Return null for Kobold since parallel execution manages its own Kobolds
                        return (messages, kobold); // Kobold reference is stale but kept for API compatibility
                    }
                    else if (_useEnhancedExecution)
                    {
                        // Sequential enhanced execution (existing behavior)
                        messages = await kobold.StartWorkingWithPlanEnhancedAsync(
                            _planService,
                            maxIterations,
                            _allowPlanModifications,
                            _autoApproveModifications,
                            cancellationToken);
                    }
                    else
                    {
                        // Basic plan execution
                        messages = await kobold.StartWorkingWithPlanAsync(_planService, maxIterations, cancellationToken);
                    }
                }
                else
                {
                    messages = await StartKoboldWorkAsync(kobold.Id, maxIterations);
                }

                // Check Kobold's final state
                var ending = RunEndMessage(kobold, task.Id, projectInfo, taskPreview, IsTaskParked(task.Id),
                    cancellationToken.IsCancellationRequested);
                if (ending is { } end)
                    messageCallback?.Invoke(end.Level, end.Text);
            }
            catch (Exception ex)
            {
                messageCallback?.Invoke("error", $"✗ Kobold {kobold.Id.ToString()[..8]} execution error: {ex.Message}");
                messages = new List<Message>();

                // Sync status from Kobold (it should have captured the error)
                await SyncTaskFromKoboldAsync(kobold);
                // A failed run keeps what it wrote (TASK-168)
                await ReleaseWorktreeAsync(task, worktreePath);
                throw;
            }
            finally
            {
                // Always unsummon the Kobold after execution completes (success, failure, or exception).
                // This frees the parallel slot immediately instead of waiting for monitoring cleanup.
                if (!koboldRemovedByParallel)
                {
                    UnsummonKobold(kobold.Id);
                    _logger?.LogDebug("Unsummoned Kobold {KoboldId} after task {TaskId} execution",
                        kobold.Id.ToString()[..8], task.Id.ToString()[..8]);
                }

                EndRun(task.Id, runCancellation);

                // Note: worktree cleanup happens after SyncTaskFromKoboldAsync below
                // so commit can still use the worktree path
            }

            // Final sync of task status from Kobold (commits happen here if task completed)
            await SyncTaskFromKoboldAsync(kobold, worktreePath);

            // After the commit (if any): remove the worktree only when nothing in it would be lost
            await ReleaseWorktreeAsync(task, worktreePath);

            // Update feature status if Wyvern is available
            UpdateFeatureStatus();

            return (messages, kobold);
        }

        /// <summary>
        /// Phase 4: Executes a task using parallel step execution.
        /// Spawns multiple Kobolds to execute independent steps concurrently.
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <param name="plan">Implementation plan with steps to execute</param>
        /// <param name="analyzer">Step dependency analyzer for grouping steps</param>
        /// <param name="agentType">Type of agent to use</param>
        /// <param name="maxIterations">Maximum iterations per Kobold</param>
        /// <param name="provider">LLM provider (optional)</param>
        /// <param name="messageCallback">Callback for progress messages</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>All messages from parallel execution, or null if setup failed</returns>
        internal async Task<List<Message>?> ExecuteTaskParallelAsync(
            TaskRecord task,
            KoboldImplementationPlan plan,
            Birko.AI.Orchestration.Services.StepDependencyAnalyzer analyzer,
            string agentType,
            int maxIterations,
            string? provider,
            Action<string, string>? messageCallback,
            CancellationToken cancellationToken)
        {
            var allMessages = new List<Message>();
            var parallelGroups = analyzer.AnalyzeParallelGroups(plan);
            var maxParallel = _planningConfig?.MaxParallelSteps ?? 3;

            messageCallback?.Invoke("info", $"📊 Plan analyzed into {parallelGroups.Count} parallel group(s)");

            for (int groupIndex = 0; groupIndex < parallelGroups.Count; groupIndex++)
            {
                var group = parallelGroups[groupIndex];
                var pendingSteps = group.Where(s => s.Status == StepStatus.Pending).ToList();

                if (pendingSteps.Count == 0)
                {
                    messageCallback?.Invoke("info", $"Group {groupIndex + 1}: All steps already completed");
                    continue;
                }

                messageCallback?.Invoke("info",
                    $"Group {groupIndex + 1}/{parallelGroups.Count}: " +
                    $"Executing {pendingSteps.Count} step(s)" +
                    (pendingSteps.Count > 1 ? " in parallel" : ""));

                // Split steps across available Kobolds
                var stepBatches = SplitStepsForParallelExecution(pendingSteps, maxParallel);

                // Create Kobolds for each batch
                var koboldTasks = new List<Task<(List<Message> messages, Kobold kobold)?>>();
                var semaphore = new SemaphoreSlim(maxParallel);

                foreach (var batch in stepBatches)
                {
                    // A run stopped meanwhile (a park cancelled it, or a shutdown) starts no further batch, and that
                    // is not a failure; a question parks without cancelling, so the park itself is checked (D10)
                    try
                    {
                        await semaphore.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    if (IsTaskParked(task.Id))
                    {
                        semaphore.Release();
                        break;
                    }
                    koboldTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            return StepBatchRunner != null
                                ? await StepBatchRunner(task, batch, cancellationToken)
                                : await ExecuteStepBatchAsync(
                                    task, batch, agentType, maxIterations, provider,
                                    messageCallback, cancellationToken);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                // Wait for all batches in this group to complete
                var batchResults = await Task.WhenAll(koboldTasks);

                // Collect messages and check for failures
                var groupFailed = false;
                foreach (var result in batchResults)
                {
                    if (result.HasValue)
                    {
                        allMessages.AddRange(result.Value.messages);
                        if (result.Value.kobold.HasError)
                        {
                            groupFailed = true;
                        }
                    }
                }

                // Reload the plan the batches merged their steps into
                if (_planService != null && !string.IsNullOrEmpty(plan.ProjectId) && !string.IsNullOrEmpty(plan.TaskId))
                {
                    plan = await _planService.LoadPlanAsync(plan.ProjectId!, plan.TaskId) ?? plan;
                }

                // If any step failed, skip remaining groups
                if (groupFailed)
                {
                    messageCallback?.Invoke("error", "Group failed - stopping parallel execution");
                    break;
                }

                // The task waits on a person, or its run was stopped: no further group starts (TASK-171, D10)
                if (IsTaskParked(task.Id) || cancellationToken.IsCancellationRequested)
                {
                    messageCallback?.Invoke("info", IsTaskParked(task.Id)
                        ? "⏸️ Task waits on a person's decision - parallel execution stopped"
                        : "⏹️ Run stopped - parallel execution ended");
                    break;
                }
            }

            return allMessages;
        }

        /// <summary>Tests only: runs a parallel batch in place of summoning a Kobold for it.</summary>
        internal Func<TaskRecord, List<ImplementationStep>, CancellationToken, Task<(List<Message> messages, Kobold kobold)?>>? StepBatchRunner { get; set; }

        /// <summary>
        /// Phase 4: Splits steps into batches for parallel execution.
        /// </summary>
        /// <param name="steps">Steps to split</param>
        /// <param name="maxParallel">Maximum number of parallel batches</param>
        /// <returns>List of step batches</returns>
        private List<List<ImplementationStep>> SplitStepsForParallelExecution(
            List<ImplementationStep> steps, int maxParallel)
        {
            var batches = new List<List<ImplementationStep>>();
            var batchSize = Math.Max(1, (int)Math.Ceiling((double)steps.Count / maxParallel));

            for (int i = 0; i < steps.Count; i += batchSize)
            {
                var batch = steps.Skip(i).Take(batchSize).ToList();
                batches.Add(batch);
            }

            return batches;
        }

        /// <summary>
        /// Phase 4: Executes a batch of steps using a dedicated Kobold.
        /// </summary>
        /// <param name="task">Parent task</param>
        /// <param name="batch">Steps to execute in this batch</param>
        /// <param name="agentType">Type of agent to use</param>
        /// <param name="maxIterations">Maximum iterations</param>
        /// <param name="provider">LLM provider (optional)</param>
        /// <param name="messageCallback">Callback for progress messages</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Messages from execution and the Kobold</returns>
        private async Task<(List<Message> messages, Kobold kobold)?> ExecuteStepBatchAsync(
            TaskRecord task,
            List<ImplementationStep> batch,
            string agentType,
            int maxIterations,
            string? provider,
            Action<string, string>? messageCallback,
            CancellationToken cancellationToken)
        {
            // Summon Kobold
            var kobold = await SummonKoboldAsync(task, agentType, provider);
            if (kobold == null) return null;

            // Set assigned step indices (convert to 0-based)
            var indices = batch.Select(s => s.Index - 1).ToList();
            kobold.SetAssignedStepIndices(indices);

            // Load shared plan using task ID
            if (_planService != null && _plannerAgent != null)
            {
                var projectId = task.ProjectId ?? _projectId ?? string.Empty;
                var loadedPlan = await _planService.LoadPlanAsync(projectId, task.Id);
                if (loadedPlan != null)
                {
                    // Use EnsurePlanAsync to properly set the plan on the Kobold
                    kobold.ImplementationPlan = loadedPlan;
                }
            }

            // Set message callback on agent
            if (messageCallback != null)
            {
                kobold.Agent.SetMessageCallback(messageCallback);
            }

            messageCallback?.Invoke("info",
                $"  → Kobold {kobold.Id.ToString()[..8]} assigned {batch.Count} step(s): " +
                string.Join(", ", batch.Select(s => $"Step {s.Index}")));

            // Execute
            var messages = await kobold.StartWorkingWithPlanEnhancedAsync(
                _planService, maxIterations, _allowPlanModifications,
                _autoApproveModifications, cancellationToken);

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
                        _taskToKoboldMap.TryRemove(task.Id, out _);
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

            var newlyCompleted = _wyvern.UpdateFeatureStatus(taskStatuses);

            // Notify about newly completed features with branches ready for merge
            if (_onFeatureBranchReady != null)
            {
                var projectName = _projectId ?? "unknown";
                foreach (var (featureName, gitBranch) in newlyCompleted)
                {
                    if (!string.IsNullOrEmpty(gitBranch))
                    {
                        _logger?.LogInformation("Feature '{FeatureName}' completed, branch '{Branch}' ready for merge",
                            featureName, gitBranch);
                        try
                        {
                            _onFeatureBranchReady(projectName, featureName, gitBranch);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error notifying feature branch ready for {Feature}", featureName);
                        }
                    }
                }
            }
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
        /// Uses per-project timeout from configuration if available, otherwise falls back to provided global timeout.
        /// </summary>
        /// <param name="globalTimeout">Global maximum allowed working duration before a Kobold is considered stuck</param>
        /// <returns>List of stuck Kobold info (ID, task ID, working duration)</returns>
        public async Task<List<(Guid KoboldId, string? TaskId, TimeSpan WorkingDuration)>> HandleStuckKoboldsAsync(TimeSpan globalTimeout)
        {
            // Get per-project timeout from projects.json (in seconds), falls back to global timeout
            TimeSpan effectiveTimeout = globalTimeout;
            
            if (_projectRepository != null && !string.IsNullOrEmpty(_projectId))
            {
                try
                {
                    var project = _projectRepository.GetById(_projectId);
                    if (project?.Agents?.Kobold?.Timeout > 0)
                    {
                        effectiveTimeout = TimeSpan.FromSeconds(project.Agents.Kobold.Timeout);
                        _logger?.LogDebug(
                            "Using per-project Kobold timeout: {ProjectTimeout} seconds ({Minutes:F1} minutes) for project {ProjectId}",
                            project.Agents.Kobold.Timeout, effectiveTimeout.TotalMinutes, _projectId);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load per-project timeout for {ProjectId}, using global timeout", _projectId);
                }
            }
            
            var stuckKobolds = _koboldFactory.GetStuckKobolds(effectiveTimeout);
            var result = new List<(Guid KoboldId, string? TaskId, TimeSpan WorkingDuration)>();

            foreach (var (kobold, workingDuration) in stuckKobolds)
            {
                // A parked task's lingering Kobold is not timed out and failed: the task waits on its decision
                if (kobold.TaskId.HasValue && IsTaskParked(kobold.TaskId.Value.ToString()))
                    continue;

                var taskPreview = kobold.TaskDescription?.Length > 60 
                    ? kobold.TaskDescription.Substring(0, 60) + "..." 
                    : kobold.TaskDescription ?? "unknown";
                var projectInfo = kobold.ProjectId ?? _projectId ?? "unknown";
                
                // Calculate time since start for context
                var totalTime = kobold.StartedAt.HasValue 
                    ? DateTime.UtcNow - kobold.StartedAt.Value 
                    : TimeSpan.Zero;
                
                var lastActivityTime = kobold.LastLlmResponseAt ?? kobold.StartedAt;
                var idleTimeInfo = lastActivityTime.HasValue 
                    ? $"{workingDuration.TotalMinutes:F1} minutes since last LLM response"
                    : "unknown idle time";
                
                _logger?.LogWarning(
                    "⚠️ Stuck Kobold detected\n" +
                    "  Project: {ProjectId}\n" +
                    "  Kobold ID: {KoboldId}\n" +
                    "  Task ID: {TaskId}\n" +
                    "  Total time: {TotalTime:F1} minutes\n" +
                    "  Idle time: {IdleTime}\n" +
                    "  Task: {TaskDescription}",
                    projectInfo,
                    kobold.Id.ToString()[..8],
                    kobold.TaskId?.ToString()[..8] ?? "unknown",
                    totalTime.TotalMinutes,
                    idleTimeInfo,
                    taskPreview);

                // Mark the Kobold as stuck (transitions to Done with error)
                kobold.MarkAsStuck(workingDuration, effectiveTimeout);

                // Save plan progress so it can be resumed on retry
                // IMPORTANT: Don't mark plan as Failed if there's partial progress - keep it InProgress for resumption
                if (kobold.ImplementationPlan != null && _planService != null && !string.IsNullOrEmpty(kobold.ProjectId))
                {
                    var completedSteps = kobold.ImplementationPlan.CompletedStepsCount;
                    var totalSteps = kobold.ImplementationPlan.Steps.Count;

                    // Add timeout log entry
                    kobold.ImplementationPlan.AddLogEntry(
                        $"⏱️ Kobold timed out after being idle for {workingDuration.TotalMinutes:F1} minutes (no LLM response). " +
                        $"Progress: {completedSteps}/{totalSteps} steps completed. " +
                        $"Plan saved for resumption on retry.");

                    // Keep plan InProgress (not Failed) if there's partial progress
                    // This allows EnsurePlanAsync to reuse the plan on retry
                    if (completedSteps > 0 && kobold.ImplementationPlan.Status == PlanStatus.InProgress)
                    {
                        // Status stays InProgress - don't mark as Failed
                        _logger?.LogInformation(
                            "📋 Preserving plan progress for retry: {CompletedSteps}/{TotalSteps} steps completed",
                            completedSteps, totalSteps);
                    }

                    try
                    {
                        await _planService.SavePlanAsync(kobold.ImplementationPlan);
                        _logger?.LogDebug("💾 Saved plan progress for timed-out Kobold {KoboldId}", kobold.Id.ToString()[..8]);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to save plan progress for timed-out Kobold {KoboldId}", kobold.Id.ToString()[..8]);
                    }
                }

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
        /// Detects and handles Kobolds that have been working longer than the specified timeout (synchronous version).
        /// Note: Prefer HandleStuckKoboldsAsync() for non-blocking operation.
        /// </summary>
        /// <param name="globalTimeout">Global maximum allowed working duration before a Kobold is considered stuck</param>
        /// <returns>List of stuck Kobold info (ID, task ID, working duration)</returns>
        public List<(Guid KoboldId, string? TaskId, TimeSpan WorkingDuration)> HandleStuckKobolds(TimeSpan globalTimeout)
        {
            return HandleStuckKoboldsAsync(globalTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
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

        #region Parked tasks (TASK-019)

        /// <summary>
        /// Parks a task on a pending decision: the decision is stored first, as the source of truth, then the task
        /// moves to <see cref="TaskStatus.AwaitingHumanDecision"/>, its running Kobold's run is cancelled (unless
        /// <paramref name="cancelRun"/> is false: a question's run stops itself at its next turn boundary), and the
        /// decision card is raised. Returns false, changing nothing, when no decision store is configured (a non-SQL
        /// backend) or the task is already parked, so the caller keeps the automatic path.
        /// </summary>
        public async Task<bool> ParkTaskAsync(TaskRecord task, PendingDecision decision, bool cancelRun = true)
        {
            if (_pendingDecisions == null)
            {
                _logger?.LogWarning("Cannot park task {TaskId}: no pending-decision store is configured", task.Id[..Math.Min(8, task.Id.Length)]);
                return false;
            }

            await _parkLock.WaitAsync();
            try
            {
                return await ParkTaskLockedAsync(task, decision, cancelRun);
            }
            finally
            {
                _parkLock.Release();
            }
        }

        private async Task<bool> ParkTaskLockedAsync(TaskRecord task, PendingDecision decision, bool cancelRun)
        {

            if (task.Status == TaskStatus.AwaitingHumanDecision)
            {
                _logger?.LogWarning("Task {TaskId} is already parked", task.Id[..Math.Min(8, task.Id.Length)]);
                return false;
            }

            // Stored under this Drake's project, never the caller's: another project's Drakes would expire it
            decision.TaskId = task.Id;
            decision.ProjectId = _projectId ?? task.ProjectId ?? "";
            await _pendingDecisions!.CreateAsync(decision);

            await UpdateTaskWithWalAsync(task, TaskStatus.AwaitingHumanDecision, task.AssignedAgent);
            _taskToKoboldMap.TryRemove(task.Id, out _);
            SaveTasksToFile();

            // Parked first, then cancelled: a run finishing meanwhile finds the task parked and cannot overwrite it.
            // A question defers the cancel to the run's next turn boundary (cancelRun: false), so the turn that asked
            // reaches the saved conversation.
            if (cancelRun)
                CancelRun(task.Id);

            RaiseDecisionCallback(_onDecisionRequired, decision);
            _logger?.LogInformation("Task {TaskId} parked on decision {DecisionId} ({Timeout})",
                task.Id[..Math.Min(8, task.Id.Length)], decision.Id[..8],
                decision.Timeout is { } t ? $"times out in {t}" : "no timeout");
            return true;
        }

        /// <summary>The question as the person sees it: with the note the levels it passed left (TASK-206) after its context.</summary>
        internal static AskHumanQuestion WithNote(AskHumanQuestion question, string? note) =>
            string.IsNullOrWhiteSpace(note)
                ? question
                : question with { Context = string.IsNullOrWhiteSpace(question.Context) ? note : $"{question.Context} {note}" };

        /// <summary>Whether this Drake's tracker holds the task — a project has one Drake per task area (TASK-169).</summary>
        public bool HoldsTask(string taskId) => _taskTracker.GetTaskById(taskId) != null;

        /// <summary>True while the task waits on a pending decision.</summary>
        public bool IsTaskParked(string taskId) =>
            _taskTracker.GetTaskById(taskId)?.Status == TaskStatus.AwaitingHumanDecision;

        /// <summary>
        /// Makes this Drake's task statuses agree with the stored decisions, which win: a task with an unresolved
        /// decision is parked, and a parked task without one returns to Unassigned. Runs on every reload from the task
        /// file, whose saves are debounced and so can lag behind a park.
        /// </summary>
        public async Task ReconcilePendingDecisionsAsync()
        {
            if (_pendingDecisions == null || string.IsNullOrEmpty(_projectId))
                return;

            HashSet<string> unresolved;
            try
            {
                unresolved = (await _pendingDecisions.GetUnresolvedForProjectAsync(_projectId))
                    .Select(d => d.TaskId).ToHashSet();
            }
            catch (Exception ex)
            {
                // The reload and orphan recovery go on without it; the next cycle tries again
                _logger?.LogWarning(ex, "Could not read pending decisions for {ProjectId}", _projectId);
                return;
            }

            var changed = false;
            foreach (var task in _taskTracker.GetAllTasks())
            {
                var shouldBeParked = unresolved.Contains(task.Id);
                if (shouldBeParked && task.Status is TaskStatus.Done or TaskStatus.Failed)
                {
                    _logger?.LogWarning("Task {TaskId} is {Status} but has an unresolved decision; left as it is",
                        task.Id[..Math.Min(8, task.Id.Length)], task.Status);
                }
                else if (shouldBeParked && task.Status != TaskStatus.AwaitingHumanDecision)
                {
                    await UpdateTaskWithWalAsync(task, TaskStatus.AwaitingHumanDecision, task.AssignedAgent);
                    _taskToKoboldMap.TryRemove(task.Id, out _);
                    changed = true;
                }
                else if (!shouldBeParked && task.Status == TaskStatus.AwaitingHumanDecision)
                {
                    // Answered while no Drake held the task: "Handle it automatically" still runs its automatic route,
                    // and a review is applied rather than rerun
                    var answered = await LatestResolvedAsync(task.Id);
                    if (answered is { Source: PendingDecisionSource.Approval, ResolutionKind: PendingDecisionResolution.Answered })
                    {
                        await ApplyReviewAsync(task, answered);
                        changed = true;
                        continue;
                    }
                    if (answered is { ResolutionKind: PendingDecisionResolution.Answered }
                        && answered.Resolution == PendingDecisionNotification.HandleAutomatically)
                        await RunAutomaticRouteAsync(answered);
                    await UnparkAsync(task);
                    changed = true;
                }
            }

            if (changed)
                SaveTasksToFile();
        }

        /// <summary>
        /// Falls back on every decision of this Drake's tasks whose timeout passed by <paramref name="now"/>
        /// (FEATURE-027 D4): the decision is recorded as timed out, an escalation behind it goes through the automatic
        /// path it was parked instead of while the task is still parked (so no Kobold picks it up meanwhile), and the
        /// task then returns to Unassigned to resume from its saved plan. Returns how many expired.
        /// </summary>
        public async Task<int> ExpirePendingDecisionsAsync(DateTime now)
        {
            if (_pendingDecisions == null || string.IsNullOrEmpty(_projectId))
                return 0;

            var expired = 0;
            foreach (var decision in await _pendingDecisions.GetExpiredAsync(now))
            {
                // One Drake per area: a decision belongs to the Drake whose tracker holds its task
                var task = _taskTracker.GetTaskById(decision.TaskId);
                if (decision.ProjectId != _projectId || task == null)
                    continue;
                if (!await _pendingDecisions.ResolveAsync(decision.Id, PendingDecisionResolution.TimedOut, null, now))
                    continue;

                expired++;
                _logger?.LogInformation("Decision {DecisionId} for task {TaskId} timed out; falling back",
                    decision.Id[..8], decision.TaskId[..Math.Min(8, decision.TaskId.Length)]);

                await RunAutomaticRouteAsync(decision);
                await UnparkAsync(task);
                decision.ResolutionKind = PendingDecisionResolution.TimedOut;
                decision.ResolvedAt = now;
                RaiseDecisionCallback(_onDecisionResolved, decision);
            }

            return expired;
        }

        /// <summary>
        /// A person answers a pending decision (TASK-020). "Handle it automatically" on an escalation runs its automatic
        /// route, as a timeout would; any other answer is recorded for the agent (TASK-022) and the task resumes from its
        /// saved plan. Otherwise: <see cref="DecisionAnswerResult.NotFound"/> for an unknown decision or one of another
        /// project or area, <see cref="DecisionAnswerResult.AlreadyResolved"/> when answered or timed out, and
        /// <see cref="DecisionAnswerResult.InvalidAnswer"/> for an answer its question does not accept (left open).
        /// </summary>
        public async Task<DecisionAnswerResult> ResolvePendingDecisionAsync(string decisionId, string answer)
        {
            if (_pendingDecisions == null)
                return DecisionAnswerResult.NotFound;

            var decision = await _pendingDecisions.GetByIdAsync(decisionId);
            if (decision == null || decision.ProjectId != _projectId)
                return DecisionAnswerResult.NotFound;
            var task = _taskTracker.GetTaskById(decision.TaskId);
            if (task == null)
                return DecisionAnswerResult.NotFound;
            if (decision.IsResolved)
                return DecisionAnswerResult.AlreadyResolved;
            if (!PendingDecisionNotification.AcceptsAnswer(decision, answer, out answer))
                return DecisionAnswerResult.InvalidAnswer;

            var at = DateTime.UtcNow;
            if (!await _pendingDecisions.ResolveAsync(decision.Id, PendingDecisionResolution.Answered, answer, at))
                return DecisionAnswerResult.AlreadyResolved;
            decision.Resolution = answer;
            decision.ResolutionKind = PendingDecisionResolution.Answered;
            decision.ResolvedAt = at;

            if (decision.Source == PendingDecisionSource.Approval)
            {
                await ApplyReviewAsync(task, decision);
                RaiseDecisionCallback(_onDecisionResolved, decision);
                return DecisionAnswerResult.Resolved;
            }

            if (answer == PendingDecisionNotification.HandleAutomatically && decision.Escalation != null)
            {
                await RunAutomaticRouteAsync(decision);
            }
            else
            {
                if (decision.Escalation is { } alert)
                {
                    alert.Status = EscalationStatus.Resolved;
                    alert.Resolution = $"Human: {answer}";
                    alert.ResolvedAt = at;
                }
                await RecordAnswerOnPlanAsync(decision, answer);
            }

            await UnparkAsync(task);
            RaiseDecisionCallback(_onDecisionResolved, decision);
            _logger?.LogInformation("Decision {DecisionId} for task {TaskId} answered",
                decision.Id[..8], decision.TaskId[..Math.Min(8, decision.TaskId.Length)]);
            return DecisionAnswerResult.Resolved;
        }

        /// <summary>
        /// A Kobold asks a person (TASK-021, FEATURE-028): parks the task on the question, leaving the run to stop at its
        /// next turn boundary. Refused when the project's policy disallows asking, the task has reached its question
        /// cap (every question it ever asked counts), there is no decision store, or the task is already parked. A
        /// failing policy is read as allow, no cap. A question has no timeout until TASK-023 supplies one.
        /// </summary>
        /// <summary>
        /// The question chain a Kobold's <c>ask_human</c> climbs before it reaches a person (TASK-202, FEATURE-078 D23); the
        /// factory hands each Drake its own. With no level registered, every question reaches the person.
        /// </summary>
        public QuestionChain QuestionChain { get; set; } = new();

        /// <summary>
        /// The checkers a finished task goes through after the acceptance check's own checks (TASK-217, FEATURE-078 D8,
        /// D30); the factory hands each Drake its own. With none registered, a task is judged as before.
        /// </summary>
        public Services.Checks.TaskCheckRunner TaskChecks { get; set; } = new();

        public async Task<AskHumanOutcome> AskHumanAsync(TaskRecord task, AskHumanQuestion question)
        {
            if (_pendingDecisions == null)
                return new AskHumanOutcome(false, null, "asking a person is not available on this server");

            AskHumanLimits limits;
            try
            {
                limits = _askHumanPolicy.For(_projectId ?? task.ProjectId ?? "");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Ask-human policy failed for {ProjectId}; allowing", _projectId);
                limits = new AskHumanLimits();
            }

            if (!limits.Allow)
                return new AskHumanOutcome(false, null, "this project does not let agents ask");
            if (limits.MaxQuestionsPerTask is { } cap
                && await _pendingDecisions.CountForTaskAsync(task.Id, PendingDecisionSource.AskHuman) >= cap)
                return new AskHumanOutcome(false, null, $"the question limit for this task ({cap}) is reached");

            if (IsTaskParked(task.Id))
                return new AskHumanOutcome(false, null, "the task is already waiting on a person");

            // The question climbs the hierarchy first (TASK-202, D23); only one no level answers parks the task for a person
            var projectId = _projectId ?? task.ProjectId ?? "";
            var chainQuestion = new ChainQuestion(projectId, task.Id, QuestionLevel.Worker, question);
            var outcome = await QuestionChain.AskAsync(chainQuestion);
            var decision = new PendingDecision
            {
                Disposition = EscalationDisposition.HumanDecisionRequired,
                Source = PendingDecisionSource.AskHuman,
                Question = WithNote(question, outcome.PersonNote).ToJson(),
                Path = outcome.Path.ToList()
            };

            if (!outcome.ReachesPerson)
            {
                // Answered on the way: recorded answered, the task keeps running, and the levels below the answerer hear it
                decision.ProjectId = projectId;
                decision.TaskId = task.Id;
                decision.Resolution = outcome.Answer!.Answer;
                decision.ResolutionKind = PendingDecisionResolution.Answered;
                decision.ResolvedAt = DateTime.UtcNow;
                // Recorded as the agent's, with its reason, and offered to the person to keep or overturn (TASK-208, D21)
                decision.AnsweredBy = outcome.AnsweredBy;
                decision.AnswerReason = outcome.Answer.Reason;
                decision.AgentAnswer = outcome.Answer.Answer;
                await _pendingDecisions.CreateAsync(decision);
                RaiseDecisionCallback(_onDecisionRequired, decision);
                await QuestionChain.DeliverAnswerAsync(chainQuestion, outcome.Path, decision.Resolution, outcome.AnsweredBy);
                _logger?.LogInformation("ask_human for task {TaskId} answered by the {Level} level: {Reason}",
                    task.Id, outcome.AnsweredBy, outcome.Answer.Reason);
                return new AskHumanOutcome(false, decision.Id, null, decision.Resolution, outcome.AnsweredBy);
            }

            return await ParkTaskAsync(task, decision, cancelRun: false)
                ? new AskHumanOutcome(true, decision.Id, null)
                : new AskHumanOutcome(false, null, "the task is already waiting on a person");
        }

        /// <summary>
        /// The message a sequential run's end is reported with (level, text), or null for none: done, failed, parked,
        /// or ended with steps left. A run whose token was cancelled stopped for that reason (a shutdown), not at its
        /// iteration limit (TASK-191).
        /// </summary>
        internal static (string Level, string Text)? RunEndMessage(Kobold kobold, string taskId, string projectInfo,
            string taskPreview, bool parked, bool cancelled)
        {
            var koboldId = kobold.Id.ToString()[..8];
            var shortTaskId = taskId[..Math.Min(8, taskId.Length)];
            var details = $"  Project: {projectInfo}\n  Task ID: {shortTaskId}\n  Task: {taskPreview}";
            if (kobold.IsSuccess)
                return ("success", $"✓ Kobold {koboldId} completed task successfully\n{details}");
            if (kobold.HasError)
                return ("error", $"✗ Kobold {koboldId} failed: {kobold.ErrorMessage}\n{details}");
            if (parked)
                return ("info", $"⏸️ Kobold {koboldId} stopped: its task waits on a person's decision\n  Task ID: {shortTaskId}");
            if (kobold.Status == KoboldStatus.Working && kobold.ImplementationPlan != null)
            {
                var plan = kobold.ImplementationPlan;
                // Only the caller cancels a run that is not parked (parking is the other canceller): the server stopping
                if (cancelled)
                    return ("info",
                        $"⏹ Kobold {koboldId} stopped: the server is shutting down - work incomplete ({plan.CompletedStepsCount}/{plan.Steps.Count} steps done); its plan is saved and the task resumes on the next run\n{details}");
                return ("warning",
                    $"⚠ Kobold {koboldId} reached iteration limit - work incomplete ({plan.CompletedStepsCount}/{plan.Steps.Count} steps done)\n{details}");
            }
            return null;
        }

        /// <summary>
        /// The person overturned an agent's answer to one of this Drake's tasks (TASK-208, FEATURE-078 D21): the task's
        /// plan is revised through the WrongApproach route with their answer, finished steps kept. A task a Kobold is
        /// running keeps running on its revised plan; any other task (Done included) reopens to the queue, and its Kobold
        /// hears the person's answer as overriding the agent's. A parked task stays parked. False when this Drake does not
        /// hold the task.
        /// </summary>
        public async Task<bool> ApplyOverturnAsync(PendingDecision decision)
        {
            var task = _taskTracker.GetTaskById(decision.TaskId);
            if (task == null)
                return false;

            var running = _runCancellations.ContainsKey(task.Id);
            if (!running && !IsTaskParked(task.Id) && task.Status != TaskStatus.Unassigned)
            {
                var previous = task.Status;
                await UpdateTaskWithWalAsync(task, TaskStatus.Unassigned, task.AssignedAgent);
                await SaveTasksToFileAsync();
                PublishStatusChange(task, previous, $"A person overturned an agent's answer: {decision.Resolution}");
            }
            await RevisePlanForOverturnAsync(task, decision);
            _logger?.LogInformation("Task {TaskId} reopened: a person overturned the {Level} level's answer to decision {DecisionId}",
                task.Id[..Math.Min(8, task.Id.Length)], decision.AnsweredBy, decision.Id[..8]);
            return true;
        }

        /// <summary>Revises the task's plan for each overturned answer not yet revised for (TASK-208).</summary>
        private async Task ReviseOverturnedPlansAsync(TaskRecord task)
        {
            if (_pendingDecisions == null)
                return;
            try
            {
                foreach (var decision in await _pendingDecisions.GetOverturnedUnrevisedForTaskAsync(task.Id))
                    await RevisePlanForOverturnAsync(task, decision);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not revise the plan of task {TaskId} for an overturned answer", task.Id);
            }
        }

        private async Task RevisePlanForOverturnAsync(TaskRecord task, PendingDecision decision)
        {
            var question = PendingDecisionNotification.QuestionText(decision);
            var alert = new EscalationAlert
            {
                ProjectId = _projectId ?? task.ProjectId ?? "",
                TaskId = task.Id,
                AgentType = task.AssignedAgent,
                WorkerId = GetKoboldForTask(task.Id)?.Id.ToString() ?? "",
                Type = EscalationType.WrongApproach,
                Summary = $"A person overturned the answer to \"{question}\": it was \"{decision.AgentAnswer}\" " +
                          $"({AgentAnswerReview.LevelName(decision.AnsweredBy ?? QuestionLevel.Person)}), it is now " +
                          $"\"{decision.Resolution}\". Revise the remaining steps to follow the person's answer, and add " +
                          "steps to redo finished work that relied on the old one."
            };
            try
            {
                await HandleEscalationAsync(alert, EscalationDisposition.AutoResolve);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not revise the plan of task {TaskId} for an overturned answer",
                    task.Id[..Math.Min(8, task.Id.Length)]);
            }
            await _pendingDecisions!.MarkRevisedAsync(decision.Id, DateTime.UtcNow);
        }

        internal void CancelRun(string taskId)
        {
            if (!_runCancellations.TryGetValue(taskId, out var run))
                return;
            try { run.Cancel(); }
            catch (ObjectDisposedException) { } // the run ended between the lookup and the cancel
        }

        /// <summary>Registers the cancellation of a task's current run, so parking the task can stop it.</summary>
        internal void BeginRun(string taskId, CancellationTokenSource run) => _runCancellations[taskId] = run;

        /// <summary>Unregisters a run, only if it is still the task's current one.</summary>
        internal void EndRun(string taskId, CancellationTokenSource run) =>
            _runCancellations.TryRemove(new KeyValuePair<string, CancellationTokenSource>(taskId, run));

        /// <summary>
        /// Parks the task when the escalation's disposition (given, or chosen by the policy) is HumanDecisionRequired.
        /// False sends the escalation down its automatic route: an automatic disposition, a task this Drake does not hold
        /// or that already finished, or no decision store. An escalation for a task already parked is absorbed (true).
        /// </summary>
        private async Task<bool> TryRouteToHumanAsync(EscalationAlert alert, EscalationDisposition? disposition)
        {
            var task = string.IsNullOrEmpty(alert.TaskId) ? null : _taskTracker.GetTaskById(alert.TaskId);

            // A parked task waits on its person: a late escalation must not run an automatic route under the open
            // decision. Only an explicit disposition (a timeout, or "Handle it automatically") goes through.
            if (disposition == null && task?.Status == TaskStatus.AwaitingHumanDecision)
            {
                alert.Status = EscalationStatus.Resolved;
                alert.Resolution = "Already awaiting a human decision";
                alert.ResolvedAt = DateTime.UtcNow;
                _logger?.LogInformation("Escalation {AlertId} for parked task {TaskId} absorbed", alert.Id[..8],
                    task.Id[..Math.Min(8, task.Id.Length)]);
                return true;
            }

            var route = disposition is { } given
                ? new EscalationRoute(given)
                : _dispositionPolicy.Choose(alert, task);
            if (route.Disposition != EscalationDisposition.HumanDecisionRequired)
                return false;

            if (task == null || task.Status is TaskStatus.Done or TaskStatus.Failed)
                return false;

            alert.Status = EscalationStatus.InProgress;
            var decision = new PendingDecision
            {
                Disposition = EscalationDisposition.HumanDecisionRequired,
                Source = PendingDecisionSource.Escalation,
                Question = $"{alert.Type}: {alert.Summary}",
                Escalation = alert,
                Timeout = route.Timeout
            };
            if (await ParkTaskAsync(task, decision))
                return true;

            // Another escalation parked it first: this one is absorbed, as for any task already parked
            if (task.Status == TaskStatus.AwaitingHumanDecision)
            {
                alert.Status = EscalationStatus.Resolved;
                alert.Resolution = "Already awaiting a human decision";
                alert.ResolvedAt = DateTime.UtcNow;
                return true;
            }

            alert.Status = EscalationStatus.Pending;
            return false;
        }

        /// <summary>Runs a parked escalation's automatic route while its task is still parked, so no Kobold takes it meanwhile.</summary>
        private async Task RunAutomaticRouteAsync(PendingDecision decision)
        {
            if (decision.Escalation is not { } alert)
                return;

            alert.Status = EscalationStatus.Pending;
            try
            {
                await HandleEscalationAsync(alert, EscalationDisposition.AutoResolve);
                await _pendingDecisions!.RecordEscalationOutcomeAsync(decision.Id, alert);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Automatic route failed for decision {DecisionId}", decision.Id[..8]);
            }
        }

        /// <summary>
        /// The task's answered and timed-out decisions, oldest first, for the Kobold to hear on resume (TASK-022), after
        /// what a person wrote for the tasks it depends on, directly or through others and in any area (TASK-175,
        /// FEATURE-028 D8), so a dependent never asks a person what they already answered. Empty without a store. Null
        /// when the store cannot be read: a run without a person's answer would guess or ask again, so the caller holds
        /// the task for a later cycle instead.
        /// </summary>
        /// <summary>The project's constraints and exclusions, from Wyrm's recommendation and Wyvern's analysis, without repeats.</summary>
        private List<string> CombinedConstraints()
        {
            var combined = new List<string>();
            if (_wyrmRecommendation?.Constraints?.Any() == true)
                combined.AddRange(_wyrmRecommendation.Constraints);
            if (_wyvern?.Analysis?.Constraints?.Any() == true)
                combined.AddRange(_wyvern.Analysis.Constraints.Where(c => !combined.Contains(c)));
            if (_wyrmRecommendation?.OutOfScope?.Any() == true)
                combined.AddRange(_wyrmRecommendation.OutOfScope.Select(o => $"OUT OF SCOPE: {o}"));
            if (_wyvern?.Analysis?.OutOfScope?.Any() == true)
                combined.AddRange(_wyvern.Analysis.OutOfScope
                    .Where(o => !combined.Any(c => c.EndsWith(o)))
                    .Select(o => $"OUT OF SCOPE: {o}"));
            return combined;
        }

        /// <summary>
        /// What this Drake owns for a task, as the supervisor answers a worker's question from it (TASK-203, FEATURE-078
        /// D18): the task's description, its plan, the project's constraints, and the answers it and the tasks it depends on
        /// already have. Null when this Drake does not hold the task, or its answers cannot be read.
        /// </summary>
        public async Task<Services.SupervisorSources?> SupervisorSourcesAsync(string taskId)
        {
            var task = _taskTracker.GetTaskById(taskId);
            if (task == null)
                return null;
            var answers = await LoadHumanAnswersAsync(taskId);
            if (answers == null)
                return null;

            string? plan = null;
            if (_planService != null)
            {
                try
                {
                    var saved = await _planService.LoadPlanAsync(_projectId ?? task.ProjectId ?? "", taskId);
                    if (saved?.Steps.Count > 0)
                        plan = string.Join(Environment.NewLine, saved.Steps.Select(s =>
                            $"{s.Index}. {s.Title}" + (string.IsNullOrWhiteSpace(s.Description) ? "" : $" — {s.Description}")));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not read the plan of task {TaskId} for the supervisor", taskId);
                }
            }

            var (description, analysisConstraints) = AnalysisFacts(task);
            var constraints = CombinedConstraints();
            constraints.AddRange(analysisConstraints.Where(c => !constraints.Contains(c)));
            var taskText = description == null ? task.Task : $"{task.Task}{Environment.NewLine}{description}";

            return new Services.SupervisorSources(taskText, plan, constraints,
                answers.Where(a => !a.TimedOut && !string.IsNullOrWhiteSpace(a.Answer)).Select(a => (a.Question, a.Answer!)).ToList());
        }

        /// <summary>
        /// The task's full description and the project's constraints as Wyvern and Wyrm saved them in the project folder
        /// (<c>analysis.json</c>, <c>wyrm-recommendation.json</c>, beside <c>tasks/</c>): the task record keeps only the
        /// task's title, and this Drake is not always given the Wyvern in memory. The task is found by the
        /// <c>[area-n]</c> id its title starts with. Unreadable files give nothing.
        /// </summary>
        private (string? Description, List<string> Constraints) AnalysisFacts(TaskRecord task)
        {
            var constraints = new List<string>();
            string? description = null;
            var id = System.Text.RegularExpressions.Regex.Match(task.Task, @"^\[([^\]]+)\]").Groups[1].Value;

            // The project's Wyvern, when the factory gave this Drake one (TASK-225), already holds the analysis; its
            // constraints come through CombinedConstraints, so only the description is taken here
            if (_wyvern?.Analysis is { } loaded)
            {
                if (id.Length > 0)
                    description = loaded.Areas.SelectMany(a => a.Tasks).FirstOrDefault(t => t.Id == id)?.Description;
                return (description, constraints);
            }

            var folder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(_outputMarkdownPath)));
            if (folder == null)
                return (null, constraints);
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            try
            {
                var analysisPath = Path.Combine(folder, "analysis.json");
                if (File.Exists(analysisPath)
                    && System.Text.Json.JsonSerializer.Deserialize<WyvernAnalysis>(File.ReadAllText(analysisPath), options) is { } analysis)
                {
                    if (id.Length > 0)
                        description = analysis.Areas.SelectMany(a => a.Tasks).FirstOrDefault(t => t.Id == id)?.Description;
                    constraints.AddRange(analysis.Constraints);
                    constraints.AddRange(analysis.OutOfScope.Select(o => $"OUT OF SCOPE: {o}"));
                }
                var wyrmPath = Path.Combine(folder, "wyrm-recommendation.json");
                if (File.Exists(wyrmPath)
                    && System.Text.Json.JsonSerializer.Deserialize<WyrmRecommendation>(File.ReadAllText(wyrmPath), options) is { } wyrm)
                {
                    constraints.AddRange(wyrm.Constraints.Where(c => !constraints.Contains(c)));
                    constraints.AddRange(wyrm.OutOfScope.Select(o => $"OUT OF SCOPE: {o}").Where(c => !constraints.Contains(c)));
                }
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                _logger?.LogWarning(ex, "Could not read the analysis for task {TaskId}'s supervisor sources", task.Id);
            }
            return (description, constraints);
        }

        internal async Task<IReadOnlyList<HumanAnswer>?> LoadHumanAnswersAsync(string taskId)
        {
            if (_pendingDecisions == null)
                return Array.Empty<HumanAnswer>();
            try
            {
                var inherited = new List<(DateTime At, HumanAnswer Answer)>();
                foreach (var ancestor in TasksDependedOn(taskId))
                {
                    foreach (var decision in await _pendingDecisions.GetResolvedForTaskAsync(ancestor.Id))
                    {
                        if (HumanAnswer.CarriesPersonsWords(decision))
                            inherited.Add((decision.ResolvedAt ?? decision.CreatedAt, HumanAnswer.From(decision, TaskLabel(ancestor))));
                    }
                }
                if (inherited.Count > 0)
                    _logger?.LogInformation("Task {TaskId} hears {Count} answer(s) a person gave the tasks it depends on: decision(s) {Decisions}",
                        taskId, inherited.Count, string.Join(", ", inherited.Select(i => i.Answer.DecisionId[..8])));
                var own = (await _pendingDecisions.GetResolvedForTaskAsync(taskId)).AsEnumerable().Reverse().Select(d => HumanAnswer.From(d));
                return inherited.OrderBy(i => i.At).Select(i => i.Answer).Concat(own).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not read the answered decisions of task {TaskId}; not summoning a Kobold for it", taskId);
                return null;
            }
        }

        /// <summary>
        /// Every task <paramref name="taskId"/> depends on, directly or through others, across the project's areas. A
        /// dependency names a task by the bracketed id in its text, as the readiness check reads it.
        /// </summary>
        private List<TaskRecord> TasksDependedOn(string taskId)
        {
            var own = _taskTracker.GetAllTasks();
            var task = own.FirstOrDefault(t => t.Id == taskId);
            if (task == null || DependenciesOf(task).Count == 0)
                return new List<TaskRecord>();

            var all = own.Concat(LoadTasksFromOtherAreas()).ToList();
            var found = new List<TaskRecord>();
            var seen = new HashSet<string> { taskId };
            var pending = new Queue<TaskRecord>([task]);
            while (pending.Count > 0)
            {
                foreach (var dep in DependenciesOf(pending.Dequeue()))
                {
                    foreach (var match in all.Where(t => TaskTags.Is(t.Task, dep)))
                    {
                        if (!seen.Add(match.Id))
                            continue;
                        found.Add(match);
                        pending.Enqueue(match);
                    }
                }
            }
            return found;
        }

        private static List<string> DependenciesOf(TaskRecord task) =>
            task.Dependencies.Count > 0 ? task.Dependencies : ParseDependenciesFromDescription(task.Task);

        private static readonly char[] LineBreaks = ['\r', '\n'];

        /// <summary>A task as another task hears it named: its first line, cut short.</summary>
        private static string TaskLabel(TaskRecord task)
        {
            var line = task.Task.Split(LineBreaks, 2)[0].Trim();
            return line.Length <= 120 ? line : line[..117] + "...";
        }

        /// <summary>The tasks of the project's other areas, read from their task files; one that cannot be read is skipped.</summary>
        private List<TaskRecord> LoadTasksFromOtherAreas()
        {
            var taskDir = Path.GetDirectoryName(_outputMarkdownPath);
            var tasks = new List<TaskRecord>();
            if (string.IsNullOrEmpty(taskDir) || !Directory.Exists(taskDir))
                return tasks;
            foreach (var taskFile in Directory.GetFiles(taskDir, "*-tasks.md"))
            {
                if (Path.GetFullPath(taskFile) == Path.GetFullPath(_outputMarkdownPath))
                    continue;
                try
                {
                    var tracker = new TaskTracker();
                    tracker.LoadFromFile(taskFile);
                    tasks.AddRange(tracker.GetAllTasks());
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load tasks from {TaskFile} for the answers its tasks pass on", taskFile);
                }
            }
            return tasks;
        }

        /// <summary>Returns a still-parked task to the queue; the automatic route may already have moved it on.</summary>
        private async Task UnparkAsync(TaskRecord task)
        {
            if (task.Status != TaskStatus.AwaitingHumanDecision)
                return;
            await UpdateTaskWithWalAsync(task, TaskStatus.Unassigned, task.AssignedAgent);
            SaveTasksToFile();
        }

        /// <summary>
        /// The acceptance check on a finished task (TASK-025): true lets Done through. Otherwise the task has been
        /// reopened, failed, or parked for a person's review, and the Kobold that finished it is let go so its Done is
        /// not synced again. <paramref name="kobold"/> is null for a task recovered without one.
        /// </summary>
        private async Task<bool> PassesAcceptanceAsync(TaskRecord task, Kobold? kobold, string? worktreePath)
        {
            var workspace = kobold?.Agent.Options.WorkingDirectory is { Length: > 0 } w ? w : ResolveWorkspacePath();
            AcceptanceResult result;
            try
            {
                result = string.IsNullOrEmpty(workspace)
                    ? new AcceptanceResult([], ["The task's workspace is unknown, so nothing could be checked."], [])
                    : await _acceptanceService.EvaluateAsync(task, workspace);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Acceptance check could not run for task {TaskId}", task.Id[..Math.Min(8, task.Id.Length)]);
                result = new AcceptanceResult([], [$"The acceptance check could not run: {ex.Message}"], []);
            }

            // Then the checkers, cheapest first (TASK-217, D8, D30), unless the task's own promises already failed it
            if (result.Unmet.Count == 0 && TaskChecks.Checkers.Count > 0 && !string.IsNullOrEmpty(workspace))
                result = await WithCheckersAsync(task, workspace, result);

            if (result.Passed)
            {
                _logger?.LogInformation("Acceptance check passed for task {TaskId} ({Count} checks)",
                    task.Id[..Math.Min(8, task.Id.Length)], result.Checked.Count);
                return true;
            }

            _taskToKoboldMap.TryRemove(task.Id, out _);
            // The early return skips the sync path's unregister; a Kobold left registered would hold its files "in use"
            if (kobold != null && !string.IsNullOrEmpty(_projectId) && _sharedPlanningContext != null)
                _ = _sharedPlanningContext.UnregisterAgentAsync(kobold.Id.ToString(), false, "acceptance check did not pass");
            if (result.Unmet.Count > 0)
            {
                await ReopenOrEscalateAsync(task, result.Unmet, kobold, worktreePath);
                return false;
            }

            await ParkForReviewAsync(task, kobold, worktreePath, result);
            return false;
        }

        /// <summary>
        /// Runs the checkers on a task its own checks did not fail: their findings become unmet promises (the task is
        /// reopened with them), and a checker that could not run goes to a person, never counting as a pass.
        /// </summary>
        private async Task<AcceptanceResult> WithCheckersAsync(TaskRecord task, string workspace, AcceptanceResult own)
        {
            Services.Checks.TaskCheckReport report;
            try
            {
                var projectId = _projectId ?? task.ProjectId ?? "";
                var plan = _planService == null || projectId.Length == 0 ? null : await _planService.LoadPlanAsync(projectId, task.Id);
                var planned = plan?.Steps.SelectMany(s => s.FilesToCreate.Concat(s.FilesToModify)).Distinct().ToList() ?? [];
                var answers = _pendingDecisions == null ? [] : (await _pendingDecisions.GetResolvedForTaskAsync(task.Id))
                    .Where(d => d.ResolutionKind == PendingDecisionResolution.Answered && !string.IsNullOrWhiteSpace(d.Resolution)
                        && d.Source == PendingDecisionSource.AskHuman)
                    .Select(d => $"{PendingDecisionNotification.QuestionText(d)} → {d.Resolution}")
                    .ToList();
                report = await TaskChecks.RunAsync(new Services.Checks.TaskCheckInput(task, workspace, projectId, planned, answers));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "The checkers could not run for task {TaskId}", task.Id[..Math.Min(8, task.Id.Length)]);
                return own with { NeedsHuman = [.. own.NeedsHuman, $"The checks could not run: {ex.Message}"] };
            }

            _logger?.LogInformation("Checkers on task {TaskId}: ran {Ran}; {Findings} finding(s), {Failed} could not run",
                task.Id[..Math.Min(8, task.Id.Length)], string.Join(", ", report.Ran), report.Findings.Count, report.CouldNotRun.Count);
            return new AcceptanceResult(
                report.Findings.Select(f => f.ToString()).ToList(),
                [.. own.NeedsHuman, .. report.CouldNotRun],
                [.. own.Checked, .. report.Ran.Where(name => report.Findings.All(f => f.Checker != name)).Select(name => $"The {name} check passed")]);
        }

        /// <summary>
        /// What no machine could judge goes to a person, with no fallback (FEATURE-031 D4): the work is committed, the
        /// task waits on an Approve / Reject card that never times out, and its feature is not offered for merging,
        /// since that waits for every task to be Done. Without a decision store the task fails rather than pass unchecked.
        /// </summary>
        private async Task ParkForReviewAsync(TaskRecord task, Kobold? kobold, string? worktreePath, AcceptanceResult result)
        {
            if (_pendingDecisions == null)
            {
                await FailAcceptanceAsync(task, $"{ErrorClassifier.AcceptanceFailurePrefix}: it needs a person's review " +
                    "and this server keeps no decisions (SQLite backend only)", null);
                return;
            }

            if (kobold != null)
            {
                await CommitTaskCompletionAsync(kobold, task, worktreePath);
                if (task.Status == TaskStatus.Failed)
                    return; // the commit failed and said so; there is nothing reviewed to approve
            }

            var decision = new PendingDecision
            {
                Disposition = EscalationDisposition.HumanDecisionRequired,
                Source = PendingDecisionSource.Approval,
                Question = AcceptanceReview.Question(task, result)
            };
            if (!await ParkTaskAsync(task, decision, cancelRun: false))
                await FailAcceptanceAsync(task, $"{ErrorClassifier.AcceptanceFailurePrefix}: its review card could not be raised", null);
        }

        /// <summary>
        /// A failed check reopens the task with the unmet criteria (FEATURE-031 D3): its plan is revised through the
        /// WrongApproach route, keeping finished steps, and it returns to the queue. At <c>MaxAcceptanceReopens</c> it
        /// escalates to a person instead (TASK-222, FEATURE-078 D9, D29); a person's note (<paramref name="byPerson"/>)
        /// always reopens it, past the limit too.
        /// </summary>
        private async Task ReopenOrEscalateAsync(TaskRecord task, IReadOnlyList<string> unmet, Kobold? kobold,
            string? worktreePath = null, bool byPerson = false)
        {
            var summary = string.Join(" ", unmet);
            var max = _acceptance?.MaxAcceptanceReopens ?? new Models.Configuration.VerificationConfiguration().MaxAcceptanceReopens;
            if (task.AcceptanceAttempts >= max && !byPerson)
            {
                await EscalateAcceptanceAsync(task, unmet, kobold, worktreePath, max);
                return;
            }

            var previous = task.Status;
            task.AcceptanceAttempts++;
            task.Task += $"\n\n{TaskAcceptanceService.ReopenNotePrefix} {task.AcceptanceAttempts}): {summary}";
            var alert = new EscalationAlert
            {
                ProjectId = _projectId ?? task.ProjectId ?? "",
                TaskId = task.Id,
                AgentType = task.AssignedAgent,
                WorkerId = kobold?.Id.ToString() ?? "",
                Type = EscalationType.WrongApproach,
                Summary = $"Acceptance check failed: {summary}"
            };
            try
            {
                await HandleEscalationAsync(alert, EscalationDisposition.AutoResolve);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not revise the plan of task {TaskId} after a failed acceptance check",
                    task.Id[..Math.Min(8, task.Id.Length)]);
            }

            await UpdateTaskWithWalAsync(task, TaskStatus.Unassigned, task.AssignedAgent);
            await SaveTasksToFileAsync();
            PublishStatusChange(task, previous, $"Acceptance check failed (attempt {task.AcceptanceAttempts}): {summary}");
            _logger?.LogWarning("Task {TaskId} reopened by the acceptance check (attempt {Attempt}/{Max}): {Summary}",
                task.Id[..Math.Min(8, task.Id.Length)], task.AcceptanceAttempts, max, summary);
        }

        /// <summary>
        /// A task whose checks failed past the reopen limit goes to a person (TASK-222, FEATURE-078 D9, D29): its work is
        /// committed and it waits on a card listing every attempt's findings, never timing out. Approve marks it Done, a
        /// note sends it back once more, "Handle it automatically" fails it with a fix task. Without a decision store, or
        /// when the card cannot be raised, it fails with the fix task as before.
        /// </summary>
        private async Task EscalateAcceptanceAsync(TaskRecord task, IReadOnlyList<string> unmet, Kobold? kobold,
            string? worktreePath, int max)
        {
            var summary = string.Join(" ", unmet);
            var failure = $"{ErrorClassifier.AcceptanceFailurePrefix} after {max} reopen(s): {summary}";
            if (_pendingDecisions == null)
            {
                await FailAcceptanceAsync(task, failure, unmet);
                return;
            }

            if (kobold != null)
            {
                await CommitTaskCompletionAsync(kobold, task, worktreePath);
                if (task.Status == TaskStatus.Failed)
                    return; // the commit failed and said so
            }

            var decision = new PendingDecision
            {
                Disposition = EscalationDisposition.HumanDecisionRequired,
                Source = PendingDecisionSource.Approval,
                Question = AcceptanceReview.EscalationQuestion(task, unmet),
                // Marks the card as an escalation and keeps what a fix task would name
                Escalation = new EscalationAlert
                {
                    ProjectId = _projectId ?? task.ProjectId ?? "",
                    TaskId = task.Id,
                    AgentType = task.AssignedAgent,
                    WorkerId = kobold?.Id.ToString() ?? "",
                    Type = EscalationType.WrongApproach,
                    Summary = summary
                }
            };
            if (!await ParkTaskAsync(task, decision, cancelRun: false))
            {
                await FailAcceptanceAsync(task, failure, unmet);
                return;
            }
            _logger?.LogWarning("Task {TaskId} failed its checks {Count} times; escalated to a person: {Summary}",
                task.Id[..Math.Min(8, task.Id.Length)], task.AcceptanceAttempts + 1, summary);
        }

        /// <summary>
        /// Files a fix task for <paramref name="failed"/> (TASK-223, FEATURE-078 D14): <paramref name="kind"/> is what failed
        /// (<c>failure</c> when recovery gave up, <c>monitor-…</c> for a repeated monitor alert), <paramref name="what"/> says
        /// how. It takes the failed task's priority and is worked before other work of that priority. Returns null when a fix
        /// of that kind for that task already stands: one not Done (a failed fix still stands), or one Done since the task
        /// last changed, which reopens it at the next pick.
        /// </summary>
        public async Task<TaskRecord?> FileFixTaskAsync(TaskRecord failed, string kind, string what, TaskPriority? priority = null)
        {
            var standing = _taskTracker.GetAllTasks().Any(t => t.FixesTaskId == failed.Id && t.FixKind == kind &&
                (t.Status != TaskStatus.Done || t.UpdatedAt >= failed.UpdatedAt));
            if (standing)
                return null;

            var shortId = failed.Id[..Math.Min(8, failed.Id.Length)];
            var name = TaskToken(failed.Task) is { } token ? $"task {token} ({shortId})" : $"task {shortId}";
            // The failed task's text without its own [token] and dependency clause, so the fix never reads as that task when
            // dependencies are matched by "[token]"
            var description = DependsOnClause.Replace(LeadingToken.Replace(failed.Task, ""), "").Trim();
            var fix = _taskTracker.AddTask(
                $"[fix-{shortId}-{Guid.NewGuid().ToString("N")[..6]}] Fix what made {name} fail ({kind}): {what}\n\n" +
                $"The failed task: {description}",
                priority ?? failed.Priority, dependencies: new List<string>());
            fix.FixesTaskId = failed.Id;
            fix.FixKind = kind;
            fix.ProjectId = failed.ProjectId ?? _projectId;
            await SaveTasksToFileAsync();
            _logger?.LogWarning("Filed fix task {FixId} for task {TaskId} ({Kind}): {What}",
                fix.Id[..Math.Min(8, fix.Id.Length)], shortId, kind, what);
            return fix;
        }

        /// <summary>A fix task for the task with <paramref name="taskId"/> in this Drake, or null when it holds no such task.</summary>
        public Task<TaskRecord?> FileFixTaskAsync(string taskId, string kind, string what) =>
            _taskTracker.GetTaskById(taskId) is { } task ? FileFixTaskAsync(task, kind, what) : Task.FromResult<TaskRecord?>(null);

        private static readonly System.Text.RegularExpressions.Regex LeadingToken = new(@"^\s*\[[^\]]+\]\s*");
        private static readonly System.Text.RegularExpressions.Regex DependsOnClause = new(@"\(depends on:[^)]*\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static string? TaskToken(string text) => TaskTags.Of(text);

        /// <summary>
        /// Sends each failed task whose fix finished since it failed back to run, with its retries cleared (TASK-223).
        /// Returns whether any went back.
        /// </summary>
        private bool ReopenFixedTasks()
        {
            var all = _taskTracker.GetAllTasks();
            var reopened = false;
            foreach (var failed in all.Where(t => t.Status == TaskStatus.Failed))
            {
                if (!all.Any(f => f.FixesTaskId == failed.Id && f.Status == TaskStatus.Done && f.UpdatedAt >= failed.UpdatedAt))
                    continue;
                failed.RetryCount = 0;
                failed.AcceptanceAttempts = 0;
                failed.NextRetryAt = null;
                failed.LastRetryAttempt = null;
                _taskTracker.ClearError(failed);
                _taskTracker.UpdateTask(failed, TaskStatus.Unassigned);
                reopened = true;
                _logger?.LogInformation("Task {TaskId} runs again: its fix task is done", failed.Id[..Math.Min(8, failed.Id.Length)]);
            }
            return reopened;
        }

        /// <summary>
        /// How many tasks wait on the task <paramref name="fix"/> fixes, directly or through others (TASK-223): a fix task's
        /// impact. Zero for any other task.
        /// </summary>
        private int FixImpact(TaskRecord fix, IReadOnlyList<TaskRecord> all)
        {
            if (fix.FixesTaskId is not { } target || all.FirstOrDefault(t => t.Id == target) is not { } failed ||
                TaskToken(failed.Task) is not { } token)
                return 0;
            var waiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var frontier = new Queue<string>([token]);
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var task in all.Where(t => t.Status != TaskStatus.Done))
                {
                    var deps = task.Dependencies.Count > 0 ? task.Dependencies : ParseDependenciesFromDescription(task.Task);
                    if (!deps.Contains(current, StringComparer.OrdinalIgnoreCase) || TaskToken(task.Task) is not { } own) continue;
                    if (waiting.Add(own)) frontier.Enqueue(own);
                }
            }
            return waiting.Count;
        }

        /// <summary>Fails the task for good; with <paramref name="unmet"/>, files one fix task naming it.</summary>
        private async Task FailAcceptanceAsync(TaskRecord task, string error, IReadOnlyList<string>? unmet)
        {
            var previous = task.Status;
            await UpdateTaskWithWalAsync(task, TaskStatus.Failed, task.AssignedAgent, error);

            if (unmet is { Count: > 0 })
            {
                // A fix task like any other (TASK-231): linked to the task, never quoting its [tag], and when Done it
                // reruns the task with its checks afresh
                var priority = unmet.Any(u => u.StartsWith("Export ", StringComparison.Ordinal))
                    ? TaskPriority.Critical
                    : TaskPriority.High;
                await FileFixTaskAsync(task, "acceptance", $"the acceptance check found unmet: {string.Join(" ", unmet)}", priority);
            }

            await SaveTasksToFileAsync();
            PublishStatusChange(task, previous, error);
            _logger?.LogWarning("Task {TaskId} failed the acceptance check: {Error}", task.Id[..Math.Min(8, task.Id.Length)], error);
        }

        /// <summary>
        /// A person's answer to a finished task's review card (FEATURE-031 D4): "Approve…" marks it Done, keeping any
        /// remark on the decision; anything else rejects it, the note taking the reopen path as the unmet criterion.
        /// </summary>
        private async Task ApplyReviewAsync(TaskRecord task, PendingDecision decision)
        {
            if (task.Status != TaskStatus.AwaitingHumanDecision)
                return;

            var answer = decision.Resolution ?? "";
            if (AcceptanceReview.IsEscalation(decision) && answer == PendingDecisionNotification.HandleAutomatically)
            {
                var max = _acceptance?.MaxAcceptanceReopens ?? new Models.Configuration.VerificationConfiguration().MaxAcceptanceReopens;
                var summary = decision.Escalation!.Summary;
                await FailAcceptanceAsync(task, $"{ErrorClassifier.AcceptanceFailurePrefix} after {max} reopen(s): {summary}", [summary]);
                return;
            }
            if (!AcceptanceReview.IsApproval(answer))
            {
                // Still parked here, so the WrongApproach route revises the task's saved plan. A person may send it
                // back past the reopen limit
                await ReopenOrEscalateAsync(task, [$"A person rejected the finished work: {AcceptanceReview.Note(answer)}"], null,
                    byPerson: true);
                return;
            }

            await UpdateTaskWithWalAsync(task, TaskStatus.Done, task.AssignedAgent);
            if (!string.IsNullOrWhiteSpace(task.Provider))
                _circuitBreaker?.RecordSuccess(task.Provider);
            if (!string.IsNullOrEmpty(_projectId) && _planService != null)
                _ = _planService.DeleteConversationCheckpointAsync(_projectId, task.Id);
            await SaveTasksToFileAsync();
            PublishStatusChange(task, TaskStatus.AwaitingHumanDecision, null);
            _logger?.LogInformation("Task {TaskId} approved by a person", task.Id[..Math.Min(8, task.Id.Length)]);
        }

        private void PublishStatusChange(TaskRecord task, TaskStatus previous, string? reason)
        {
            if (_eventBus == null || previous == task.Status)
                return;
            try
            {
                _ = _eventBus.PublishAsync(new TaskStatusChangedEvent
                {
                    ProjectId = _projectId ?? "unknown",
                    TaskId = task.Id,
                    OldStatus = previous.ToString(),
                    NewStatus = task.Status.ToString(),
                    ErrorMessage = reason
                });
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to publish TaskStatusChangedEvent for task {TaskId}", task.Id[..Math.Min(8, task.Id.Length)]);
            }
        }

        private async Task<PendingDecision?> LatestResolvedAsync(string taskId)
        {
            try
            {
                return (await _pendingDecisions!.GetResolvedForTaskAsync(taskId)).FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not read the answered decisions of task {TaskId}", taskId);
                return null;
            }
        }

        private async Task RecordAnswerOnPlanAsync(PendingDecision decision, string answer)
        {
            if (_planService == null)
                return;
            try
            {
                var plan = await _planService.LoadPlanAsync(decision.ProjectId, decision.TaskId);
                if (plan == null)
                    return;
                plan.AddLogEntry($"Human decision: {answer}");
                await _planService.SavePlanAsync(plan);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not record the answer on the plan of task {TaskId}", decision.TaskId);
            }
        }

        private void RaiseDecisionCallback(Action<string, PendingDecision>? callback, PendingDecision decision)
        {
            if (callback == null)
                return;
            try
            {
                var projectName = _projectRepository?.GetById(_projectId ?? "")?.Name ?? _projectId ?? "unknown";
                callback(projectName, decision);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Decision notification failed for {DecisionId}", decision.Id[..8]);
            }
        }

        #endregion

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
                AwaitingDecisionTasks = allTasks.Count(t => t.Status == TaskStatus.AwaitingHumanDecision),
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
        /// Also clears error state when retrying a task (transitioning to Unassigned).
        /// </summary>
        /// <param name="task">Task to update</param>
        /// <param name="newStatus">New status for the task</param>
        public void UpdateTask(TaskRecord task, TaskStatus newStatus)
        {
            _taskTracker.UpdateTask(task, newStatus);

            // Clear error state when retrying a task
            if (newStatus == TaskStatus.Unassigned)
            {
                _taskTracker.ClearError(task);
            }

            SaveTasksToFile();
        }

        /// <summary>
        /// Sets a task's priority (TASK-154); the work picker orders by it on its next pass.
        /// </summary>
        public void SetTaskPriority(TaskRecord task, TaskPriority priority)
        {
            _taskTracker.SetPriority(task, priority);
            SaveTasksToFile();
        }

        /// <summary>
        /// Writes the task file (JSON and markdown) and clears the WAL. TASK-178: a tracker holding no tasks does not write
        /// over a file that holds some, since Drake never removes tasks; that tracker loaded nothing from a file it could not
        /// read, and saving would destroy the tasks. The WAL is kept in that case too.
        /// </summary>
        private async Task WriteTaskFileAsync()
        {
            if (_taskTracker.GetAllTasks().Count == 0 && TaskTracker.FileHoldsTasks(_outputMarkdownPath))
            {
                _logger?.LogWarning("Not saving an empty task list over {Path}: the file holds tasks this Drake could not load",
                    _outputMarkdownPath);
                return;
            }

            await _taskTracker.SaveToFileAsync(_outputMarkdownPath, "Drake Task Report");
            await _wal.CheckpointAsync();
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
                        await WriteTaskFileAsync();
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
        /// Clears escalation alerts associated with a task from its implementation plan.
        /// Called when a task is being retried to prevent stale escalation history.
        /// </summary>
        public async Task ClearEscalationsForTaskAsync(string taskId)
        {
            if (string.IsNullOrEmpty(taskId) || _planService == null || string.IsNullOrEmpty(_projectId))
                return;

            try
            {
                var plan = await _planService.LoadPlanAsync(_projectId, taskId);
                if (plan?.Escalations != null && plan.Escalations.Count > 0)
                {
                    plan.Escalations.Clear();
                    await _planService.SavePlanDebouncedAsync(plan);
                    _logger?.LogDebug("Cleared escalation history for task {TaskId} before retry", taskId[..Math.Min(8, taskId.Length)]);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clear escalations for task {TaskId}", taskId[..Math.Min(8, taskId.Length)]);
            }
        }

        /// <summary>
        /// Forces an immediate save of the current task state (async)
        /// </summary>
        public async Task SaveTasksToFileAsync()
        {
            await WriteTaskFileAsync();
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
        /// Flushes all pending saves and closes the save channel.
        /// Called during graceful shutdown to ensure no task state is lost.
        /// </summary>
        public async Task FlushAndCloseAsync()
        {
            try
            {
                // Complete channel writer (signals ProcessSaveQueueAsync to exit)
                _saveChannel.Writer.TryComplete();

                // Wait for the background save task to finish processing (5s timeout)
                await Task.WhenAny(_saveTask, Task.Delay(5000));

                // One final immediate save to capture any last state changes
                await WriteTaskFileAsync();

                _logger?.LogInformation("Drake flushed and closed successfully for {Path}", _outputMarkdownPath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during Drake flush for {Path}", _outputMarkdownPath);
            }
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

            await ReconcilePendingDecisionsAsync();

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
                        _taskToKoboldMap.TryRemove(task.Id, out _);
                    }

                    // Check if the task has a completed plan - if so, mark as Done
                    var newStatus = TaskStatus.Unassigned;
                    var settledByAcceptance = false;
                    _logger?.LogDebug("🔍 Checking plan for orphaned task {TaskId}, planService={HasPlanService}, projectId={ProjectId}",
                        task.Id[..Math.Min(8, task.Id.Length)], _planService != null, _projectId ?? "(null)");

                    if (_planService != null && !string.IsNullOrEmpty(_projectId))
                    {
                        try
                        {
                            var plan = await _planService.LoadPlanAsync(_projectId, task.Id);
                            _logger?.LogDebug("🔍 Plan lookup result for task {TaskId}: found={Found}, status={Status}",
                                task.Id[..Math.Min(8, task.Id.Length)], plan != null, plan?.Status.ToString() ?? "(null)");

                            if (plan != null && plan.Status == PlanStatus.Completed && _acceptance != null
                                && !await PassesAcceptanceAsync(task, null, null))
                            {
                                // Reopened, failed or parked for review by the acceptance check (TASK-025)
                                settledByAcceptance = true;
                            }
                            else if (plan != null && plan.Status == PlanStatus.Completed)
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

                    if (settledByAcceptance)
                    {
                        recovered++;
                        continue;
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
        /// Recovers task state from Write-Ahead Log after a crash
        /// </summary>
        private async Task RecoverFromWalAsync()
        {
            try
            {
                var entries = await _wal.ReadAllAsync();
                if (entries.Count == 0)
                {
                    _logger?.LogInformation("WAL recovery: No entries to replay");
                    return;
                }

                _logger?.LogInformation("WAL recovery: Replaying {Count} state changes", entries.Count);

                // Replay each entry
                foreach (var entry in entries)
                {
                    var task = _taskTracker.GetTaskById(entry.TaskId);
                    if (task == null)
                    {
                        _logger?.LogWarning("WAL recovery: Task {TaskId} not found, skipping", entry.TaskId);
                        continue;
                    }

                    // Apply the state transition
                    if (Enum.TryParse<TaskStatus>(entry.NewStatus, out var newStatus))
                    {
                        // Bug A residual fix — if WAL says Done but the task has no CommitSha,
                        // the original execution crashed between WAL-append and commit completion.
                        // Demote to Failed so FailureRecoveryJob re-runs the task instead of
                        // accepting an unverified Done that may have never committed to git.
                        if (newStatus == TaskStatus.Done && string.IsNullOrEmpty(task.CommitSha))
                        {
                            _taskTracker.UpdateTask(task, TaskStatus.Failed, entry.AssignedAgent);
                            _taskTracker.SetError(task,
                                "WAL recovery: task reached Done in WAL but no commit was recorded — likely crashed mid-flow. Retrying.");
                            _logger?.LogWarning(
                                "WAL recovery: {TaskId} Done→Failed (no CommitSha — crash before commit completed)",
                                entry.TaskId);
                            continue;
                        }

                        _taskTracker.UpdateTask(task, newStatus, entry.AssignedAgent);

                        if (!string.IsNullOrEmpty(entry.ErrorMessage))
                        {
                            _taskTracker.SetError(task, entry.ErrorMessage);
                        }

                        _logger?.LogDebug("WAL recovery: {TaskId} → {NewStatus}", entry.TaskId, entry.NewStatus);
                    }
                }

                // Save recovered state to file
                await WriteTaskFileAsync();
                
                _logger?.LogInformation("WAL recovery completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "WAL recovery failed");
            }
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
            if (ReopenFixedTasks())
                SaveTasksToFile();
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
                // Check if dependencies are met
                // Uses structured Dependencies property (preferred) or falls back to parsing from description
                var dependenciesMet = true;
                var unmeetDependencies = new List<string>();
                var failedDependencies = new List<string>();

                // Get dependencies from structured property (reliable, no truncation issues)
                // Fall back to parsing from description for backwards compatibility
                var dependencies = task.Dependencies.Count > 0
                    ? task.Dependencies
                    : ParseDependenciesFromDescription(task.Task);

                foreach (var dep in dependencies)
                {
                    // Check if dependency is done (look for [dep] in done task descriptions)
                    var depMet = doneTasks.Any(doneTask =>
                        TaskTags.Is(doneTask, dep));

                    if (depMet)
                    {
                        continue;
                    }

                    // Check if dependency has failed
                    var depFailed = failedTasks.Any(failedTask =>
                        TaskTags.Is(failedTask, dep));

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

                // Blocked, but no dependency has failed any more (done, or sent back to run, e.g. by a finished fix task,
                // TASK-223): it waits as an ordinary task again
                if (task.Status == TaskStatus.BlockedByFailure)
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
                // Normalize to ensure valid agent type (handles cases where area names were used)
                var agentType = AgentTypeValidator.Normalize(task.AssignedAgent);

                result.Add((task, agentType));
            }

            // Sort by priority before returning
            return SortTasksByPriority(result);
        }

        /// <summary>
        /// Sorts ready tasks by priority (descending), then by complexity
        /// </summary>
        private List<(TaskRecord Task, string AgentType)> SortTasksByPriority(List<(TaskRecord Task, string AgentType)> tasks)
        {
            var all = _taskTracker.GetAllTasks();
            return tasks
                .OrderByDescending(t => t.Task.Priority)  // Critical > High > Normal > Low
                .ThenByDescending(t => t.Task.FixesTaskId != null)   // fix tasks first within a priority (TASK-223)
                .ThenByDescending(t => FixImpact(t.Task, all))        // the one most work waits on first
                .ThenBy(t => GetComplexityScore(t.Task))   // Simple tasks first within same priority
                .ToList();
        }

        /// <summary>
        /// Estimates complexity score from task description (lower score = simpler task)
        /// </summary>
        private int GetComplexityScore(TaskRecord task)
        {
            var description = task.Task.ToLower();
            
            // Check for explicit complexity indicators in task description
            if (description.Contains("setup") || description.Contains("create") || description.Contains("add"))
                return 1; // Simple tasks
            if (description.Contains("implement") || description.Contains("build"))
                return 2; // Medium tasks
            if (description.Contains("integrate") || description.Contains("refactor") || description.Contains("optimize"))
                return 3; // Complex tasks
            
            return 2; // Default to medium complexity
        }

        /// <summary>
        /// Parses dependencies from a task description string (backwards compatibility).
        /// Format: "Task name (depends on: dep1, dep2)"
        /// </summary>
        private static List<string> ParseDependenciesFromDescription(string taskDescription)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                taskDescription, @"\(depends on:\s*([^)]+)\)");

            if (!match.Success)
            {
                return new List<string>();
            }

            return match.Groups[1].Value
                .Split(',')
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();
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

        /// <summary>
        /// Checks if this Drake has any tasks ready for execution (unassigned with dependencies met).
        /// Returns false if all unassigned tasks are blocked by unfulfilled dependencies.
        /// </summary>
        /// <returns>True if at least one task is ready to execute, false if all are blocked</returns>
        public bool HasReadyTasks()
        {
            var readyTasks = GetUnassignedTasks();
            return readyTasks.Count > 0;
        }

        /// <summary>
        /// Builds dependency context information from completed dependency tasks.
        /// Includes task descriptions, files created, and execution insights.
        /// CONTEXT SLIPPING FIX: Now includes error patterns and complexity indicators from dependency plans.
        /// </summary>
        /// <param name="taskDescription">Task description containing dependency references</param>
        /// <returns>Formatted context string describing completed dependencies and their outputs</returns>
        private async Task<string> BuildDependencyContextAsync(string taskDescription)
        {
            var sb = new System.Text.StringBuilder();
            
            // Parse dependencies from task description: (depends on: dep1, dep2)
            var dependsOnMatch = System.Text.RegularExpressions.Regex.Match(
                taskDescription, @"\(depends on:\s*([^)]+)\)");

            if (!dependsOnMatch.Success)
            {
                return string.Empty;
            }

            var dependencyIds = dependsOnMatch.Groups[1].Value
                .Split(',')
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            if (dependencyIds.Count == 0)
            {
                return string.Empty;
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Completed Dependencies");
            sb.AppendLine();
            sb.AppendLine("The following tasks were completed before this one. Reuse their outputs instead of recreating:");
            sb.AppendLine();

            // Get all tasks to look up dependencies
            var allTasks = _taskTracker.GetAllTasks();

            foreach (var depId in dependencyIds)
            {
                // Find the dependency task by looking for [dep-id] in task description
                var depTask = allTasks.FirstOrDefault(t =>
                    TaskTags.Is(t.Task, depId) &&
                    t.Status == TaskStatus.Done);

                if (depTask != null)
                {
                    // Extract clean task description (remove [id] prefix and dependency info)
                    var cleanTask = System.Text.RegularExpressions.Regex.Replace(
                        depTask.Task, @"^\[id:[a-f0-9-]+\]\s*", "");
                    cleanTask = System.Text.RegularExpressions.Regex.Replace(
                        cleanTask, @"\s*\(depends on:[^)]+\)\s*", "");

                    sb.AppendLine($"### [{depId}] {cleanTask}");
                    sb.AppendLine($"- **Agent**: {depTask.AssignedAgent}");

                    if (!string.IsNullOrEmpty(depTask.CommitSha))
                    {
                        sb.AppendLine($"- **Commit**: {depTask.CommitSha[..Math.Min(8, depTask.CommitSha.Length)]}");
                    }

                    // CONTEXT SLIPPING FIX: Add execution insights from dependency task
                    // This helps the Kobold understand what worked/didn't work
                    if (_sharedPlanningContext != null && !string.IsNullOrEmpty(depTask.ProjectId))
                    {
                        try
                        {
                            // Load the plan for this dependency to get execution details
                            var depPlan = _planService != null ? await _planService.LoadPlanAsync(depTask.ProjectId, depTask.Id) : null;
                            if (depPlan != null)
                            {
                                var totalIterations = depPlan.Steps.Sum(s => s.Metrics.IterationsUsed);
                                var avgIterationsPerStep = depPlan.Steps.Count > 0
                                    ? (double)totalIterations / depPlan.Steps.Count
                                    : 0;

                                sb.AppendLine($"- **Execution**: {depPlan.CompletedStepsCount}/{depPlan.Steps.Count} steps, {totalIterations} iterations total");

                                // Note any steps that had retries or errors
                                var retriedSteps = depPlan.Steps.Where(s => s.RetryCount > 0).ToList();
                                if (retriedSteps.Any())
                                {
                                    sb.AppendLine($"- **Challenges**: {retriedSteps.Count} step(s) required retries to complete");

                                    // Show what the challenges were (if error info available)
                                    var errorsEncountered = retriedSteps
                                        .Select(s => s.ErrorMessage)
                                        .Where(e => !string.IsNullOrEmpty(e))
                                        .Distinct()
                                        .Take(2)
                                        .ToList();

                                    if (errorsEncountered.Count > 0)
                                    {
                                        var errorSummaries = errorsEncountered.Select(e => e!.Length > 60 ? e.Substring(0, 60) + "..." : e);
                                        sb.AppendLine($"  - Issues overcome: {string.Join("; ", errorSummaries)}");
                                    }
                                }

                                // Show the approach if available from plan metadata
                                if (depPlan.Steps.Count > 0 && avgIterationsPerStep > 5)
                                {
                                    sb.AppendLine($"- **Complexity**: This task required {avgIterationsPerStep:F0} iterations per step - expect similar complexity");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(ex, "Could not load plan insights for dependency {DepId}", depId);
                        }
                    }

                    // List output files if available
                    if (depTask.OutputFiles.Any())
                    {
                        sb.AppendLine("- **Files created/modified**:");
                        foreach (var file in depTask.OutputFiles.Take(20)) // Limit to avoid overwhelming context
                        {
                            sb.AppendLine($"  - `{file}`");
                        }
                        if (depTask.OutputFiles.Count > 20)
                        {
                            sb.AppendLine($"  - *(and {depTask.OutputFiles.Count - 20} more files)*");
                        }
                    }
                    else
                    {
                        sb.AppendLine("- *(No output files tracked)*");
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// GAP FIX 1 & 4: Loads the full Specification from disk with features.
        /// </summary>
        private async Task<Specification?> LoadSpecificationAsync()
        {
            if (string.IsNullOrEmpty(_specificationPath) || !File.Exists(_specificationPath))
            {
                return null;
            }

            try
            {
                var content = await File.ReadAllTextAsync(_specificationPath);
                var projectFolder = Path.GetDirectoryName(_specificationPath) ?? "";

                var spec = new Specification
                {
                    Name = _projectId ?? "Unknown",
                    FilePath = _specificationPath,
                    ProjectFolder = projectFolder,
                    Content = content,
                    ContentHash = Specification.ComputeHash(content)
                };

                // Load features from specification.features.json
                var featuresPath = Path.Combine(projectFolder, "specification.features.json");
                if (File.Exists(featuresPath))
                {
                    try
                    {
                        var featuresJson = await File.ReadAllTextAsync(featuresPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(featuresJson);

                        if (doc.RootElement.TryGetProperty("features", out var featuresProp))
                        {
                            var options = new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            var features = System.Text.Json.JsonSerializer.Deserialize<List<Feature>>(
                                featuresProp.GetRawText(), options);
                            if (features != null)
                            {
                                spec.Features = features;
                            }
                        }

                        if (doc.RootElement.TryGetProperty("specificationVersion", out var versionProp))
                        {
                            spec.Version = versionProp.GetInt32();
                        }

                        if (doc.RootElement.TryGetProperty("specificationContentHash", out var hashProp))
                        {
                            spec.ContentHash = hashProp.GetString() ?? spec.ContentHash;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to load features from {Path}", featuresPath);
                    }
                }

                _logger?.LogDebug("Loaded specification {Name} v{Version} with {FeatureCount} features",
                    spec.Name, spec.Version, spec.Features.Count);

                return spec;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load specification from {Path}", _specificationPath);
                return null;
            }
        }

        /// <summary>
        /// Gets feature details from specification by feature ID.
        /// </summary>
        private (string? FeatureId, string? FeatureName, string? FeatureDescription, List<string> AcceptanceCriteria)
            GetFeatureFromSpecification(Specification spec, string featureId)
        {
            var feature = spec.Features.FirstOrDefault(f => f.Id == featureId);
            if (feature == null)
            {
                return (featureId, null, null, new List<string>());
            }

            return (feature.Id, feature.Name, feature.Description,
                feature.AcceptanceCriteria ?? new List<string>());
        }
    }
}
