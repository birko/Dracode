using Birko.AI.Orchestration.Models;
using Birko.EventBus;
using Birko.AI;
using Birko.AI.Providers;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using Birko.AI.Resilience.Services;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Factories
{
    /// <summary>
    /// Factory for creating and managing Drake supervisors.
    /// Each Drake monitors a specific task output path from the Wyvern.
    /// Supports creating Drakes with implementation planning capabilities.
    /// </summary>
    public class DrakeFactory
    {
        private readonly KoboldFactory _koboldFactory;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly IProjectRepository? _projectRepository;
        private readonly ITaskRepository? _taskRepository;
        private readonly GitService? _gitService;
        private readonly ProviderCircuitBreaker? _circuitBreaker;
        private readonly SharedPlanningContextService? _sharedPlanningContext;
        private readonly IEventBus? _eventBus;
        private readonly SqlPendingDecisionRepository? _pendingDecisions;
        private readonly ProviderRateLimiter? _rateLimiter;
        private readonly CostTrackingService? _costTracker;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly KoboldLairConfiguration _koboldLairConfig;
        private readonly string _projectsPath;
        private readonly bool _planningEnabled;
        private readonly bool _useEnhancedExecution;
        private readonly bool _allowPlanModifications;
        private readonly bool _autoApproveModifications;
        private readonly bool _filterFilesByPlan;
        private readonly Dictionary<string, Drake> _drakes;
        private readonly Dictionary<string, string?> _drakeProjectIds; // Maps drake name to project ID
        private readonly object _lock = new object();

        /// <summary>
        /// Optional callback invoked when a feature's tasks are all complete and its branch is ready for merge.
        /// Parameters: projectName, featureName, branchName
        /// </summary>
        public Action<string, string, string>? OnFeatureBranchReady { get; set; }

        /// <summary>
        /// Optional callback invoked when an escalation is raised and handled by Drake.
        /// Parameters: projectName, escalationAlert, resolution
        /// </summary>
        public Action<string, EscalationAlert, string>? OnEscalation { get; set; }

        /// <summary>Chooses automatic or human resolution for escalations (TASK-023 provides one); null keeps them automatic.</summary>
        public IEscalationDispositionPolicy? DispositionPolicy { get; set; }

        /// <summary>How much Kobolds may ask a person (TASK-023 provides one); null allows every question, no cap.</summary>
        public IAskHumanPolicy? AskHumanPolicy { get; set; }

        /// <summary>
        /// The question chain the pipeline's questions climb before they reach a person (TASK-201, FEATURE-078 D18, D24).
        /// With no level registered, every question reaches the person, as before the chain.
        /// </summary>
        public QuestionChain Chain { get; set; } = new();

        /// <summary>Raised when a task parks on a decision a person must answer. Parameters: projectName, decision.</summary>
        public Action<string, PendingDecision>? OnDecisionRequired { get; set; }

        /// <summary>Raised when a pending decision is answered or times out. Parameters: projectName, decision.</summary>
        public Action<string, PendingDecision>? OnDecisionResolved { get; set; }

        /// <summary>
        /// Creates a new DrakeFactory
        /// </summary>
        /// <param name="koboldFactory">Kobold factory for creating workers</param>
        /// <param name="providerConfigService">Provider configuration service</param>
        /// <param name="koboldLairConfig">KoboldLair configuration (extracts projectsPath and planning settings internally)</param>
        /// <param name="loggerFactory">Optional logger factory for Drake logging</param>
        /// <param name="gitService">Optional git service for committing changes on task completion</param>
        /// <param name="projectRepository">Optional project repository for resolving project paths and per-project parallel limits</param>
        /// <param name="circuitBreaker">Optional circuit breaker for provider failure tracking</param>
        /// <param name="sharedPlanningContext">Optional shared planning context service for cross-agent coordination</param>
        public DrakeFactory(
            KoboldFactory koboldFactory,
            ProviderConfigurationService providerConfigService,
            KoboldLairConfiguration koboldLairConfig,
            ILoggerFactory? loggerFactory = null,
            GitService? gitService = null,
            IProjectRepository? projectRepository = null,
            ProviderCircuitBreaker? circuitBreaker = null,
            SharedPlanningContextService? sharedPlanningContext = null,
            ITaskRepository? taskRepository = null,
            IEventBus? eventBus = null,
            ProviderRateLimiter? rateLimiter = null,
            CostTrackingService? costTracker = null,
            SqlPendingDecisionRepository? pendingDecisions = null)
        {
            _rateLimiter = rateLimiter;
            _costTracker = costTracker;
            _koboldFactory = koboldFactory;
            _providerConfigService = providerConfigService;
            _koboldLairConfig = koboldLairConfig;
            _projectRepository = projectRepository;
            _taskRepository = taskRepository;
            _loggerFactory = loggerFactory;
            _gitService = gitService;
            _circuitBreaker = circuitBreaker;
            _sharedPlanningContext = sharedPlanningContext;
            _eventBus = eventBus;
            _pendingDecisions = pendingDecisions;

            // Extract configuration values from koboldLairConfig
            _projectsPath = koboldLairConfig.ProjectsPath ?? "./projects";
            _planningEnabled = koboldLairConfig.Planning?.Enabled ?? true;
            _useEnhancedExecution = koboldLairConfig.Planning?.UseEnhancedExecution ?? true;
            _allowPlanModifications = koboldLairConfig.Planning?.AllowPlanModifications ?? false;
            _autoApproveModifications = koboldLairConfig.Planning?.AutoApproveModifications ?? false;
            _filterFilesByPlan = koboldLairConfig.Planning?.FilterFilesByPlan ?? true;
            
            _drakes = new Dictionary<string, Drake>();
            _drakeProjectIds = new Dictionary<string, string?>();
        }

        /// <summary>
        /// Creates a Drake supervisor that monitors tasks from a specific Wyvern output path
        /// </summary>
        /// <param name="taskFilePath">Path to the Wyvern task markdown file</param>
        /// <param name="drakeName">Optional name for the Drake (uses file path if not specified)</param>
        /// <param name="provider">Optional provider override</param>
        /// <param name="model">Optional model override</param>
        /// <param name="specificationPath">Optional path to project specification</param>
        /// <param name="projectId">Optional project identifier for resource limiting</param>
        /// <returns>Created Drake instance</returns>
        public Drake CreateDrake(
            string taskFilePath,
            string? drakeName = null,
            string? provider = null,
            string? model = null,
            string? specificationPath = null,
            string? projectId = null)
        {
            var name = drakeName ?? taskFilePath;

            // Quick check if Drake already exists (avoids expensive initialization)
            lock (_lock)
            {
                if (_drakes.ContainsKey(name))
                {
                    throw new InvalidOperationException($"Drake with name '{name}' already exists");
                }
            }

            // Perform expensive initialization outside the lock
            // Get provider settings
            string effectiveProvider;
            Dictionary<string, string> config;
            AgentOptions options;

            if (provider != null)
            {
                options = _providerConfigService.GetProviderSettingsForAgent("wyvern").options;
                // An override is a provider *name*: resolve its type and its own config (TASK-089).
                (effectiveProvider, config, _) = _providerConfigService.GetProviderSettingsByName(provider);
                if (model != null)
                {
                    config["model"] = model;
                }
            }
            else
            {
                (effectiveProvider, config, options) = _providerConfigService.GetProviderSettingsForAgent("wyvern");
            }

            // Create task tracker by reading the file if it exists
            var taskTracker = new TaskTracker
            {
                Repository = _taskRepository,
                ProjectId = projectId,
                AreaName = ExtractAreaName(taskFilePath)
            };
            if (File.Exists(taskFilePath))
            {
                // Load existing tasks from file
                LoadTasksFromFile(taskTracker, taskFilePath);

                // Back-fill database rows for tasks that only exist in the file (TASK-091) — loading never
                // adds them, so later status updates would target rows that do not exist.
                var backfillLogger = _loggerFactory?.CreateLogger<DrakeFactory>();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var added = await taskTracker.EnsureInRepositoryAsync();
                        if (added > 0)
                            backfillLogger?.LogInformation("Added {Count} missing task rows for {TaskFile}", added, taskFilePath);
                    }
                    catch (Exception ex)
                    {
                        backfillLogger?.LogError(ex, "Failed to back-fill task rows for {TaskFile}", taskFilePath);
                    }
                });
            }

            // Create logger for Drake
            var logger = _loggerFactory?.CreateLogger<Drake>();

            // Create plan service and planner agent if planning is enabled
            KoboldPlanService? planService = null;
            KoboldPlannerAgent? plannerAgent = null;

            if (_planningEnabled)
            {
                planService = new KoboldPlanService(
                    _projectsPath,
                    _loggerFactory?.CreateLogger<KoboldPlanService>(),
                    _projectRepository
                );

                // Create planner agent using kobold provider settings
                var (plannerProvider, plannerConfig, plannerOptions) =
                    _providerConfigService.GetProviderSettingsForAgent("kobold");

                // Set working directory: existing projects use source path, new projects use workspace subfolder
                var resolvedWorkspace = ResolveWorkspaceForProject(projectId, taskFilePath);
                if (!string.IsNullOrEmpty(resolvedWorkspace))
                {
                    plannerOptions.WorkingDirectory = resolvedWorkspace;
                }

                plannerAgent = (KoboldPlannerAgent)KoboldLairAgentFactory.Create(
                    plannerProvider,
                    _koboldLairConfig,
                    plannerOptions,
                    plannerConfig,
                    "kobold-planner",
                    _rateLimiter,
                    _costTracker,
                    _loggerFactory?.CreateLogger<TrackedLlmProvider>(),
                    projectId);
            }

            // Create implementation service for specification tracking
            ProjectImplementationService? implementationService = null;
            if (_projectRepository != null && !string.IsNullOrEmpty(projectId))
            {
                implementationService = new ProjectImplementationService(
                    _projectsPath,
                    _projectRepository,
                    _loggerFactory?.CreateLogger<ProjectImplementationService>());
            }
            // Create the Drake with all dependencies including plan service and project repository
            var drake = new Drake(
                _koboldFactory,
                taskTracker,
                taskFilePath,
                effectiveProvider,
                config,
                options,
                specificationPath,
                projectId,
                logger,
                _providerConfigService,
                _projectRepository,
                _gitService,
                planService,
                plannerAgent,
                _circuitBreaker,
                _planningEnabled,
                _useEnhancedExecution,
                _allowPlanModifications,
                _autoApproveModifications,
                _filterFilesByPlan,
                _sharedPlanningContext,
                implementationService,
                _koboldLairConfig.Planning,
                OnFeatureBranchReady,
                OnEscalation,
                _eventBus,
                _pendingDecisions,
                DispositionPolicy,
                AskHumanPolicy,
                OnDecisionRequired,
                OnDecisionResolved,
                _koboldLairConfig.Verification
            )
            {
                QuestionChain = Chain
            };

            // Lock only for dictionary insertion, with race condition check
            lock (_lock)
            {
                // Double-check in case another thread created it while we were initializing
                if (_drakes.ContainsKey(name))
                {
                    throw new InvalidOperationException($"Drake with name '{name}' already exists (race condition)");
                }

                _drakes[name] = drake;
                _drakeProjectIds[name] = projectId;

                return drake;
            }
        }

        /// <summary>
        /// Gets the count of active drakes for a specific project
        /// </summary>
        public int GetActiveDrakeCountForProject(string? projectId)
        {
            lock (_lock)
            {
                return _drakeProjectIds.Count(kvp => kvp.Value == projectId);
            }
        }

        /// <summary>
        /// Checks if a new drake can be created for the specified project based on the parallel limit
        /// </summary>
        public bool CanCreateDrakeForProject(string? projectId)
        {
            var currentCount = GetActiveDrakeCountForProject(projectId);
            var maxAllowed = AgentLimitResolver.GetMaxParallel(_projectRepository, _koboldLairConfig.Limits, projectId, "drake");
            return currentCount < maxAllowed;
        }

        /// <summary>
        /// Gets an existing Drake by name
        /// </summary>
        public Drake? GetDrake(string drakeName)
        {
            lock (_lock)
            {
                return _drakes.TryGetValue(drakeName, out var drake) ? drake : null;
            }
        }

        /// <summary>
        /// Gets all Drakes
        /// </summary>
        public IReadOnlyCollection<Drake> GetAllDrakes()
        {
            lock (_lock)
            {
                return _drakes.Values.ToList();
            }
        }

        /// <summary>
        /// Gets all Drakes associated with a specific project
        /// </summary>
        /// <param name="projectId">Project identifier</param>
        /// <returns>List of Drake instances with their names</returns>
        public List<(Drake Drake, string Name)> GetDrakesForProject(string projectId)
        {
            lock (_lock)
            {
                return _drakeProjectIds
                    .Where(kvp => kvp.Value == projectId)
                    .Select(kvp => (_drakes[kvp.Key], kvp.Key))
                    .ToList();
            }
        }

        /// <summary>
        /// The analyzer's questions for a project (TASK-170, FEATURE-078 D26), asked since <paramref name="since"/>: decisions
        /// on the whole project, with no task. Empty without a decision store.
        /// </summary>
        public async Task<IReadOnlyList<PendingDecision>> ProjectQuestionsAsync(string projectId, DateTime since) =>
            _pendingDecisions == null
                ? Array.Empty<PendingDecision>()
                : await _pendingDecisions.GetProjectLevelAsync(projectId, since);

        /// <summary>Whether the project's analysis waits on a person: one of its project-level questions is still open.</summary>
        public async Task<bool> ProjectWaitsOnPersonAsync(string projectId) =>
            _pendingDecisions != null
            && (await _pendingDecisions.GetUnresolvedForProjectAsync(projectId)).Any(d => d.TaskId.Length == 0);

        /// <summary>
        /// Whether the analyzer may ask a person for this project: a decision store exists and the ask policy allows it
        /// (a policy that fails allows, as for a Kobold's <c>ask_human</c>).
        /// </summary>
        public bool CanAskForProject(string projectId)
        {
            if (_pendingDecisions == null)
                return false;
            try
            {
                return (AskHumanPolicy ?? UnrestrictedAskHumanPolicy.Instance).For(projectId).Allow;
            }
            catch (Exception ex)
            {
                _loggerFactory?.CreateLogger<DrakeFactory>().LogWarning(ex, "Ask-human policy failed for {ProjectId}; allowing", projectId);
                return true;
            }
        }

        /// <summary>
        /// Stores the analyzer's questions as decisions on the whole project (an empty task id, <see cref="PendingDecisionSource.AskHuman"/>,
        /// no timeout) and raises a card for each (TASK-170). The project's analysis then waits until every one is answered.
        /// Returns the stored decisions; none when asking is not possible here.
        /// </summary>
        public async Task<IReadOnlyList<PendingDecision>> AskProjectQuestionsAsync(string projectId, IEnumerable<AskHumanQuestion> questions)
        {
            if (!CanAskForProject(projectId))
                return Array.Empty<PendingDecision>();
            var projectName = _projectRepository?.GetById(projectId)?.Name ?? projectId;
            var asked = new List<PendingDecision>();
            foreach (var question in questions)
            {
                // The overseer asks: the question climbs from Dragon's level, and only one no level answers reaches the person
                var outcome = await Chain.AskAsync(new ChainQuestion(projectId, null, QuestionLevel.Overseer, question));
                var decision = new PendingDecision
                {
                    ProjectId = projectId,
                    TaskId = "",
                    Disposition = EscalationDisposition.HumanDecisionRequired,
                    Source = PendingDecisionSource.AskHuman,
                    Question = question.ToJson(),
                    Path = outcome.Path.ToList()
                };
                if (!outcome.ReachesPerson)
                {
                    // Answered on the way: stored answered, so the analysis re-runs with it and no card goes to the person
                    decision.Resolution = outcome.Answer!.Answer;
                    decision.ResolutionKind = PendingDecisionResolution.Answered;
                    decision.ResolvedAt = DateTime.UtcNow;
                }
                await _pendingDecisions!.CreateAsync(decision);
                asked.Add(decision);
                if (!outcome.ReachesPerson)
                {
                    await Chain.DeliverAnswerAsync(new ChainQuestion(projectId, null, QuestionLevel.Overseer, question),
                        outcome.Path, decision.Resolution!, outcome.AnsweredBy);
                    continue;
                }
                try
                {
                    OnDecisionRequired?.Invoke(projectName, decision);
                }
                catch (Exception ex)
                {
                    _loggerFactory?.CreateLogger<DrakeFactory>().LogWarning(ex, "Decision notification failed for {DecisionId}", decision.Id);
                }
            }
            return asked;
        }

        /// <summary>
        /// Answers a pending decision through whichever of the project's Drakes holds its task. With none alive, the
        /// answer is stored and the Drake applies it when it next starts: its reconcile runs the escalation's automatic
        /// route for "Handle it automatically" and unparks the task. Returns what became of the answer, as
        /// <see cref="Drake.ResolvePendingDecisionAsync"/> does.
        /// </summary>
        public async Task<DecisionAnswerResult> ResolvePendingDecisionAsync(string projectId, string decisionId, string answer)
        {
            var result = await ResolveAnswerAsync(projectId, decisionId, answer);
            if (result == DecisionAnswerResult.Resolved && _pendingDecisions != null
                && await _pendingDecisions.GetByIdAsync(decisionId) is { Path.Count: > 0 } decision
                && AskHumanQuestion.TryParse(decision) is { } question)
            {
                // The person's answer goes back down the path the question climbed (TASK-201, D24)
                var askedBy = decision.TaskId.Length == 0 ? QuestionLevel.Overseer : QuestionLevel.Worker;
                await Chain.DeliverAnswerAsync(new ChainQuestion(projectId, decision.TaskId.Length == 0 ? null : decision.TaskId, askedBy, question),
                    decision.Path, decision.Resolution ?? answer, QuestionLevel.Person);
            }
            return result;
        }

        private async Task<DecisionAnswerResult> ResolveAnswerAsync(string projectId, string decisionId, string answer)
        {
            foreach (var drake in GetDrakesByProject(projectId))
            {
                var result = await drake.ResolvePendingDecisionAsync(decisionId, answer);
                if (result != DecisionAnswerResult.NotFound)
                    return result;
            }

            if (_pendingDecisions == null)
                return DecisionAnswerResult.NotFound;
            var decision = await _pendingDecisions.GetByIdAsync(decisionId);
            if (decision == null || decision.ProjectId != projectId)
                return DecisionAnswerResult.NotFound;
            if (decision.IsResolved)
                return DecisionAnswerResult.AlreadyResolved;
            if (!PendingDecisionNotification.AcceptsAnswer(decision, answer, out answer))
                return DecisionAnswerResult.InvalidAnswer;
            var at = DateTime.UtcNow;
            if (!await _pendingDecisions.ResolveAsync(decisionId, PendingDecisionResolution.Answered, answer, at))
                return DecisionAnswerResult.AlreadyResolved;

            decision.Resolution = answer;
            decision.ResolutionKind = PendingDecisionResolution.Answered;
            decision.ResolvedAt = at;
            try
            {
                var projectName = _projectRepository?.GetById(projectId)?.Name ?? projectId;
                OnDecisionResolved?.Invoke(projectName, decision);
            }
            catch (Exception ex)
            {
                _loggerFactory?.CreateLogger<DrakeFactory>().LogWarning(ex, "Decision notification failed for {DecisionId}", decisionId);
            }
            return DecisionAnswerResult.Resolved;
        }

        /// <summary>
        /// Gets all Drakes associated with a specific project (simple list without names)
        /// </summary>
        /// <param name="projectId">Project identifier</param>
        /// <returns>List of Drake instances</returns>
        public List<Drake> GetDrakesByProject(string projectId)
        {
            lock (_lock)
            {
                return _drakeProjectIds
                    .Where(kvp => kvp.Value == projectId)
                    .Select(kvp => _drakes[kvp.Key])
                    .ToList();
            }
        }

        /// <summary>
        /// Removes a Drake
        /// </summary>
        public bool RemoveDrake(string drakeName)
        {
            lock (_lock)
            {
                _drakeProjectIds.Remove(drakeName);
                return _drakes.Remove(drakeName);
            }
        }

        /// <summary>
        /// Removes all Drakes associated with a specific project.
        /// Used when retrying analysis or cancelling a project.
        /// </summary>
        /// <param name="projectId">Project identifier</param>
        /// <returns>Number of Drakes removed</returns>
        public int RemoveAllDrakesForProject(string projectId)
        {
            lock (_lock)
            {
                var drakeNames = _drakeProjectIds
                    .Where(kvp => kvp.Value == projectId)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var drakeName in drakeNames)
                {
                    _drakeProjectIds.Remove(drakeName);
                    _drakes.Remove(drakeName);
                }

                return drakeNames.Count;
            }
        }

        /// <summary>
        /// Gets the total number of Drakes
        /// </summary>
        public int TotalDrakes
        {
            get
            {
                lock (_lock)
                {
                    return _drakes.Count;
                }
            }
        }

        /// <summary>
        /// Resolves the workspace path for a project.
        /// For existing projects, uses the original source path.
        /// For new projects, derives workspace from task file path.
        /// </summary>
        private string? ResolveWorkspaceForProject(string? projectId, string taskFilePath)
        {
            if (_projectRepository != null && !string.IsNullOrEmpty(projectId))
            {
                var project = _projectRepository.GetById(projectId);
                if (project?.Metadata.TryGetValue("IsExistingProject", out var isExisting) == true &&
                    isExisting == "true" &&
                    project.Metadata.TryGetValue("SourcePath", out var sourcePath) == true &&
                    Directory.Exists(sourcePath))
                {
                    return sourcePath;
                }
            }

            // Default: derive workspace from task file path
            var taskFolder = Path.GetDirectoryName(taskFilePath);
            var projectFolder = !string.IsNullOrEmpty(taskFolder) ? Path.GetDirectoryName(taskFolder) : null;
            return !string.IsNullOrEmpty(projectFolder) ? Path.Combine(projectFolder, "workspace") : null;
        }

        /// <summary>
        /// Loads tasks from a markdown file into the task tracker.
        /// Parses the markdown table format to restore task state after restarts.
        /// </summary>
        private void LoadTasksFromFile(TaskTracker taskTracker, string filePath)
        {
            var tasksLoaded = taskTracker.LoadFromFile(filePath);

            if (tasksLoaded > 0)
            {
                var logger = _loggerFactory?.CreateLogger<DrakeFactory>();
                logger?.LogInformation(
                    "📂 Loaded {Count} task(s) from {FilePath}",
                    tasksLoaded,
                    Path.GetFileName(filePath));
            }
        }

        /// <summary>
        /// Puts a failed task back to Unassigned so it runs again (TASK-153), wherever it lives: in a Drake of the project
        /// (the task's Kobold is let go and the task file saved), else in its task file (the store row follows), else
        /// only in the task store. A Drake's tracker and its task file are what Drake works from; resetting just the
        /// store row, as the REST retry once did, left the task Failed for Drake, which saved Failed back over it.
        /// </summary>
        public async Task<TaskRetry> RetryFailedTaskAsync(Models.Projects.Project project, string taskId)
        {
            var retry = await ResetFailedTaskAsync(project, taskId);
            return retry.Outcome == TaskRetryOutcome.Retried
                ? retry with { ReopenedFrom = ReopenFinishedProject(project.Id) }
                : retry;
        }

        /// <summary>
        /// Puts a project whose tasks were all finished (AwaitingVerification, Verified, Completed or Failed) back to
        /// InProgress, so Drake runs a task retried in it (TASK-196): Drake only works Analyzed and InProgress projects.
        /// When the task is done the project goes through verification again. Returns the status it was reopened from,
        /// or null when it was still being worked or is unknown.
        /// </summary>
        public Models.Projects.ProjectStatus? ReopenFinishedProject(string projectId)
        {
            var stored = _projectRepository?.GetById(projectId);
            if (stored is not { Status: Models.Projects.ProjectStatus.AwaitingVerification or Models.Projects.ProjectStatus.Verified
                    or Models.Projects.ProjectStatus.Completed or Models.Projects.ProjectStatus.Failed })
                return null;

            var was = stored.Status;
            stored.Status = Models.Projects.ProjectStatus.InProgress;
            _projectRepository!.Update(stored);
            _loggerFactory?.CreateLogger<DrakeFactory>().LogInformation("Project {ProjectId} reopened from {Status} to InProgress for a retried task", projectId, was);
            return was;
        }

        private async Task<TaskRetry> ResetFailedTaskAsync(Models.Projects.Project project, string taskId)
        {
            foreach (var (drake, _) in GetDrakesForProject(project.Id))
            {
                var task = drake.GetAllTasks().FirstOrDefault(t => t.Id == taskId);
                if (task == null)
                    continue;
                if (task.Status != TaskStatus.Failed)
                    return new TaskRetry(TaskRetryOutcome.NotFailed, task.Status);
                drake.UpdateTask(task, TaskStatus.Unassigned); // clears the error and queues the file save
                var kobold = drake.GetKoboldForTask(taskId);
                if (kobold != null)
                    drake.UnsummonKobold(kobold.Id);
                await drake.UpdateTasksFileAsync();
                return new TaskRetry(TaskRetryOutcome.Retried, TaskStatus.Unassigned);
            }

            foreach (var filePath in project.Paths.TaskFiles.Values)
            {
                var tracker = new TaskTracker();
                tracker.LoadFromFile(filePath);
                var task = tracker.GetTaskById(taskId);
                if (task == null)
                    continue;
                if (task.Status != TaskStatus.Failed)
                    return new TaskRetry(TaskRetryOutcome.NotFailed, task.Status);
                // Attached after loading, so the load adds no rows and only this change reaches the store.
                tracker.Repository = _taskRepository;
                tracker.ProjectId = project.Id;
                tracker.AreaName = ExtractAreaName(filePath);
                tracker.UpdateTask(task, TaskStatus.Unassigned);
                tracker.ClearError(task);
                await tracker.SaveToFileAsync(filePath);
                if (_taskRepository != null)
                {
                    await _taskRepository.ClearErrorAsync(taskId);
                    await _taskRepository.SetStatusAsync(taskId, TaskStatus.Unassigned);
                }
                return new TaskRetry(TaskRetryOutcome.Retried, TaskStatus.Unassigned);
            }

            var row = _taskRepository == null ? null : await _taskRepository.GetByIdAsync(taskId);
            if (row == null || row.ProjectId != project.Id)
                return new TaskRetry(TaskRetryOutcome.NotFound, null);
            if (row.Status != TaskStatus.Failed)
                return new TaskRetry(TaskRetryOutcome.NotFailed, row.Status);
            await _taskRepository!.ClearErrorAsync(taskId);
            await _taskRepository.SetStatusAsync(taskId, TaskStatus.Unassigned);
            return new TaskRetry(TaskRetryOutcome.Retried, TaskStatus.Unassigned);
        }

        /// <summary>
        /// Sets a task's priority wherever it lives (TASK-154), like <see cref="RetryFailedTaskAsync"/>: in a Drake of the
        /// project (its task file saved), else in its task file, else only in the task store; the store row is updated
        /// before this returns, so a read right after sees it. A Done task is left alone.
        /// </summary>
        public async Task<TaskPriorityChange> SetTaskPriorityAsync(Models.Projects.Project project, string taskId, TaskPriority priority)
        {
            foreach (var (drake, _) in GetDrakesForProject(project.Id))
            {
                var task = drake.GetAllTasks().FirstOrDefault(t => t.Id == taskId);
                if (task == null)
                    continue;
                if (task.Status == TaskStatus.Done)
                    return new(TaskPriorityOutcome.AlreadyDone, task, task.Priority);
                var previous = task.Priority;
                drake.SetTaskPriority(task, priority);
                await drake.UpdateTasksFileAsync();
                await SetStoredPriorityAsync(taskId, priority);
                return new(TaskPriorityOutcome.Set, task, previous);
            }

            foreach (var filePath in project.Paths.TaskFiles.Values)
            {
                var tracker = new TaskTracker();
                tracker.LoadFromFile(filePath);
                var task = tracker.GetTaskById(taskId);
                if (task == null)
                    continue;
                if (task.Status == TaskStatus.Done)
                    return new(TaskPriorityOutcome.AlreadyDone, task, task.Priority);
                var previous = task.Priority;
                tracker.SetPriority(task, priority);
                await tracker.SaveToFileAsync(filePath);
                await SetStoredPriorityAsync(taskId, priority);
                return new(TaskPriorityOutcome.Set, task, previous);
            }

            var row = _taskRepository == null ? null : await _taskRepository.GetByIdAsync(taskId);
            if (row == null || row.ProjectId != project.Id)
                return new(TaskPriorityOutcome.NotFound);
            if (row.Status == TaskStatus.Done)
                return new(TaskPriorityOutcome.AlreadyDone, row, row.Priority);
            var before = row.Priority;
            row.Priority = priority;
            await _taskRepository!.UpdateTaskAsync(row);
            return new(TaskPriorityOutcome.Set, row, before);
        }

        private async Task SetStoredPriorityAsync(string taskId, TaskPriority priority)
        {
            if (_taskRepository == null)
                return;
            var row = await _taskRepository.GetByIdAsync(taskId);
            if (row == null || row.Priority == priority)
                return;
            row.Priority = priority;
            await _taskRepository.UpdateTaskAsync(row);
        }

        /// <summary>
        /// Extracts the area name from a task file path.
        /// e.g., "C:\projects\myapp\tasks\frontend-tasks.md" → "frontend"
        /// </summary>
        private static string? ExtractAreaName(string taskFilePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(taskFilePath);
            // Remove "-tasks" suffix if present
            if (fileName.EndsWith("-tasks", StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^6];
            }
            return fileName;
        }
    }
}
