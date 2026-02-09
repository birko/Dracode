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

        // Throttle concurrent project processing to avoid overwhelming LLM providers
        private readonly SemaphoreSlim _projectThrottle;
        private const int MaxConcurrentProjects = 5;

        public WyvernProcessingService(
            ILogger<WyvernProcessingService> logger,
            ProjectService projectService,
            int checkIntervalSeconds = 60)
        {
            _logger = logger;
            _projectService = projectService;
            _checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
            _isRunning = false;
            _projectThrottle = new SemaphoreSlim(MaxConcurrentProjects, MaxConcurrentProjects);
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

            // Process projects that need analysis (WyrmAssigned status from WyrmProcessingService)
            var projectsNeedingAnalysis = _projectService.GetProjectsByStatus(ProjectStatus.WyrmAssigned)
                .Where(p => _projectService.IsAgentEnabled(p.Id, "wyvern"))
                .ToList();

            var skippedAnalysis = _projectService.GetProjectsByStatus(ProjectStatus.WyrmAssigned)
                .Except(projectsNeedingAnalysis)
                .ToList();
            foreach (var project in skippedAnalysis)
            {
                _logger.LogInformation("⏭️ Skipping analysis for project {ProjectName} - Wyvern disabled", project.Name);
            }

            if (projectsNeedingAnalysis.Count > 0)
            {
                _logger.LogInformation("🔍 Analyzing {Count} project(s) (max {Max} concurrent)", projectsNeedingAnalysis.Count, MaxConcurrentProjects);
                var analysisTasks = projectsNeedingAnalysis.Select(async project =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await _projectThrottle.WaitAsync(cancellationToken);
                    try
                    {
                        await AnalyzeProjectAsync(project);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to analyze project: {ProjectId}", project.Id);
                    }
                    finally
                    {
                        _projectThrottle.Release();
                    }
                });
                await Task.WhenAll(analysisTasks);
            }

            // Process projects with modified specifications (in parallel)
            var projectsWithModifiedSpecs = _projectService.GetProjectsByStatus(ProjectStatus.SpecificationModified)
                .Where(p => _projectService.IsAgentEnabled(p.Id, "wyvern"))
                .ToList();

            var skippedReanalysis = _projectService.GetProjectsByStatus(ProjectStatus.SpecificationModified)
                .Except(projectsWithModifiedSpecs)
                .ToList();
            foreach (var project in skippedReanalysis)
            {
                _logger.LogInformation("⏭️ Skipping reanalysis for project {ProjectName} - Wyvern disabled", project.Name);
            }

            if (projectsWithModifiedSpecs.Count > 0)
            {
                _logger.LogInformation("🔄 Reanalyzing {Count} modified project(s) (max {Max} concurrent)", projectsWithModifiedSpecs.Count, MaxConcurrentProjects);
                var reanalysisTasks = projectsWithModifiedSpecs.Select(async project =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await _projectThrottle.WaitAsync(cancellationToken);
                    try
                    {
                        await ReanalyzeModifiedProjectAsync(project);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reanalyze modified project: {ProjectId}", project.Id);
                    }
                    finally
                    {
                        _projectThrottle.Release();
                    }
                });
                await Task.WhenAll(reanalysisTasks);
            }

            // Log statistics
            var stats = _projectService.GetStatistics();
            _logger.LogInformation("📊 {Stats}", stats);
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

            // Transition back to WyrmAssigned so normal analysis flow can pick it up
            // This allows Wyvern to re-analyze with potentially updated Wyrm recommendations
            _projectService.UpdateProjectStatus(project.Id, ProjectStatus.WyrmAssigned);

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
