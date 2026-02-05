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
        private bool _isRunning;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new Drake execution service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="projectService">Project service for accessing projects</param>
        /// <param name="drakeFactory">Factory for creating Drakes</param>
        /// <param name="executionIntervalSeconds">Interval in seconds between execution cycles (default: 30)</param>
        public DrakeExecutionService(
            ILogger<DrakeExecutionService> logger,
            ProjectService projectService,
            DrakeFactory drakeFactory,
            int executionIntervalSeconds = 30)
        {
            _logger = logger;
            _projectService = projectService;
            _drakeFactory = drakeFactory;
            _executionInterval = TimeSpan.FromSeconds(executionIntervalSeconds);
            _isRunning = false;
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

            // Find projects that are analyzed or in progress
            var projectsToProcess = allProjects
                .Where(p => p.Status == ProjectStatus.Analyzed || p.Status == ProjectStatus.InProgress)
                .ToList();

            if (projectsToProcess.Count == 0)
            {
                _logger.LogDebug("No analyzed or in-progress projects to process");
                return;
            }

            _logger.LogInformation("🔄 Processing {Count} project(s) in parallel", projectsToProcess.Count);

            // Process all projects in parallel
            var projectTasks = projectsToProcess.Select(async project =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    await ProcessProjectAsync(project, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing project {ProjectName}", project.Name);
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

            if (project.TaskFiles == null || project.TaskFiles.Count == 0)
            {
                _logger.LogWarning("Project {ProjectName} has no task files", project.Name);
                return drakes;
            }

            foreach (var (areaName, taskFilePath) in project.TaskFiles)
            {
                // Paths are pre-resolved by ProjectRepository, normalize for safety
                var normalizedPath = Path.GetFullPath(taskFilePath);
                var drakeName = $"{project.Name}:{areaName}";

                // Check if Drake already exists
                var existingDrake = _drakeFactory.GetDrake(drakeName);
                if (existingDrake != null)
                {
                    drakes.Add((existingDrake, drakeName));
                    continue;
                }

                // Check if we can create a new Drake
                if (!_drakeFactory.CanCreateDrakeForProject(project.Id))
                {
                    _logger.LogDebug("Cannot create Drake for {DrakeName} - project at Drake limit", drakeName);
                    continue;
                }

                // Verify task file exists
                if (!File.Exists(normalizedPath))
                {
                    _logger.LogWarning("Task file not found: {TaskFilePath}", normalizedPath);
                    continue;
                }

                try
                {
                    var drake = _drakeFactory.CreateDrake(
                        taskFilePath: normalizedPath,
                        drakeName: drakeName,
                        specificationPath: project.SpecificationPath,
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
            var stats = drake.GetStatistics();

            _logger.LogDebug("Drake stats - Tasks: {Total} (Unassigned: {Unassigned}, Working: {Working}, Done: {Done})",
                stats.TotalTasks, stats.UnassignedTasks, stats.WorkingTasks, stats.DoneTasks);

            // Skip if no unassigned tasks
            if (stats.UnassignedTasks == 0)
            {
                return;
            }

            // Get unassigned tasks from the tracker
            var allTasks = drake.GetStatistics();

            // We need to access tasks through the Drake - get tasks that need processing
            // For now, we'll use ExecuteTaskAsync which handles summoning and execution

            // Find tasks that are unassigned and ready (no pending dependencies)
            var tasksToExecute = GetReadyTasks(drake);

            // Execute all ready tasks in parallel (Kobold limits are enforced by the factory)
            var taskExecutions = tasksToExecute.Select(async item =>
            {
                var (task, agentType) = item;

                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    _logger.LogInformation("🚀 Starting task {TaskId} with agent {AgentType}",
                        task.Id[..Math.Min(8, task.Id.Length)], agentType);

                    // Execute the task (this summons a Kobold and runs it)
                    var result = await drake.ExecuteTaskAsync(
                        task,
                        agentType,
                        maxIterations: 30,
                        messageCallback: (level, msg) => _logger.LogInformation("[{Level}] {Message}", level, msg)
                    );

                    if (result == null)
                    {
                        _logger.LogDebug("Task {TaskId} deferred - Kobold limit reached", task.Id[..Math.Min(8, task.Id.Length)]);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to execute task {TaskId}", task.Id);
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
            var totalTasks = 0;
            var doneTasks = 0;
            var drakesToRemove = new List<string>();

            foreach (var (drake, drakeName) in drakesWithNames)
            {
                var stats = drake.GetStatistics();
                totalTasks += stats.TotalTasks;
                doneTasks += stats.DoneTasks;

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
                _drakeFactory.RemoveDrake(drakeName);
                _logger.LogInformation("🗑️ Removed completed Drake {DrakeName}", drakeName);
            }

            // Check if entire project is complete
            if (totalTasks > 0 && doneTasks == totalTasks)
            {
                _projectService.UpdateProjectStatus(project.Id, ProjectStatus.Completed);
                _logger.LogInformation("✅ Project {ProjectName} completed! All {Count} tasks done.",
                    project.Name, totalTasks);
            }
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
