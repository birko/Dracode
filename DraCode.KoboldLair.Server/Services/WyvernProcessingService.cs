using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors for new project specifications and processes them with Wyverns.
    /// Runs every minute to check for projects needing processing.
    /// </summary>
    public class WyvernProcessingService : BackgroundService
    {
        private readonly ILogger<WyvernProcessingService> _logger;
        private readonly ProjectService _projectService;
        private readonly TimeSpan _checkInterval;
        private bool _isRunning;
        private readonly object _lock = new object();

        public WyvernProcessingService(
            ILogger<WyvernProcessingService> logger,
            ProjectService projectService,
            int checkIntervalSeconds = 60)
        {
            _logger = logger;
            _projectService = projectService;
            _checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
            _isRunning = false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🐉 Wyvern Processing Service started. Interval: {Interval}s",
                _checkInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for the interval before next run
                    await Task.Delay(_checkInterval, stoppingToken);

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
                        _logger.LogWarning("⚠️ Previous Wyvern processing job still running, skipping this cycle");
                        continue;
                    }

                    // Process specifications
                    await ProcessSpecificationsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in Wyvern processing cycle");
                }
                finally
                {
                    // Mark job as complete
                    lock (_lock)
                    {
                        _isRunning = false;
                    }
                }
            }

            _logger.LogInformation("🛑 Wyvern Processing Service stopped");
        }

        /// <summary>
        /// Processes all projects by iterating through the project repository
        /// </summary>
        private async Task ProcessSpecificationsAsync(CancellationToken cancellationToken)
        {
            var allProjects = _projectService.GetAllProjects();
            if (allProjects.Count == 0)
            {
                _logger.LogDebug("No projects registered");
                return;
            }

            _logger.LogInformation("📋 Found {Count} project(s)", allProjects.Count);

            // Process projects that need Wyrm assignment
            var projectsNeedingWyrm = _projectService.GetProjectsByStatus(ProjectStatus.New);
            foreach (var project in projectsNeedingWyrm)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Check if Wyrm is enabled for this project
                if (!_projectService.IsAgentEnabled(project.Id, "wyvern"))
                {
                    _logger.LogInformation("⏭️ Skipping project {ProjectName} - Wyvern disabled", project.Name);
                    continue;
                }

                try
                {
                    await AssignWyvernToProjectAsync(project);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to assign Wyvern to project: {ProjectId}", project.Id);
                }
            }

            // Process projects that need analysis
            var projectsNeedingAnalysis = _projectService.GetProjectsByStatus(ProjectStatus.WyvernAssigned);
            foreach (var project in projectsNeedingAnalysis)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Check if Wyvern is enabled for this project
                if (!_projectService.IsAgentEnabled(project.Id, "wyvern"))
                {
                    _logger.LogInformation("⏭️ Skipping analysis for project {ProjectName} - Wyvern disabled", project.Name);
                    continue;
                }

                try
                {
                    await AnalyzeProjectAsync(project);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to analyze project: {ProjectId}", project.Id);
                }
            }

            // Process projects with modified specifications (reprocessing)
            var projectsWithModifiedSpecs = _projectService.GetProjectsByStatus(ProjectStatus.SpecificationModified);
            foreach (var project in projectsWithModifiedSpecs)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Check if Wyvern is enabled for this project
                if (!_projectService.IsAgentEnabled(project.Id, "wyvern"))
                {
                    _logger.LogInformation("⏭️ Skipping reanalysis for project {ProjectName} - Wyvern disabled", project.Name);
                    continue;
                }

                try
                {
                    await ReanalyzeModifiedProjectAsync(project);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reanalyze modified project: {ProjectId}", project.Id);
                }
            }

            // Log statistics
            var stats = _projectService.GetStatistics();
            _logger.LogInformation("📊 {Stats}", stats);
        }

        /// <summary>
        /// Assigns a Wyvern to a project
        /// </summary>
        private async Task AssignWyvernToProjectAsync(Project project)
        {
            _logger.LogInformation("🐲 Assigning Wyvern to project: {ProjectName}", project.Name);

            var wyvern = await _projectService.AssignWyvernAsync(project.Id);

            _logger.LogInformation("✅ Wyvern assigned to project: {ProjectName}", project.Name);
        }

        /// <summary>
        /// Runs Wyvern analysis on a project
        /// </summary>
        private async Task AnalyzeProjectAsync(Project project)
        {
            _logger.LogInformation("🔍 Starting Wyvern analysis for project: {ProjectName}", project.Name);

            var analysis = await _projectService.AnalyzeProjectAsync(project.Id);

            _logger.LogInformation("✅ Wyvern analysis completed for project: {ProjectName}. Total tasks: {TaskCount}",
                project.Name, analysis.TotalTasks);
        }

        /// <summary>
        /// Reanalyzes a project whose specification was modified.
        /// First transitions back to WyvernAssigned, then runs normal analysis.
        /// </summary>
        private async Task ReanalyzeModifiedProjectAsync(Project project)
        {
            _logger.LogInformation("🔄 Reanalyzing modified specification for project: {ProjectName}", project.Name);

            // Transition to WyvernAssigned so normal analysis flow can pick it up
            _projectService.UpdateProjectStatus(project.Id, ProjectStatus.WyvernAssigned);

            // Run analysis (which will create new tasks for changes)
            var analysis = await _projectService.AnalyzeProjectAsync(project.Id);

            _logger.LogInformation("✅ Reanalysis completed for project: {ProjectName}. Total tasks: {TaskCount}",
                project.Name, analysis.TotalTasks);
        }

        /// <summary>
        /// Gets the current status of the processing service
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
