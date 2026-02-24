using System.Text.Json;
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
        private readonly ILogger<ProjectService> _logger;
        private readonly GitService _gitService;
        private readonly ProjectConfigurationService? _projectConfigService;
        private readonly string _projectsPath;

        public ProjectService(
            ProjectRepository repository,
            WyvernFactory wyvernFactory,
            ILogger<ProjectService> logger,
            GitService gitService,
            KoboldLairConfiguration config,
            ProjectConfigurationService? projectConfigService = null)
        {
            _repository = repository;
            _wyvernFactory = wyvernFactory;
            _logger = logger;
            _gitService = gitService;
            _projectConfigService = projectConfigService;
            _projectsPath = config.ProjectsPath ?? "./projects";
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
            if (project == null || string.IsNullOrEmpty(project.Paths.Output))
                return false;

            if (!await _gitService.IsGitInstalledAsync())
                return false;

            return await _gitService.IsRepositoryAsync(project.Paths.Output);
        }

        /// <summary>
        /// Creates a project folder under ./projects/{sanitized-name}/ and returns the folder path (async version).
        /// This should be called before Dragon saves the specification so files go to the right place.
        /// Also initializes a git repository if git is available.
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>The full path to the project folder</returns>
        public async Task<string> CreateProjectFolderAsync(string projectName)
        {
            var sanitizedName = SanitizeProjectName(projectName);
            var folder = Path.Combine(_projectsPath, sanitizedName);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                _logger.LogInformation("Created project folder: {Path}", folder);
            }

            // Initialize git repository if git is available
            await InitializeGitRepositoryAsync(folder);

            return folder;
        }

        /// <summary>
        /// Creates a project folder under ./projects/{sanitized-name}/ and returns the folder path.
        /// This should be called before Dragon saves the specification so files go to the right place.
        /// Also initializes a git repository if git is available.
        /// Note: Prefer CreateProjectFolderAsync() for non-blocking operation.
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <returns>The full path to the project folder</returns>
        public string CreateProjectFolder(string projectName)
        {
            return CreateProjectFolderAsync(projectName).ConfigureAwait(false).GetAwaiter().GetResult();
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
            // Workspace for generated code is a subfolder (created lazily when first file is written)

            var project = new Project
            {
                Name = projectName,
                Paths = new ProjectPaths
                {
                    Specification = specificationPath,
                    Output = projectFolder
                },
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
                var wyvernProvider = _repository.GetProjectProvider(projectId, "wyvern");
                var wyvernModel = _repository.GetProjectModel(projectId, "wyvern");

                // Get project-specific provider/model settings for Wyrm
                var wyrmProvider = _repository.GetProjectProvider(projectId, "wyrm");
                var wyrmModel = _repository.GetProjectModel(projectId, "wyrm");

                _logger.LogDebug("Creating Wyvern for project {ProjectName} with Wyvern provider={WyvernProvider}, Wyrm provider={WyrmProvider}",
                    project.Name, wyvernProvider ?? "default", wyrmProvider ?? "default");

                // For existing projects, pass the source path so Wyvern scans the actual codebase
                string? workspaceScanPath = null;
                if (project.Metadata.TryGetValue("IsExistingProject", out var isExisting) && isExisting == "true" &&
                    project.Metadata.TryGetValue("SourcePath", out var srcPath) && Directory.Exists(srcPath))
                {
                    workspaceScanPath = srcPath;
                }

                // Create wyvern for this project with project-specific settings
                var wyvern = _wyvernFactory.CreateWyvern(
                    project.Name,
                    project.Paths.Specification,
                    project.Paths.Output,
                    wyvernProvider,
                    wyvernModel,
                    wyrmProvider,
                    wyrmModel,
                    workspaceScanPath: workspaceScanPath
                );

                // Update project
                project.Tracking.WyvernId = wyvern.ProjectName; // Using project name as wyvern ID
                project.Status = ProjectStatus.WyrmAssigned;
                _repository.Update(project);

                _logger.LogInformation("🐉 Assigned wyvern to project: {ProjectName}", project.Name);

                return wyvern;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign Wyvern to project: {ProjectId}", projectId);
                project.Status = ProjectStatus.Failed;
                project.Tracking.ErrorMessage = $"Wyvern assignment failed: {ex.Message}";
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
                // Wyvern may have been lost after server restart - re-assign it
                _logger.LogWarning("🔄 Wyvern not found for project {ProjectName}, re-assigning...", project.Name);
                wyvern = await AssignWyvernAsync(project.Id);
            }

            try
            {
                // Check if Wyrm is enabled for this project
                var wyrmEnabled = _repository.IsAgentEnabled(projectId, "wyrm");

                // Determine if we're reprocessing pending areas or doing full analysis
                var isReprocessing = project.Tracking.PendingAreas.Count > 0;
                var areasToProcess = isReprocessing ? project.Tracking.PendingAreas : null;

                WyvernAnalysis analysis;

                if (!isReprocessing)
                {
                    _logger.LogInformation("🔍 Starting wyvern analysis for project: {ProjectName}", project.Name);

                    // Load Wyrm recommendations if they exist
                    WyrmRecommendation? wyrmRecommendation = null;
                    var wyrmRecommendationPath = Path.Combine(project.Paths.Output, "wyrm-recommendation.json");
                    if (File.Exists(wyrmRecommendationPath))
                    {
                        try
                        {
                            var wyrmJson = await File.ReadAllTextAsync(wyrmRecommendationPath);
                            wyrmRecommendation = System.Text.Json.JsonSerializer.Deserialize<WyrmRecommendation>(wyrmJson, new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true,
                                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                            });
                            
                            _logger.LogInformation("📖 Loaded Wyrm recommendations for project: {ProjectName}", project.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to load Wyrm recommendations, proceeding without them");
                        }
                    }

                    // Run full analysis with optional Wyrm recommendations
                    analysis = await wyvern.AnalyzeProjectAsync(null, wyrmRecommendation);

                    // Save analysis report (simplified name - project folder provides context)
                    var analysisReportPath = Path.Combine(project.Paths.Output, "analysis.md");
                    var report = wyvern.GenerateReport();
                    await File.WriteAllTextAsync(analysisReportPath, report);
                    project.Paths.Analysis = analysisReportPath;
                }
                else
                {
                    _logger.LogInformation("🔄 Reprocessing {Count} pending area(s) for project: {ProjectName}",
                        project.Tracking.PendingAreas.Count, project.Name);

                    // For reprocessing, we need the existing analysis
                    analysis = wyvern.Analysis ?? await wyvern.AnalyzeProjectAsync();
                }

                // Create tasks (process only pending areas if reprocessing)
                var (taskFiles, failedAreas) = await wyvern.CreateTasksAsync(
                    areasToProcess,
                    isReprocessing ? project.Paths.TaskFiles : null
                );

                // Compute content hash for change detection
                var specContent = await File.ReadAllTextAsync(project.Paths.Specification);
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
                project.Timestamps.LastProcessedAt = DateTime.UtcNow;
                project.Tracking.LastProcessedContentHash = contentHash;
                project.Paths.TaskFiles = taskFiles;
                project.Tracking.PendingAreas = pendingAreas;

                // Determine final status:
                // - Set to Analyzed only if Wyrm is enabled AND all areas have tasks
                // - Otherwise keep as WyvernAssigned for reprocessing on next cycle
                var allAreasComplete = pendingAreas.Count == 0;

                if (wyrmEnabled && allAreasComplete)
                {
                    project.Status = ProjectStatus.Analyzed;
                    project.Timestamps.AnalyzedAt = DateTime.UtcNow;
                    _logger.LogInformation("✅ Wyvern analysis completed for project: {ProjectName}. Tasks: {TaskCount}, Areas: {AreaCount}",
                        project.Name, analysis.TotalTasks, allAreas.Count);
                }
                else
                {
                    // Keep as WyrmAssigned - will be reprocessed on next service cycle
                    project.Status = ProjectStatus.WyrmAssigned;

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
                project.Tracking.ErrorMessage = $"Analysis failed: {ex.Message}";
                _repository.Update(project);
                throw;
            }
        }

        /// <summary>
        /// Updates a project in the repository (persists changes to disk).
        /// </summary>
        public void UpdateProject(Project project)
        {
            _repository.Update(project);
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
                project.Tracking.ErrorMessage = errorMessage;
            }

            _repository.Update(project);
            _logger.LogInformation("Updated project {ProjectId} status to {Status}", projectId, status);
        }

        /// <summary>
        /// Updates the execution state of a project with validation
        /// </summary>
        /// <param name="projectId">Project ID or name</param>
        /// <param name="state">New execution state</param>
        /// <returns>True if state was updated successfully, false if validation failed</returns>
        public bool SetExecutionState(string projectId, ProjectExecutionState state)
        {
            var project = _repository.GetById(projectId) ?? _repository.GetByName(projectId);
            if (project == null)
            {
                _logger.LogWarning("Cannot set execution state: Project not found: {ProjectId}", projectId);
                return false;
            }

            // Validate state transition
            var currentState = project.ExecutionState;

            // Cannot resume from Cancelled state
            if (currentState == ProjectExecutionState.Cancelled && state == ProjectExecutionState.Running)
            {
                _logger.LogWarning("Cannot resume cancelled project {ProjectName}", project.Name);
                return false;
            }

            // Cannot pause/suspend completed or failed projects
            if ((project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Failed) &&
                (state == ProjectExecutionState.Paused || state == ProjectExecutionState.Suspended))
            {
                _logger.LogWarning("Cannot pause/suspend project {ProjectName} with status {Status}", 
                    project.Name, project.Status);
                return false;
            }

            project.ExecutionState = state;
            _repository.Update(project);
            
            _logger.LogInformation("Updated project {ProjectName} execution state: {OldState} → {NewState}", 
                project.Name, currentState, state);
            
            return true;
        }

        /// <summary>
        /// Gets the current execution state of a project
        /// </summary>
        /// <param name="projectId">Project ID or name</param>
        /// <returns>Current execution state, or null if project not found</returns>
        public ProjectExecutionState? GetExecutionState(string projectId)
        {
            var project = _repository.GetById(projectId) ?? _repository.GetByName(projectId);
            return project?.ExecutionState;
        }


        /// <summary>
        /// Resets a failed project so Wyvern analysis can be retried.
        /// Clears the error message and sets status back to New for reprocessing.
        /// </summary>
        /// <param name="projectId">Project ID or name</param>
        /// <returns>True if retry was initiated successfully</returns>
        public bool RetryAnalysis(string projectId)
        {
            var project = _repository.GetById(projectId) ?? _repository.GetByName(projectId);
            if (project == null)
            {
                _logger.LogWarning("Cannot retry analysis - project not found: {ProjectId}", projectId);
                return false;
            }

            if (project.Status != ProjectStatus.Failed)
            {
                _logger.LogWarning("Cannot retry analysis for project {ProjectName} - status is {Status}, not Failed",
                    project.Name, project.Status);
                return false;
            }

            // Clear Wyvern assignment so it gets recreated
            if (project.Tracking.WyvernId != null)
            {
                _wyvernFactory.RemoveWyvern(project.Name);
                project.Tracking.WyvernId = null;
            }

            // Reset project state
            project.Status = ProjectStatus.New;
            project.Tracking.ErrorMessage = null;
            project.Tracking.PendingAreas.Clear();
            project.Timestamps.UpdatedAt = DateTime.UtcNow;

            _repository.Update(project);
            _logger.LogInformation("🔄 Retry initiated for project '{ProjectName}' - reset to New status", project.Name);

            return true;
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
                project.Timestamps.UpdatedAt = DateTime.UtcNow;
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
        /// Finds a project by name with fuzzy matching.
        /// First tries exact/sanitized match via repository, then tries containment matching
        /// (e.g., "birko.framework-master-specification" contains "birko.framework").
        /// </summary>
        /// <param name="name">The project name to search for</param>
        /// <returns>The matching project, or null if no match found</returns>
        public Project? FindProjectByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // First try exact/sanitized match
            var exact = _repository.GetByName(name);
            if (exact != null)
                return exact;

            // Try containment matching - check if any existing project name is contained
            // in the search name, or vice versa. Minimum 3 chars to avoid false positives.
            var allProjects = _repository.GetAll();
            var searchLower = name.ToLowerInvariant();

            foreach (var project in allProjects)
            {
                var projectNameLower = project.Name.ToLowerInvariant();
                if (projectNameLower.Length < 3)
                    continue;

                // Check if search name contains an existing project name
                // e.g., "birko.framework-master-specification" contains "birko.framework"
                if (searchLower.Contains(projectNameLower))
                    return project;

                // Check if existing project name contains search name
                // e.g., project "birko.framework" contains search "birko"
                if (searchLower.Length >= 3 && projectNameLower.Contains(searchLower))
                    return project;
            }

            // Also try matching by output folder name
            var sanitizedSearch = SanitizeProjectName(name);
            if (sanitizedSearch.Length >= 3)
            {
                foreach (var project in allProjects)
                {
                    if (string.IsNullOrEmpty(project.Paths.Output))
                        continue;

                    var folderName = Path.GetFileName(project.Paths.Output)?.ToLowerInvariant() ?? "";
                    if (folderName.Length >= 3 && (sanitizedSearch.Contains(folderName) || folderName.Contains(sanitizedSearch)))
                        return project;
                }
            }

            return null;
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
                WyvernAssignedProjects = allProjects.Count(p => p.Status == ProjectStatus.WyrmAssigned),
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
                Paths = new ProjectPaths
                {
                    Specification = "", // Will be set when Dragon creates the specification
                    Output = projectFolder
                },
                Status = ProjectStatus.Prototype,
                Metadata = new Dictionary<string, string>
                {
                    ["SourcePath"] = sourcePath,
                    ["IsExistingProject"] = "true",
                    ["ImportedAt"] = DateTime.UtcNow.ToString("O")
                }
            };

            _repository.Add(project);

            // Auto-configure external paths from project references
            AutoConfigureExternalPaths(project, sourcePath);

            _logger.LogInformation("✨ Registered existing project: {ProjectName} (ID: {ProjectId}) from {SourcePath}",
                projectName, project.Id, sourcePath);

            return project.Id;
        }

        /// <summary>
        /// Auto-discovers project references and configures external paths and metadata.
        /// Non-fatal if discovery fails.
        /// </summary>
        private void AutoConfigureExternalPaths(Project project, string sourcePath)
        {
            try
            {
                // Add the source path itself as an allowed external path
                var normalizedSourcePath = Path.GetFullPath(sourcePath);
                AddExternalPathToProject(project, normalizedSourcePath);
                _projectConfigService?.AddAllowedExternalPath(project.Id, sourcePath);

                // Discover project references
                var discovery = ProjectReferenceDiscoverer.DiscoverReferences(sourcePath);

                // Add each external directory as an allowed path
                foreach (var extDir in discovery.ExternalDirectories)
                {
                    var normalizedExtDir = Path.GetFullPath(extDir);
                    AddExternalPathToProject(project, normalizedExtDir);
                    _projectConfigService?.AddAllowedExternalPath(project.Id, extDir);
                }

                // Store discovery metadata on the project
                if (discovery.PrimaryProjectFile != null)
                {
                    project.Metadata["ProjectFile"] = discovery.PrimaryProjectFile;
                }
                if (!string.IsNullOrEmpty(discovery.ProjectType))
                {
                    project.Metadata["ProjectType"] = discovery.ProjectType;
                }
                // Store external project references as structured data
                if (discovery.References.Any(r => r.IsExternal))
                {
                    project.ExternalProjectReferences = discovery.References
                        .Where(r => r.IsExternal)
                        .Select(r => new ExternalProjectReference
                        {
                            Name = r.Name,
                            Path = r.DirectoryPath,
                            RelativePath = Path.GetRelativePath(sourcePath, r.DirectoryPath).Replace('\\', '/')
                        })
                        .ToList();
                }

                _repository.Update(project);

                _logger.LogInformation(
                    "📦 Auto-configured {ExtCount} external path(s) for project {ProjectName} (type: {ProjectType})",
                    discovery.ExternalDirectories.Count, project.Name, discovery.ProjectType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-configure external paths for project {ProjectName}", project.Name);
            }
        }

        /// <summary>
        /// Adds an external path directly to the project's Security.AllowedExternalPaths list.
        /// </summary>
        private static void AddExternalPathToProject(Project project, string normalizedPath)
        {
            if (!project.Security.AllowedExternalPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
            {
                project.Security.AllowedExternalPaths.Add(normalizedPath);
            }
        }

        /// <summary>
        /// Updates the specification path for a project.
        /// Called after a specification file is created for an existing project.
        /// </summary>
        /// <param name="projectId">Project ID or name</param>
        /// <param name="specificationPath">Path to the specification file</param>
        /// <returns>True if updated successfully</returns>
        public bool UpdateSpecificationPath(string projectId, string specificationPath)
        {
            var project = _repository.GetById(projectId) ?? _repository.GetByName(projectId);
            if (project == null)
            {
                _logger.LogWarning("Cannot update specification path - project not found: {ProjectId}", projectId);
                return false;
            }

            project.Paths.Specification = specificationPath;
            project.Timestamps.UpdatedAt = DateTime.UtcNow;
            _repository.Update(project);

            _logger.LogInformation("Updated specification path for project '{ProjectName}': {Path}",
                project.Name, specificationPath);
            return true;
        }

        /// <summary>
        /// Approves a project specification, changing status from Prototype to New (async version).
        /// This allows Wyvern to start processing the project.
        /// Also creates an initial git commit if git is enabled.
        /// </summary>
        /// <param name="projectId">Project ID or name</param>
        /// <returns>True if approved successfully</returns>
        public async Task<bool> ApproveProjectAsync(string projectId)
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

            // If specification path is empty, try to find it in the project folder
            if (string.IsNullOrEmpty(project.Paths.Specification))
            {
                var specPath = Path.Combine(project.Paths.Output, "specification.md");
                if (File.Exists(specPath))
                {
                    // Make relative path
                    var relativePath = Path.GetRelativePath(_projectsPath, specPath).Replace('\\', '/');
                    if (!relativePath.StartsWith("./"))
                    {
                        relativePath = "./" + relativePath;
                    }
                    project.Paths.Specification = relativePath;
                    _logger.LogInformation("Auto-detected specification path for project '{ProjectName}': {Path}",
                        project.Name, relativePath);
                }
                else
                {
                    _logger.LogWarning("Cannot approve project '{ProjectName}' - specification file not found at {Path}",
                        project.Name, specPath);
                    return false;
                }
            }

            project.Status = ProjectStatus.New;
            project.Timestamps.UpdatedAt = DateTime.UtcNow;
            _repository.Update(project);

            // Create initial git commit with the specification
            await CreateInitialGitCommitAsync(project);

            _logger.LogInformation("✅ Project '{ProjectName}' approved and ready for Wyvern processing", project.Name);
            return true;
        }

        /// <summary>
        /// Approves a project specification, changing status from Prototype to New.
        /// This allows Wyvern to start processing the project.
        /// Also creates an initial git commit if git is enabled.
        /// Note: Prefer ApproveProjectAsync() for non-blocking operation.
        /// </summary>
        /// <param name="projectId">Project ID or name</param>
        /// <returns>True if approved successfully</returns>
        public bool ApproveProject(string projectId)
        {
            return ApproveProjectAsync(projectId).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates an initial git commit when a project is approved
        /// </summary>
        private async Task CreateInitialGitCommitAsync(Project project)
        {
            if (string.IsNullOrEmpty(project.Paths.Output))
                return;

            try
            {
                if (!await _gitService.IsGitInstalledAsync())
                    return;

                if (!await _gitService.IsRepositoryAsync(project.Paths.Output))
                    return;

                // Stage all files
                await _gitService.StageAllAsync(project.Paths.Output);

                // Create initial commit
                var committed = await _gitService.CommitChangesAsync(
                    project.Paths.Output,
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
        public async Task SetProjectProvidersAsync(string projectId, string agentType, string? provider, string? model = null)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            await _repository.SetProjectProviderAsync(projectId, agentType, provider, model);
            _logger.LogInformation("Updated {AgentType} provider to {Provider} for project: {ProjectName}",
                agentType, provider, project.Name);
        }

        /// <summary>
        /// Gets provider configuration for a project (returns the project itself with agent configs)
        /// </summary>
        public Project? GetProjectConfig(string projectId)
        {
            return _repository.GetById(projectId);
        }

        /// <summary>
        /// Initializes configuration for all projects that don't have agent configs set
        /// </summary>
        public async Task InitializeProjectConfigurationsAsync(ProviderConfigurationService globalConfig)
        {
            var projects = _repository.GetAll();
            foreach (var project in projects)
            {
                // Check if project already has agent configuration
                bool needsInit = project.Agents.Wyvern.Provider == null &&
                                 !project.Agents.Wyrm.Enabled &&
                                 !project.Agents.Wyvern.Enabled &&
                                 !project.Agents.Drake.Enabled;

                if (needsInit)
                {
                    // Initialize with global defaults
                    project.Agents.Wyvern.Provider = globalConfig.GetProviderForAgent("wyvern");
                    project.Agents.Wyvern.Enabled = true;
                    project.Agents.Drake.Provider = globalConfig.GetProviderForAgent("wyvern");
                    project.Agents.Drake.Enabled = true;
                    project.Agents.Kobold.Provider = globalConfig.GetProviderForAgent("kobold");
                    project.Agents.Kobold.Enabled = true;
                    project.Agents.Wyrm.Enabled = true;
                    project.Agents.KoboldPlanner.Enabled = true;

                    await _repository.UpdateAsync(project);
                    _logger.LogInformation("Initialized configuration for project: {ProjectName}", project.Name);
                }
            }
        }

        /// <summary>
        /// Toggles whether an agent type is enabled for a project
        /// </summary>
        public async Task SetAgentEnabledAsync(string projectId, string agentType, bool enabled)
        {
            var project = _repository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            await _repository.SetAgentEnabledAsync(projectId, agentType, enabled);
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

            return _repository.IsAgentEnabled(projectId, agentType);
        }

        /// <summary>
        /// Sets the maximum parallel kobolds limit for a project
        /// </summary>
        public async Task SetMaxParallelKoboldsAsync(string projectId, int maxParallel)
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

            await _repository.SetAgentLimitAsync(projectId, "kobold", maxParallel);
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

            return _repository.GetMaxParallel(projectId, "kobold", defaultValue: 1);
        }
    }
}
