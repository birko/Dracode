using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors for new project specifications and processes them with Wyverns.
    /// Runs every minute to check for projects needing processing.
    /// </summary>
    public class WyvernProcessingService : PeriodicBackgroundService
    {
        private readonly ILogger<WyvernProcessingService> _logger;
        private readonly ProjectService _projectService;

        // Throttle concurrent project processing to avoid overwhelming LLM providers
        private readonly SemaphoreSlim _projectThrottle;
        private const int MaxConcurrentProjects = 5;

        protected override ILogger Logger => _logger;

        public WyvernProcessingService(
            ILogger<WyvernProcessingService> logger,
            ProjectService projectService,
            int checkIntervalSeconds = 60)
            : base(TimeSpan.FromSeconds(checkIntervalSeconds))
        {
            _logger = logger;
            _projectService = projectService;
            _projectThrottle = new SemaphoreSlim(MaxConcurrentProjects, MaxConcurrentProjects);
        }

        /// <summary>
        /// Processes all projects by iterating through the project repository
        /// </summary>
        protected override async Task ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            var allProjects = _projectService.GetAllProjects();
            if (allProjects.Count == 0)
            {
                _logger.LogDebug("No projects registered");
                return;
            }

            _logger.LogInformation("📋 Found {Count} project(s)", allProjects.Count);

            // Process projects that need analysis (WyrmAssigned status from WyrmProcessingService)
            var wyrmAssigned = _projectService.GetProjectsByStatus(ProjectStatus.WyrmAssigned);

            // Skip projects that are not in Running execution state
            var pausedProjects = wyrmAssigned.Where(p => p.ExecutionState != ProjectExecutionState.Running).ToList();
            foreach (var project in pausedProjects)
            {
                _logger.LogDebug("Skipping project {ProjectName} - execution state: {State}", project.Name, project.ExecutionState);
            }

            var projectsNeedingAnalysis = wyrmAssigned
                .Where(p => p.ExecutionState == ProjectExecutionState.Running)
                .Where(p => _projectService.IsAgentEnabled(p.Id, "wyvern"))
                .ToList();

            var skippedAnalysis = wyrmAssigned
                .Where(p => p.ExecutionState == ProjectExecutionState.Running)
                .Where(p => !_projectService.IsAgentEnabled(p.Id, "wyvern"))
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
            var modifiedSpecs = _projectService.GetProjectsByStatus(ProjectStatus.SpecificationModified);

            var pausedModified = modifiedSpecs.Where(p => p.ExecutionState != ProjectExecutionState.Running).ToList();
            foreach (var project in pausedModified)
            {
                _logger.LogDebug("Skipping reanalysis for {ProjectName} - execution state: {State}", project.Name, project.ExecutionState);
            }

            var projectsWithModifiedSpecs = modifiedSpecs
                .Where(p => p.ExecutionState == ProjectExecutionState.Running)
                .Where(p => _projectService.IsAgentEnabled(p.Id, "wyvern"))
                .ToList();

            var skippedReanalysis = modifiedSpecs
                .Where(p => p.ExecutionState == ProjectExecutionState.Running)
                .Where(p => !_projectService.IsAgentEnabled(p.Id, "wyvern"))
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
        /// Runs Wyvern analysis on a project with timing
        /// </summary>
        private async Task AnalyzeProjectAsync(Project project)
        {
            _logger.LogInformation("[Wyvern] START {ProjectName} | Beginning analysis", project.Name);

            var analysisStart = DateTime.UtcNow;
            var analysis = await _projectService.AnalyzeProjectAsync(project.Id);
            var analysisDuration = DateTime.UtcNow - analysisStart;

            _logger.LogInformation("[Wyvern] COMPLETE {ProjectName} | Duration: {Duration}ms | Tasks: {TaskCount}",
                project.Name, analysisDuration.TotalMilliseconds.ToString("F0"), analysis.TotalTasks);

            // Warn if analysis took too long
            if (analysisDuration.TotalSeconds > 120)
            {
                _logger.LogWarning("[Wyvern] SLOW {ProjectName} | Analysis took {Duration}s",
                    project.Name, analysisDuration.TotalSeconds.ToString("F1"));
            }
        }

        /// <summary>
        /// Reanalyzes a project whose specification was modified.
        /// First transitions back to WyvernAssigned, then runs normal analysis.
        /// </summary>
        private async Task ReanalyzeModifiedProjectAsync(Project project)
        {
            _logger.LogInformation("[Wyvern] REANALYZE {ProjectName} | Starting reanalysis", project.Name);

            var reanalysisStart = DateTime.UtcNow;

            // Transition back to WyvernAssigned so normal analysis flow can pick it up
            // This allows Wyvern to re-analyze with potentially updated Wyrm recommendations
            _projectService.UpdateProjectStatus(project.Id, ProjectStatus.WyrmAssigned);

            // Run analysis (which will create new tasks for changes)
            var analysis = await _projectService.AnalyzeProjectAsync(project.Id);
            var reanalysisDuration = DateTime.UtcNow - reanalysisStart;

            _logger.LogInformation("[Wyvern] REANALYZE COMPLETE {ProjectName} | Duration: {Duration}ms | Tasks: {TaskCount}",
                project.Name, reanalysisDuration.TotalMilliseconds.ToString("F0"), analysis.TotalTasks);
        }

    }
}
