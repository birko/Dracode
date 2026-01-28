using DraCode.KoboldLair.Server.Models;
using DraCode.KoboldLair.Server.Agents.Wyrm;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Service for managing project lifecycle, from specification creation to Wyrm assignment.
    /// Coordinates between Dragon (requirements), Wyrm (analysis), and Drake (execution).
    /// </summary>
    public class ProjectService
    {
        private readonly ProjectRepository _repository;
        private readonly WyrmFactory _wyrmFactory;
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly ILogger<ProjectService> _logger;
        private readonly string _defaultOutputPathBase;

        public ProjectService(
            ProjectRepository repository,
            WyrmFactory wyrmFactory,
            ProjectConfigurationService projectConfigService,
            ILogger<ProjectService> logger,
            string defaultOutputPathBase = "./workspace")
        {
            _repository = repository;
            _wyrmFactory = wyrmFactory;
            _projectConfigService = projectConfigService;
            _logger = logger;
            _defaultOutputPathBase = defaultOutputPathBase;
        }

        /// <summary>
        /// Registers a new project when Dragon creates a specification
        /// </summary>
        public Project RegisterProject(string projectName, string specificationPath)
        {
            // Check if project already exists
            var existing = _repository.GetBySpecificationPath(specificationPath);
            if (existing != null)
            {
                _logger.LogWarning("Project already exists for specification: {Path}", specificationPath);
                return existing;
            }

            // Create output directory for project
            var outputPath = Path.Combine(_defaultOutputPathBase, SanitizeProjectName(projectName));
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                _logger.LogInformation("Created output directory: {Path}", outputPath);
            }

            var project = new Project
            {
                Name = projectName,
                SpecificationPath = specificationPath,
                OutputPath = outputPath,
                Status = ProjectStatus.New
            };

            _repository.Add(project);
            _logger.LogInformation("✨ Registered new project: {ProjectName} (ID: {ProjectId})", projectName, project.Id);

            return project;
        }

        /// <summary>
        /// Assigns a Wyrm to a project for specification analysis
        /// </summary>
        public async Task<Wyrm> AssignWyrmAsync(string projectId)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            if (project.Status != ProjectStatus.New)
            {
                _logger.LogWarning("Project {ProjectId} already has status {Status}, skipping Wyrm assignment", projectId, project.Status);
                var existingWyrm = _wyrmFactory.GetWyrm(project.Name);
                if (existingWyrm != null)
                {
                    return existingWyrm;
                }
            }

            try
            {
                // Create Wyrm for this project
                var wyrm = _wyrmFactory.CreateWyrm(
                    project.Name,
                    project.SpecificationPath,
                    project.OutputPath
                );

                // Update project
                project.WyrmId = wyrm.ProjectName; // Using project name as Wyrm ID
                project.Status = ProjectStatus.WyrmAssigned;
                _repository.Update(project);

                _logger.LogInformation("🐉 Assigned Wyrm to project: {ProjectName}", project.Name);

                return wyrm;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign Wyrm to project: {ProjectId}", projectId);
                project.Status = ProjectStatus.Failed;
                project.ErrorMessage = $"Wyrm assignment failed: {ex.Message}";
                _repository.Update(project);
                throw;
            }
        }

        /// <summary>
        /// Runs Wyrm analysis on a project's specification
        /// </summary>
        public async Task<WyrmAnalysis> AnalyzeProjectAsync(string projectId)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            var wyrm = _wyrmFactory.GetWyrm(project.Name);
            if (wyrm == null)
            {
                throw new InvalidOperationException($"No Wyrm assigned to project: {project.Name}");
            }

            try
            {
                _logger.LogInformation("🔍 Starting Wyrm analysis for project: {ProjectName}", project.Name);

                // Run analysis
                var analysis = await wyrm.AnalyzeProjectAsync();

                // Save analysis report
                var analysisReportPath = Path.Combine(project.OutputPath, $"{project.Name}-analysis.md");
                var report = wyrm.GenerateReport();
                await File.WriteAllTextAsync(analysisReportPath, report);

                // Create tasks
                var taskFiles = await wyrm.CreateTasksAsync();

                // Update project
                project.Status = ProjectStatus.Analyzed;
                project.AnalyzedAt = DateTime.UtcNow;
                project.AnalysisOutputPath = analysisReportPath;
                project.TaskFiles = taskFiles;
                _repository.Update(project);

                _logger.LogInformation("✅ Wyrm analysis completed for project: {ProjectName}. Tasks: {TaskCount}", 
                    project.Name, analysis.TotalTasks);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wyrm analysis failed for project: {ProjectId}", projectId);
                project.Status = ProjectStatus.Failed;
                project.ErrorMessage = $"Analysis failed: {ex.Message}";
                _repository.Update(project);
                throw;
            }
        }

        /// <summary>
        /// Updates project status
        /// </summary>
        public void UpdateProjectStatus(string projectId, ProjectStatus status, string? errorMessage = null)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            project.Status = status;
            if (errorMessage != null)
            {
                project.ErrorMessage = errorMessage;
            }

            _repository.Update(project);
            _logger.LogInformation("Updated project {ProjectId} status to {Status}", projectId, status);
        }

        /// <summary>
        /// Gets all projects
        /// </summary>
        public List<Project> GetAllProjects()
        {
            return _repository.GetAll();
        }

        /// <summary>
        /// Gets projects by status
        /// </summary>
        public List<Project> GetProjectsByStatus(ProjectStatus status)
        {
            return _repository.GetByStatus(status);
        }

        /// <summary>
        /// Gets a project by ID
        /// </summary>
        public Project? GetProject(string projectId)
        {
            return _repository.GetById(projectId);
        }

        /// <summary>
        /// Gets statistics about projects
        /// </summary>
        public ProjectStatistics GetStatistics()
        {
            var allProjects = _repository.GetAll();
            return new ProjectStatistics
            {
                TotalProjects = allProjects.Count,
                NewProjects = allProjects.Count(p => p.Status == ProjectStatus.New),
                WyrmAssignedProjects = allProjects.Count(p => p.Status == ProjectStatus.WyrmAssigned),
                AnalyzedProjects = allProjects.Count(p => p.Status == ProjectStatus.Analyzed),
                InProgressProjects = allProjects.Count(p => p.Status == ProjectStatus.InProgress),
                CompletedProjects = allProjects.Count(p => p.Status == ProjectStatus.Completed),
                FailedProjects = allProjects.Count(p => p.Status == ProjectStatus.Failed)
            };
        }

        /// <summary>
        /// Sanitizes project name for use as directory name
        /// </summary>
        private string SanitizeProjectName(string projectName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", projectName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim().Replace(" ", "-").ToLowerInvariant();
        }

        /// <summary>
        /// Sets provider configuration for a project
        /// </summary>
        public void SetProjectProviders(string projectId, string agentType, string? provider, string? model = null)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            _projectConfigService.SetProjectProvider(projectId, agentType, provider, model);
            _logger.LogInformation("Updated {AgentType} provider to {Provider} for project: {ProjectName}", 
                agentType, provider, project.Name);
        }

        /// <summary>
        /// Gets provider configuration for a project
        /// </summary>
        public ProjectConfig GetProjectConfig(string projectId)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            return _projectConfigService.GetOrCreateProjectConfig(projectId, project.Name);
        }

        /// <summary>
        /// Initializes configuration for all projects that don't have it
        /// </summary>
        public void InitializeProjectConfigurations(ProviderConfigurationService globalConfig)
        {
            var projects = _repository.GetAll();
            foreach (var project in projects)
            {
                var config = _projectConfigService.GetProjectConfig(project.Id);
                if (config == null)
                {
                    // Create default configuration
                    var newConfig = _projectConfigService.GetOrCreateProjectConfig(project.Id, project.Name);
                    
                    // Initialize with global defaults if not set
                    if (string.IsNullOrEmpty(newConfig.WyrmProvider))
                    {
                        newConfig.WyrmProvider = globalConfig.GetProviderForAgent("wyvern");
                        newConfig.DrakeProvider = globalConfig.GetProviderForAgent("wyvern");
                        newConfig.KoboldProvider = globalConfig.GetProviderForAgent("kobold");
                        _projectConfigService.UpdateProjectConfig(newConfig);
                    }
                    
                    _logger.LogInformation("Initialized configuration for project: {ProjectName}", project.Name);
                }
            }
        }

        /// <summary>
        /// Toggles whether an agent type is enabled for a project
        /// </summary>
        public void SetAgentEnabled(string projectId, string agentType, bool enabled)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            _projectConfigService.SetAgentEnabled(projectId, agentType, enabled);
            _logger.LogInformation("{AgentType} {Status} for project: {ProjectName}", 
                agentType, enabled ? "enabled" : "disabled", project.Name);
        }

        /// <summary>
        /// Checks if an agent type is enabled for a project
        /// </summary>
        public bool IsAgentEnabled(string projectId, string agentType)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            return _projectConfigService.IsAgentEnabled(projectId, agentType);
        }

        /// <summary>
        /// Sets the maximum parallel kobolds limit for a project
        /// </summary>
        public void SetMaxParallelKobolds(string projectId, int maxParallel)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            if (maxParallel < 1)
            {
                throw new ArgumentException("MaxParallelKobolds must be at least 1");
            }

            _projectConfigService.SetMaxParallelKobolds(projectId, maxParallel);
            _logger.LogInformation("Updated MaxParallelKobolds to {Max} for project: {ProjectName}", 
                maxParallel, project.Name);
        }

        /// <summary>
        /// Gets the maximum parallel kobolds limit for a project
        /// </summary>
        public int GetMaxParallelKobolds(string projectId)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            return _projectConfigService.GetMaxParallelKobolds(projectId);
        }
    }

    /// <summary>
    /// Statistics about projects in the system
    /// </summary>
    public class ProjectStatistics
    {
        public int TotalProjects { get; set; }
        public int NewProjects { get; set; }
        public int WyrmAssignedProjects { get; set; }
        public int AnalyzedProjects { get; set; }
        public int InProgressProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int FailedProjects { get; set; }

        public override string ToString()
        {
            return $"Projects: {TotalProjects} total, {NewProjects} new, {WyrmAssignedProjects} assigned, " +
                   $"{AnalyzedProjects} analyzed, {InProgressProjects} in progress, " +
                   $"{CompletedProjects} completed, {FailedProjects} failed";
        }
    }
}
