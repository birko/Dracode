using DraCode.KoboldLair.Server.Models;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Background service that monitors for new project specifications and processes them with Wyverns.
    /// Runs every minute to check for new specs, assign Wyverns, and trigger analysis.
    /// </summary>
    public class WyvernProcessingService : BackgroundService
    {
        private readonly ILogger<WyvernProcessingService> _logger;
        private readonly ProjectService _projectService;
        private readonly TimeSpan _checkInterval;
        private readonly string _specificationsPath;
        private bool _isRunning;
        private readonly object _lock = new object();

        public WyvernProcessingService(
            ILogger<WyvernProcessingService> logger,
            ProjectService projectService,
            string specificationsPath = "./specifications",
            int checkIntervalSeconds = 60)
        {
            _logger = logger;
            _projectService = projectService;
            _specificationsPath = specificationsPath;
            _checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
            _isRunning = false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🐉 Wyvern Processing Service started. Interval: {Interval}s, Watching: {Path}",
                _checkInterval.TotalSeconds, _specificationsPath);

            // Ensure specifications directory exists
            if (!Directory.Exists(_specificationsPath))
            {
                Directory.CreateDirectory(_specificationsPath);
                _logger.LogInformation("Created specifications directory: {Path}", _specificationsPath);
            }

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
        /// Processes all specifications in the specifications directory
        /// </summary>
        private async Task ProcessSpecificationsAsync(CancellationToken cancellationToken)
        {
            if (!Directory.Exists(_specificationsPath))
            {
                _logger.LogDebug("Specifications directory does not exist: {Path}", _specificationsPath);
                return;
            }

            // Get all specification files
            var specFiles = Directory.GetFiles(_specificationsPath, "*.md");
            if (specFiles.Length == 0)
            {
                _logger.LogDebug("No specification files found");
                return;
            }

            _logger.LogInformation("📋 Found {Count} specification file(s)", specFiles.Length);

            // Check for new projects that need Wyrms
            var newProjects = await CheckForNewProjectsAsync(specFiles);

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
        /// Checks for new specification files that don't have projects yet
        /// </summary>
        private async Task<List<Models.Project>> CheckForNewProjectsAsync(string[] specFiles)
        {
            var newProjects = new List<Models.Project>();

            foreach (var specPath in specFiles)
            {
                try
                {
                    // Check if this specification already has a project
                    var existingProject = _projectService.GetAllProjects()
                        .FirstOrDefault(p => Path.GetFullPath(p.SpecificationPath) == Path.GetFullPath(specPath));

                    if (existingProject != null)
                    {
                        _logger.LogDebug("Specification already has project: {SpecPath}", specPath);
                        continue;
                    }

                    // Extract project name from specification file
                    var projectName = await ExtractProjectNameAsync(specPath);
                    if (string.IsNullOrEmpty(projectName))
                    {
                        projectName = Path.GetFileNameWithoutExtension(specPath);
                    }

                    // Register new project
                    var project = _projectService.RegisterProject(projectName, specPath);
                    newProjects.Add(project);

                    _logger.LogInformation("✨ Discovered new specification: {SpecPath} -> Project: {ProjectName}",
                        Path.GetFileName(specPath), projectName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking specification: {SpecPath}", specPath);
                }
            }

            return newProjects;
        }

        /// <summary>
        /// Extracts project name from specification markdown
        /// </summary>
        private async Task<string> ExtractProjectNameAsync(string specPath)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(specPath);

                // Look for first H1 heading
                var h1Line = lines.FirstOrDefault(l => l.TrimStart().StartsWith("# "));
                if (h1Line != null)
                {
                    return h1Line.TrimStart().Substring(2).Trim();
                }

                // Look for "Project:" or "Project Name:" pattern
                var projectLine = lines.FirstOrDefault(l =>
                    l.Contains("Project:", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("Project Name:", StringComparison.OrdinalIgnoreCase));

                if (projectLine != null)
                {
                    var colonIndex = projectLine.IndexOf(':');
                    if (colonIndex >= 0 && colonIndex < projectLine.Length - 1)
                    {
                        return projectLine.Substring(colonIndex + 1).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract project name from: {SpecPath}", specPath);
            }

            return "";
        }

        /// <summary>
        /// Assigns a Wyvern to a project
        /// </summary>
        private async Task AssignWyvernToProjectAsync(Models.Project project)
        {
            _logger.LogInformation("🐲 Assigning Wyvern to project: {ProjectName}", project.Name);

            var wyvern = await _projectService.AssignWyvernAsync(project.Id);

            _logger.LogInformation("✅ Wyvern assigned to project: {ProjectName}", project.Name);
        }

        /// <summary>
        /// Runs Wyvern analysis on a project
        /// </summary>
        private async Task AnalyzeProjectAsync(Models.Project project)
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
        private async Task ReanalyzeModifiedProjectAsync(Models.Project project)
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
