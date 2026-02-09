using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that picks up analyzed projects, creates Drakes for each task file,
    /// and starts Kobolds to execute tasks. This bridges the gap between Wyvern analysis
    /// and actual task execution.
    /// </summary>
    public class DrakeExecutionService : BackgroundService
    {
        private readonly ILogger<DrakeExecutionService> _logger;
        private readonly ProjectService _projectService;
        private readonly DrakeFactory _drakeFactory;
        private readonly TimeSpan _executionInterval;
        private readonly int _maxKoboldIterations;
        private bool _isRunning;
        private readonly object _lock = new object();

        // Throttle concurrent project processing to avoid overwhelming resources
        private readonly SemaphoreSlim _projectThrottle;
        private const int MaxConcurrentProjects = 5;

        /// <summary>
        /// Creates a new Drake execution service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="projectService">Project service for accessing projects</param>
        /// <param name="drakeFactory">Factory for creating Drakes</param>
        /// <param name="executionIntervalSeconds">Interval in seconds between execution cycles (default: 30)</param>
        /// <param name="maxKoboldIterations">Maximum iterations for Kobold execution (default: 100)</param>
        public DrakeExecutionService(
            ILogger<DrakeExecutionService> logger,
            ProjectService projectService,
            DrakeFactory drakeFactory,
            int executionIntervalSeconds = 30,
            int maxKoboldIterations = 100)
        {
            _logger = logger;
            _projectService = projectService;
            _drakeFactory = drakeFactory;
            _executionInterval = TimeSpan.FromSeconds(executionIntervalSeconds);
            _maxKoboldIterations = maxKoboldIterations;
            _isRunning = false;
            _projectThrottle = new SemaphoreSlim(MaxConcurrentProjects, MaxConcurrentProjects);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "🐲 Drake Execution Service started. Interval: {Interval}s",
                _executionInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for the interval before next run
                    await Task.Delay(_executionInterval, stoppingToken);

                    // Check if previous job is still running
                    bool canRun;
                    lock (_lock)
                    {
                        canRun = !_isRunning;
                        if (canRun)
                        {
                            _isRunning = true;
                        }
                    }

                    if (!canRun)
                    {
                        _logger.LogDebug("⚠️ Previous execution cycle still running, skipping");
                        continue;
                    }

                    // Execute the main cycle
                    await ExecuteCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in Drake execution cycle");
                }
                finally
                {
                    lock (_lock)
                    {
                        _isRunning = false;
                    }
                }
            }

            _logger.LogInformation("🛑 Drake Execution Service stopped");
        }

        /// <summary>
        /// Executes a single cycle: find analyzed projects, create Drakes, execute tasks
        /// </summary>
        private async Task ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            // Get all projects
            var allProjects = _projectService.GetAllProjects();

            // Find projects that are analyzed or in progress AND actively running
            var projectsToProcess = allProjects
                .Where(p => (p.Status == ProjectStatus.Analyzed || p.Status == ProjectStatus.InProgress) 
                            && p.ExecutionState == ProjectExecutionState.Running)
                .ToList();

            // Log skipped projects with non-Running execution states
            var skippedProjects = allProjects
                .Where(p => (p.Status == ProjectStatus.Analyzed || p.Status == ProjectStatus.InProgress)
                            && p.ExecutionState != ProjectExecutionState.Running)
                .ToList();

            if (skippedProjects.Count > 0)
            {
                foreach (var skipped in skippedProjects)
                {
                    _logger.LogDebug("⏸️ Skipping project {ProjectName} - execution state: {State}", 
                        skipped.Name, skipped.ExecutionState);
                }
            }

            if (projectsToProcess.Count == 0)
            {
                _logger.LogDebug("No analyzed or in-progress projects to process");
                return;
            }

            _logger.LogInformation("🔄 Processing {Count} project(s) (max {Max} concurrent)", projectsToProcess.Count, MaxConcurrentProjects);

            // Process projects with throttled parallelism
            var projectTasks = projectsToProcess.Select(async project =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await _projectThrottle.WaitAsync(cancellationToken);
                try
                {
                    await ProcessProjectAsync(project, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing project {ProjectName}", project.Name);
                }
                finally
                {
                    _projectThrottle.Release();
                }
            });

            await Task.WhenAll(projectTasks);
        }

        /// <summary>
        /// Processes a single project: creates Drakes if needed and executes tasks
        /// </summary>
        private async Task ProcessProjectAsync(Project project, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Processing project: {ProjectName} (Status: {Status})", project.Name, project.Status);

            // Ensure Drakes exist for all task files (returns drake name with drake)
            var drakesWithNames = EnsureDrakesForProject(project);

            if (drakesWithNames.Count == 0)
            {
                _logger.LogWarning("No Drakes created for project {ProjectName} - no task files found", project.Name);
                return;
            }

            // Transition to InProgress if currently Analyzed
            if (project.Status == ProjectStatus.Analyzed)
            {
                _projectService.UpdateProjectStatus(project.Id, ProjectStatus.InProgress);
                _logger.LogInformation("📋 Project {ProjectName} transitioned to InProgress", project.Name);
            }

            // Process all Drakes in parallel - find unassigned tasks and execute them
            var drakeTasks = drakesWithNames.Select(item =>
                ProcessDrakeTasksAsync(item.Drake, project, cancellationToken));

            await Task.WhenAll(drakeTasks);

            // Check if all tasks are complete and clean up finished Drakes
            await CheckProjectCompletionAsync(project, drakesWithNames);
        }

        /// <summary>
        /// Ensures Drakes exist for all task files in the project
        /// </summary>
        /// <returns>List of tuples containing the Drake and its name for tracking</returns>
        private List<(Drake Drake, string Name)> EnsureDrakesForProject(Project project)
        {
            var drakes = new List<(Drake Drake, string Name)>();

            if (project.Paths.TaskFiles == null || project.Paths.TaskFiles.Count == 0)
            {
                _logger.LogWarning("Project {ProjectName} has no task files", project.Name);
                return drakes;
            }

            var currentDrakeCount = _drakeFactory.GetActiveDrakeCountForProject(project.Id);
            _logger.LogDebug("Project {ProjectName} currently has {Count} active Drake(s)", project.Name, currentDrakeCount);

            foreach (var (areaName, taskFilePath) in project.Paths.TaskFiles)
            {
                // Paths are pre-resolved by ProjectRepository, normalize for safety
                var normalizedPath = Path.GetFullPath(taskFilePath);
                var drakeName = $"{project.Name}:{areaName}";

                // Check if Drake already exists
                var existingDrake = _drakeFactory.GetDrake(drakeName);
                if (existingDrake != null)
                {
                    _logger.LogDebug("Using existing Drake {DrakeName}", drakeName);
                    drakes.Add((existingDrake, drakeName));
                    continue;
                }

                // Verify task file exists
                if (!File.Exists(normalizedPath))
                {
                    _logger.LogWarning("Task file not found: {TaskFilePath}", normalizedPath);
                    continue;
                }

                // Check if this area is already complete (all tasks done)
                if (IsAreaComplete(normalizedPath))
                {
                    _logger.LogDebug("Skipping Drake creation for {DrakeName} - all tasks already complete", drakeName);
                    continue;
                }

                // Check if we can create a new Drake
                if (!_drakeFactory.CanCreateDrakeForProject(project.Id))
                {
                    _logger.LogDebug("Cannot create Drake for {DrakeName} - project at Drake limit (current: {Current})", 
                        drakeName, currentDrakeCount);
                    continue;
                }

                try
                {
                    var drake = _drakeFactory.CreateDrake(
                        taskFilePath: normalizedPath,
                        drakeName: drakeName,
                        specificationPath: project.Paths.Specification,
                        projectId: project.Id
                    );

                    drakes.Add((drake, drakeName));
                    _logger.LogInformation("🐉 Created Drake {DrakeName} for project {ProjectName}",
                        drakeName, project.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create Drake {DrakeName}", drakeName);
                }
            }

            return drakes;
        }

        /// <summary>
        /// Processes tasks for a single Drake - finds unassigned tasks and starts Kobolds
        /// </summary>
        private async Task ProcessDrakeTasksAsync(Drake drake, Project project, CancellationToken cancellationToken)
        {
            // Reload tasks from file to get current state (fixes stale Drake reuse bug)
            await drake.ReloadTasksFromFileAsync();

            var stats = drake.GetStatistics();

            _logger.LogDebug(
                "Drake stats for project {ProjectName}\n" +
                "  Tasks: {Total} (Unassigned: {Unassigned}, Working: {Working}, Done: {Done}, Failed: {Failed}, Blocked: {Blocked})",
                project.Name, stats.TotalTasks, stats.UnassignedTasks, stats.WorkingTasks, stats.DoneTasks, stats.FailedTasks, stats.BlockedTasks);

            // **CRITICAL: Stop processing if any tasks have failed**
            if (stats.FailedTasks > 0)
            {
                _logger.LogWarning(
                    "⛔ Project {ProjectName} has {FailedCount} failed task(s). Halting execution until errors are resolved.\n" +
                    "  Project ID: {ProjectId}",
                    project.Name, stats.FailedTasks, project.Id);
                
                // Log details about failed tasks
                var failedTasks = drake.GetAllTasks().Where(t => t.Status == TaskStatus.Failed).ToList();
                foreach (var task in failedTasks)
                {
                    var taskPreview = task.Task.Length > 60 ? task.Task.Substring(0, 60) + "..." : task.Task;
                    _logger.LogError(
                        "❌ Failed task in project {ProjectName}\n" +
                        "  Task ID: {TaskId}\n" +
                        "  Task: {Task}\n" +
                        "  Error: {Error}",
                        project.Name,
                        task.Id[..Math.Min(8, task.Id.Length)], 
                        taskPreview,
                        task.ErrorMessage ?? "No error message");
                }

                // Log blocked tasks
                if (stats.BlockedTasks > 0)
                {
                    _logger.LogWarning(
                        "🟠 Project {ProjectName}: {BlockedCount} task(s) blocked by failed dependencies",
                        project.Name, stats.BlockedTasks);
                }
                
                return; // Stop processing this Drake until failed tasks are resolved
            }

            // Skip if no unassigned tasks
            if (stats.UnassignedTasks == 0)
            {
                _logger.LogDebug("No unassigned tasks for this Drake");
                return;
            }

            // Find tasks that are unassigned and ready (no pending dependencies)
            var tasksToExecute = GetReadyTasks(drake);
            
            if (tasksToExecute.Count == 0)
            {
                _logger.LogDebug("No ready tasks to execute (all unassigned tasks have unmet dependencies)");
                return;
            }

            _logger.LogInformation("Found {Count} ready task(s) to execute in project {ProjectName}", tasksToExecute.Count, project.Name);

            // Execute all ready tasks in parallel (Kobold limits are enforced by the factory)
            var taskExecutions = tasksToExecute.Select(async item =>
            {
                var (task, agentType) = item;

                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    var taskPreview = task.Task.Length > 60 ? task.Task.Substring(0, 60) + "..." : task.Task;
                    _logger.LogInformation(
                        "🚀 Starting task execution\n" +
                        "  Project: {ProjectName}\n" +
                        "  Task ID: {TaskId}\n" +
                        "  Agent: {AgentType}\n" +
                        "  Task: {TaskDescription}",
                        project.Name,
                        task.Id[..Math.Min(8, task.Id.Length)],
                        agentType,
                        taskPreview);

                    // Execute the task (this summons a Kobold and runs it)
                    var result = await drake.ExecuteTaskAsync(
                        task,
                        agentType,
                        maxIterations: _maxKoboldIterations,
                        messageCallback: (level, msg) => _logger.LogInformation("[{Level}] {Message}", level, msg)
                    );

                    if (result == null)
                    {
                        _logger.LogDebug(
                            "Task {TaskId} deferred - Kobold limit reached for project {ProjectName}", 
                            task.Id[..Math.Min(8, task.Id.Length)],
                            project.Name);
                    }
                }
                catch (Exception ex)
                {
                    var taskPreview = task.Task.Length > 60 ? task.Task.Substring(0, 60) + "..." : task.Task;
                    _logger.LogError(ex, 
                        "❌ Failed to execute task\n" +
                        "  Project: {ProjectName}\n" +
                        "  Task ID: {TaskId}\n" +
                        "  Task: {TaskDescription}",
                        project.Name,
                        task.Id,
                        taskPreview);
                }
            });

            await Task.WhenAll(taskExecutions);
        }

        /// <summary>
        /// Gets tasks that are ready to execute (unassigned and dependencies met)
        /// </summary>
        private List<(TaskRecord Task, string AgentType)> GetReadyTasks(Drake drake)
        {
            return drake.GetUnassignedTasks();
        }

        /// <summary>
        /// Checks if all tasks in the project are complete and updates status.
        /// Also removes completed Drakes to free up resources for other areas.
        /// </summary>
        private async Task CheckProjectCompletionAsync(Project project, List<(Drake Drake, string Name)> drakesWithNames)
        {
            var drakesToRemove = new List<string>();

            // Check active Drakes for cleanup
            foreach (var (drake, drakeName) in drakesWithNames)
            {
                var stats = drake.GetStatistics();

                // Check if this individual Drake has completed all its tasks
                // Only remove if all tasks are done and no Kobolds are still working
                if (stats.TotalTasks > 0 && stats.DoneTasks == stats.TotalTasks && stats.WorkingTasks == 0)
                {
                    drakesToRemove.Add(drakeName);
                }
            }

            // Remove completed Drakes to free up resources
            foreach (var drakeName in drakesToRemove)
            {
                var removed = _drakeFactory.RemoveDrake(drakeName);
                if (removed)
                {
                    var remainingCount = _drakeFactory.GetActiveDrakeCountForProject(project.Id);
                    _logger.LogInformation(
                        "🗑️ Removed completed Drake\n" +
                        "  Project: {ProjectName}\n" +
                        "  Drake: {DrakeName}\n" +
                        "  Remaining Drakes: {Count}", 
                        project.Name, drakeName, remainingCount);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to remove Drake {DrakeName} for project {ProjectName}", 
                        drakeName, project.Name);
                }
            }

            // Count tasks from ALL task files, not just active Drakes
            // This ensures we don't mark a project complete if some areas have no Drake yet
            var (totalTasks, doneTasks) = CountTasksFromAllFiles(project);

            // Check if entire project is complete
            if (totalTasks > 0 && doneTasks == totalTasks)
            {
                _projectService.UpdateProjectStatus(project.Id, ProjectStatus.Completed);
                _logger.LogInformation(
                    "✅ Project completed!\n" +
                    "  Project: {ProjectName}\n" +
                    "  Project ID: {ProjectId}\n" +
                    "  Total Tasks: {Count}",
                    project.Name, project.Id, totalTasks);
            }
            else
            {
                _logger.LogDebug(
                    "Project {ProjectName} progress: {Done}/{Total} tasks done",
                    project.Name, doneTasks, totalTasks);
            }
        }

        /// <summary>
        /// Checks if all tasks in an area are complete
        /// </summary>
        private bool IsAreaComplete(string taskFilePath)
        {
            try
            {
                var tracker = new TaskTracker();
                var loaded = tracker.LoadFromFile(taskFilePath);

                if (loaded == 0)
                {
                    return false;
                }

                var tasks = tracker.GetAllTasks();
                return tasks.Count > 0 && tasks.All(t => t.Status == TaskStatus.Done);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check completion status for {Path}", taskFilePath);
                return false;
            }
        }

        /// <summary>
        /// Counts total and completed tasks from all task files in the project.
        /// This reads directly from the task files to ensure accurate counts
        /// even for areas without an active Drake.
        /// </summary>
        private (int Total, int Done) CountTasksFromAllFiles(Project project)
        {
            var totalTasks = 0;
            var doneTasks = 0;

            if (project.Paths.TaskFiles == null || project.Paths.TaskFiles.Count == 0)
            {
                return (0, 0);
            }

            foreach (var (areaName, taskFilePath) in project.Paths.TaskFiles)
            {
                var normalizedPath = Path.GetFullPath(taskFilePath);

                if (!File.Exists(normalizedPath))
                {
                    _logger.LogWarning("Task file not found when checking completion: {Path}", normalizedPath);
                    continue;
                }

                try
                {
                    var tracker = new TaskTracker();
                    var loaded = tracker.LoadFromFile(normalizedPath);

                    if (loaded > 0)
                    {
                        var tasks = tracker.GetAllTasks();
                        totalTasks += tasks.Count;
                        doneTasks += tasks.Count(t => t.Status == TaskStatus.Done);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read task file {Path} for completion check", normalizedPath);
                }
            }

            return (totalTasks, doneTasks);
        }

        /// <summary>
        /// Gets the current status of the execution service
        /// </summary>
        public bool IsCurrentlyRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
        }
    }
}
