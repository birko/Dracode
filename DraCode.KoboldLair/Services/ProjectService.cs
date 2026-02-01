using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Orchestrators;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Service for managing project lifecycle, from specification creation to wyvern assignment.
    /// Coordinates between Dragon (requirements), wyvern (analysis), and Drake (execution).
    /// </summary>
    public class ProjectService
    {
        private readonly ProjectRepository _repository;
        private readonly WyvernFactory _wyvernFactory;
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly ILogger<ProjectService> _logger;
        private readonly GitService _gitService;
        private readonly string _projectsPath;

        public ProjectService(
            ProjectRepository repository,
            WyvernFactory wyvernFactory,
            ProjectConfigurationService projectConfigService,
            ILogger<ProjectService> logger,
            GitService gitService,
            string projectsPath = "./projects")
        {
            _repository = repository;
            _wyvernFactory = wyvernFactory;
            _projectConfigService = projectConfigService;
            _logger = logger;
            _gitService = gitService;
            _projectsPath = projectsPath;
        }

        /// <summary>
        /// Gets the configured projects path
        /// </summary>
        public string ProjectsPath => _projectsPath;

        /// <summary>
        /// Gets the git service (null if not configured)
        /// </summary>
        public GitService? GitService => _gitService;

        /// <summary>
        /// Checks if git is enabled for a project (git installed and repository exists)
        /// </summary>
        public async Task<bool> IsGitEnabledAsync(string projectId)
        {
            var project = _repository.GetById(projectId);
            if (project == null || string.IsNullOrEmpty(project.OutputPath))
                return false;

            if (!await _gitService.IsGitInstalledAsync())
                return false;

            return await _gitService.IsRepositoryAsync(project.OutputPath);
        }

        /// <summary>
        /// Creates a project folder under ./projects/{sanitized-name}/ and returns the folder path.
        /// This should be called before Dragon saves the specification so files go to the right place.
        /// Also initializes a git repository if git is available.
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>The full path to the project folder</returns>
        public string CreateProjectFolder(string projectName)
        {
            var sanitizedName = SanitizeProjectName(projectName);
            var folder = Path.Combine(_projectsPath, sanitizedName);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                _logger.LogInformation("Created project folder: {Path}", folder);
            }

            // Also create the workspace subfolder for generated code
            var workspaceFolder = Path.Combine(folder, "workspace");
            if (!Directory.Exists(workspaceFolder))
            {
                Directory.CreateDirectory(workspaceFolder);
            }

            // Initialize git repository if git is available
            InitializeGitRepositoryAsync(folder).GetAwaiter().GetResult();

            return folder;
        }

        /// <summary>
        /// Initializes a git repository in the project folder if git is available
        /// </summary>
        private async Task InitializeGitRepositoryAsync(string projectFolder)
        {
            try
            {
                if (await _gitService.IsGitInstalledAsync())
                {
                    var initialized = await _gitService.InitRepositoryAsync(projectFolder);
                    if (initialized)
                    {
                        _logger.LogInformation("Initialized git repository for project at {Path}", projectFolder);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize git repository at {Path}", projectFolder);
            }
        }

        /// <summary>
        /// Registers a new project when Dragon creates a specification.
        /// With consolidated folders, the specification should already be at {projectFolder}/specification.md
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

            // Also check by name to avoid duplicates
            var byName = _repository.GetByName(projectName);
            if (byName != null)
            {
                _logger.LogWarning("Project already exists with name: {Name} (ID: {Id})", projectName, byName.Id);
                return byName;
            }

            // Determine the project folder from the specification path
            // Expected: ./projects/{sanitized-name}/specification.md
            var projectFolder = Path.GetDirectoryName(specificationPath) ?? CreateProjectFolder(projectName);

            // Output path is the project folder itself (task files go here)
            // Workspace for generated code is a subfolder
            var workspacePath = Path.Combine(projectFolder, "workspace");
            if (!Directory.Exists(workspacePath))
            {
                Directory.CreateDirectory(workspacePath);
                _logger.LogInformation("Created workspace directory: {Path}", workspacePath);
            }

            var project = new Project
            {
                Name = projectName,
                SpecificationPath = specificationPath,
                OutputPath = projectFolder, // Task files and analysis go here
                Status = ProjectStatus.Prototype
            };

            _repository.Add(project);
            _logger.LogInformation("✨ Registered new project: {ProjectName} (ID: {ProjectId})", projectName, project.Id);

            return project;
        }

        /// <summary>
        /// Assigns a Wyvern to a project for specification analysis.
        /// Uses project-specific provider/model settings for both Wyvern and Wyrm if configured.
        /// </summary>
        public async Task<Wyvern> AssignWyvernAsync(string projectId)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            if (project.Status != ProjectStatus.New)
            {
                _logger.LogWarning("Project {ProjectId} already has status {Status}, skipping Wyvern assignment", projectId, project.Status);
                var existingWyvern = _wyvernFactory.GetWyvern(project.Name);
                if (existingWyvern != null)
                {
                    return existingWyvern;
                }
            }

            try
            {
                // Get project-specific provider/model settings for Wyvern
                var wyvernProvider = _projectConfigService.GetProjectProvider(projectId, "wyvern");
                var wyvernModel = _projectConfigService.GetProjectModel(projectId, "wyvern");

                // Get project-specific provider/model settings for Wyrm
                var wyrmProvider = _projectConfigService.GetProjectProvider(projectId, "wyrm");
                var wyrmModel = _projectConfigService.GetProjectModel(projectId, "wyrm");

                _logger.LogDebug("Creating Wyvern for project {ProjectName} with Wyvern provider={WyvernProvider}, Wyrm provider={WyrmProvider}",
                    project.Name, wyvernProvider ?? "default", wyrmProvider ?? "default");

                // Create wyvern for this project with project-specific settings
                var wyvern = _wyvernFactory.CreateWyvern(
                    project.Name,
                    project.SpecificationPath,
                    project.OutputPath,
                    wyvernProvider,
                    wyvernModel,
                    wyrmProvider,
                    wyrmModel
                );

                // Update project
                project.WyvernId = wyvern.ProjectName; // Using project name as wyvern ID
                project.Status = ProjectStatus.WyvernAssigned;
                _repository.Update(project);

                _logger.LogInformation("🐉 Assigned wyvern to project: {ProjectName}", project.Name);

                return wyvern;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign Wyvern to project: {ProjectId}", projectId);
                project.Status = ProjectStatus.Failed;
                project.ErrorMessage = $"Wyvern assignment failed: {ex.Message}";
                _repository.Update(project);
                throw;
            }
        }

        /// <summary>
        /// Runs Wyvern analysis on a project's specification.
        /// Only sets status to Analyzed if Wyrm is enabled and all areas have tasks assigned.
        /// If some areas fail, they are stored as pending areas for reprocessing on subsequent runs.
        /// </summary>
        public async Task<WyvernAnalysis> AnalyzeProjectAsync(string projectId)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            var wyvern = _wyvernFactory.GetWyvern(project.Name);
            if (wyvern == null)
            {
                throw new InvalidOperationException($"No Wyvern assigned to project: {project.Name}");
            }

            try
            {
                // Check if Wyrm is enabled for this project
                var wyrmEnabled = _projectConfigService.IsAgentEnabled(projectId, "wyrm");

                // Determine if we're reprocessing pending areas or doing full analysis
                var isReprocessing = project.PendingAreas.Count > 0;
                var areasToProcess = isReprocessing ? project.PendingAreas : null;

                WyvernAnalysis analysis;

                if (!isReprocessing)
                {
                    _logger.LogInformation("🔍 Starting wyvern analysis for project: {ProjectName}", project.Name);

                    // Run full analysis
                    analysis = await wyvern.AnalyzeProjectAsync();

                    // Save analysis report (simplified name - project folder provides context)
                    var analysisReportPath = Path.Combine(project.OutputPath, "analysis.md");
                    var report = wyvern.GenerateReport();
                    await File.WriteAllTextAsync(analysisReportPath, report);
                    project.AnalysisOutputPath = analysisReportPath;
                }
                else
                {
                    _logger.LogInformation("🔄 Reprocessing {Count} pending area(s) for project: {ProjectName}",
                        project.PendingAreas.Count, project.Name);

                    // For reprocessing, we need the existing analysis
                    analysis = wyvern.Analysis ?? await wyvern.AnalyzeProjectAsync();
                }

                // Create tasks (process only pending areas if reprocessing)
                var (taskFiles, failedAreas) = await wyvern.CreateTasksAsync(
                    areasToProcess,
                    isReprocessing ? project.TaskFiles : null
                );

                // Compute content hash for change detection
                var specContent = await File.ReadAllTextAsync(project.SpecificationPath);
                var contentHash = ComputeContentHash(specContent);

                // Get all areas from analysis
                var allAreas = wyvern.GetAllAreaNames();

                // Determine which areas still need processing
                var areasWithTasks = taskFiles.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var pendingAreas = allAreas
                    .Where(a => !areasWithTasks.Contains(a))
                    .Concat(failedAreas)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Update project fields
                project.LastProcessedAt = DateTime.UtcNow;
                project.LastProcessedContentHash = contentHash;
                project.TaskFiles = taskFiles;
                project.PendingAreas = pendingAreas;

                // Determine final status:
                // - Set to Analyzed only if Wyrm is enabled AND all areas have tasks
                // - Otherwise keep as WyvernAssigned for reprocessing on next cycle
                var allAreasComplete = pendingAreas.Count == 0;

                if (wyrmEnabled && allAreasComplete)
                {
                    project.Status = ProjectStatus.Analyzed;
                    project.AnalyzedAt = DateTime.UtcNow;
                    _logger.LogInformation("✅ Wyvern analysis completed for project: {ProjectName}. Tasks: {TaskCount}, Areas: {AreaCount}",
                        project.Name, analysis.TotalTasks, allAreas.Count);
                }
                else
                {
                    // Keep as WyvernAssigned - will be reprocessed on next service cycle
                    project.Status = ProjectStatus.WyvernAssigned;

                    if (!wyrmEnabled)
                    {
                        _logger.LogInformation("⏸️ Wyvern analysis partial for project: {ProjectName}. Wyrm disabled - waiting for enablement.",
                            project.Name);
                    }
                    else
                    {
                        _logger.LogInformation("⏸️ Wyvern analysis partial for project: {ProjectName}. {PendingCount}/{TotalCount} area(s) pending reprocessing.",
                            project.Name, pendingAreas.Count, allAreas.Count);
                    }
                }

                _repository.Update(project);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wyvern analysis failed for project: {ProjectId}", projectId);
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
        /// Marks a project's specification as modified, triggering reprocessing by Wyvern.
        /// Called when Dragon updates an existing specification.
        /// </summary>
        public void MarkSpecificationModified(string specificationPath)
        {
            var project = _repository.GetBySpecificationPath(specificationPath);
            if (project == null)
            {
                _logger.LogDebug("No project found for specification: {Path}", specificationPath);
                return;
            }

            // Only mark as modified if already analyzed
            if (project.Status == ProjectStatus.Analyzed ||
                project.Status == ProjectStatus.InProgress ||
                project.Status == ProjectStatus.Completed)
            {
                project.Status = ProjectStatus.SpecificationModified;
                project.UpdatedAt = DateTime.UtcNow;
                _repository.Update(project);
                _logger.LogInformation("📝 Specification modified for project: {ProjectName} - will be reprocessed", project.Name);
            }
        }

        /// <summary>
        /// Gets a project by its specification path
        /// </summary>
        public Project? GetProjectBySpecificationPath(string specificationPath)
        {
            return _repository.GetBySpecificationPath(specificationPath);
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
                PrototypeProjects = allProjects.Count(p => p.Status == ProjectStatus.Prototype),
                NewProjects = allProjects.Count(p => p.Status == ProjectStatus.New),
                WyvernAssignedProjects = allProjects.Count(p => p.Status == ProjectStatus.WyvernAssigned),
                AnalyzedProjects = allProjects.Count(p => p.Status == ProjectStatus.Analyzed),
                SpecificationModifiedProjects = allProjects.Count(p => p.Status == ProjectStatus.SpecificationModified),
                InProgressProjects = allProjects.Count(p => p.Status == ProjectStatus.InProgress),
                CompletedProjects = allProjects.Count(p => p.Status == ProjectStatus.Completed),
                FailedProjects = allProjects.Count(p => p.Status == ProjectStatus.Failed)
            };
        }

        /// <summary>
        /// Registers an existing project from a source directory on disk.
        /// Creates a project entry pointing to the existing codebase, ready for specification creation.
        /// </summary>
        /// <param name="projectName">Name for the project</param>
        /// <param name="sourcePath">Path to the existing project source code</param>
        /// <returns>Project ID if successful, null otherwise</returns>
        public string? RegisterExistingProject(string projectName, string sourcePath)
        {
            // Normalize the path
            sourcePath = Path.GetFullPath(sourcePath);

            if (!Directory.Exists(sourcePath))
            {
                _logger.LogWarning("Cannot register project - source path does not exist: {Path}", sourcePath);
                return null;
            }

            // Check if project already exists with this source path
            var existing = _repository.GetAll()
                .FirstOrDefault(p => p.Metadata.TryGetValue("SourcePath", out var sp) &&
                    string.Equals(sp, sourcePath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                _logger.LogWarning("Project already exists for source path: {Path} (ID: {Id})", sourcePath, existing.Id);
                return existing.Id;
            }

            // Also check by name
            var byName = _repository.GetByName(projectName);
            if (byName != null)
            {
                _logger.LogWarning("Project already exists with name: {Name} (ID: {Id})", projectName, byName.Id);
                return byName.Id;
            }

            // Create consolidated project folder under ./projects/
            var projectFolder = CreateProjectFolder(projectName);

            var project = new Project
            {
                Name = projectName,
                SpecificationPath = "", // Will be set when Dragon creates the specification
                OutputPath = projectFolder, // Task files and analysis go here
                Status = ProjectStatus.Prototype,
                Metadata = new Dictionary<string, string>
                {
                    ["SourcePath"] = sourcePath,
                    ["IsExistingProject"] = "true",
                    ["ImportedAt"] = DateTime.UtcNow.ToString("O")
                }
            };

            _repository.Add(project);
            _logger.LogInformation("✨ Registered existing project: {ProjectName} (ID: {ProjectId}) from {SourcePath}",
                projectName, project.Id, sourcePath);

            return project.Id;
        }

        /// <summary>
        /// Approves a project specification, changing status from Prototype to New.
        /// This allows Wyvern to start processing the project.
        /// Also creates an initial git commit if git is enabled.
        /// </summary>
        /// <param name="projectId">Project ID or name</param>
        /// <returns>True if approved successfully</returns>
        public bool ApproveProject(string projectId)
        {
            var project = _repository.GetById(projectId) ?? _repository.GetByName(projectId);
            if (project == null)
            {
                _logger.LogWarning("Cannot approve project - not found: {ProjectId}", projectId);
                return false;
            }

            if (project.Status != ProjectStatus.Prototype)
            {
                _logger.LogWarning("Cannot approve project {ProjectName} - status is {Status}, not Prototype",
                    project.Name, project.Status);
                return false;
            }

            project.Status = ProjectStatus.New;
            project.UpdatedAt = DateTime.UtcNow;
            _repository.Update(project);

            // Create initial git commit with the specification
            CreateInitialGitCommitAsync(project).GetAwaiter().GetResult();

            _logger.LogInformation("✅ Project '{ProjectName}' approved and ready for Wyvern processing", project.Name);
            return true;
        }

        /// <summary>
        /// Creates an initial git commit when a project is approved
        /// </summary>
        private async Task CreateInitialGitCommitAsync(Project project)
        {
            if (string.IsNullOrEmpty(project.OutputPath))
                return;

            try
            {
                if (!await _gitService.IsGitInstalledAsync())
                    return;

                if (!await _gitService.IsRepositoryAsync(project.OutputPath))
                    return;

                // Stage all files
                await _gitService.StageAllAsync(project.OutputPath);

                // Create initial commit
                var committed = await _gitService.CommitChangesAsync(
                    project.OutputPath,
                    $"Initial commit: {project.Name} specification\n\nProject approved and ready for development.",
                    "Dragon");

                if (committed)
                {
                    _logger.LogInformation("Created initial git commit for project: {ProjectName}", project.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create initial git commit for project: {ProjectName}", project.Name);
            }
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
        /// Computes a hash of content for change detection
        /// </summary>
        private static string ComputeContentHash(string content)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
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
                    if (string.IsNullOrEmpty(newConfig.WyvernProvider))
                    {
                        newConfig.WyvernProvider = globalConfig.GetProviderForAgent("wyvern");
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
}
