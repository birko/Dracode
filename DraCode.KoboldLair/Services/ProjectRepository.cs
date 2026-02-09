using System.Text.Json;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Configuration;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Repository for persisting projects to JSON file storage.
    /// Thread-safe for concurrent access.
    /// </summary>
    public class ProjectRepository
    {
        private readonly string _projectsDirectory;
        private readonly string _projectsFilePath;
        private readonly object _lock = new object();
        private readonly ILogger<ProjectRepository>? _logger;

        private List<Project> _projects = new();

        public ProjectRepository(string projectsDirectory = "./projects", ILogger<ProjectRepository>? logger = null)
        {
            _projectsDirectory = projectsDirectory;
            _projectsFilePath = Path.Combine(_projectsDirectory, "projects.json");
            _logger = logger;

            // Ensure directory exists
            if (!Directory.Exists(_projectsDirectory))
            {
                Directory.CreateDirectory(_projectsDirectory);
                _logger?.LogInformation("Created projects directory: {Path}", _projectsDirectory);
            }

            // Load existing projects
            LoadProjects();
        }

        /// <summary>
        /// Loads projects from disk (synchronous - called from constructor)
        /// </summary>
        private void LoadProjects()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_projectsFilePath))
                    {
                        var json = File.ReadAllTextAsync(_projectsFilePath).GetAwaiter().GetResult();
                        _projects = JsonSerializer.Deserialize<List<Project>>(json) ?? new List<Project>();

                        // Resolve all relative paths to absolute paths and ensure timestamps are set
                        foreach (var project in _projects)
                        {
                            ResolveProjectPaths(project);
                            EnsureTimestamps(project);
                        }

                        _logger?.LogInformation("Loaded {Count} project(s) from storage", _projects.Count);
                    }
                    else
                    {
                        _projects = new List<Project>();
                        _logger?.LogInformation("No existing projects file found, starting fresh");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error loading projects from storage, starting with empty list");
                    _projects = new List<Project>();
                }
            }
        }

        /// <summary>
        /// Loads projects from disk asynchronously
        /// </summary>
        private async Task LoadProjectsAsync()
        {
            try
            {
                if (File.Exists(_projectsFilePath))
                {
                    var json = await File.ReadAllTextAsync(_projectsFilePath);
                    lock (_lock)
                    {
                        _projects = JsonSerializer.Deserialize<List<Project>>(json) ?? new List<Project>();

                        // Resolve all relative paths to absolute paths
                        foreach (var project in _projects)
                        {
                            ResolveProjectPaths(project);
                        }
                    }

                    _logger?.LogInformation("Loaded {Count} project(s) from storage", _projects.Count);
                }
                else
                {
                    lock (_lock)
                    {
                        _projects = new List<Project>();
                    }
                    _logger?.LogInformation("No existing projects file found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading projects from storage, starting with empty list");
                lock (_lock)
                {
                    _projects = new List<Project>();
                }
            }
        }

        /// <summary>
        /// Ensures that project timestamps are set. If any required timestamps are null,
        /// sets them to a reasonable default value.
        /// </summary>
        private void EnsureTimestamps(Project project)
        {
            // If both CreatedAt and UpdatedAt are null, set them to the current time
            // This handles legacy projects that didn't have timestamps
            if (!project.Timestamps.CreatedAt.HasValue && !project.Timestamps.UpdatedAt.HasValue)
            {
                var now = DateTime.UtcNow;
                project.Timestamps.CreatedAt = now;
                project.Timestamps.UpdatedAt = now;
            }
            // If only CreatedAt is null, set it to UpdatedAt
            else if (!project.Timestamps.CreatedAt.HasValue && project.Timestamps.UpdatedAt.HasValue)
            {
                project.Timestamps.CreatedAt = project.Timestamps.UpdatedAt.Value;
            }
            // If only UpdatedAt is null, set it to CreatedAt
            else if (project.Timestamps.CreatedAt.HasValue && !project.Timestamps.UpdatedAt.HasValue)
            {
                project.Timestamps.UpdatedAt = project.Timestamps.CreatedAt.Value;
            }
        }

        /// <summary>
        /// Resolves relative paths in a project to absolute paths based on the projects directory.
        /// This ensures paths work correctly regardless of the application's working directory.
        /// </summary>
        private void ResolveProjectPaths(Project project)
        {
            // Resolve Paths.Specification
            if (!string.IsNullOrEmpty(project.Paths.Specification))
            {
                project.Paths.Specification = ResolvePath(project.Paths.Specification);
            }

            // Resolve Paths.Output
            if (!string.IsNullOrEmpty(project.Paths.Output))
            {
                project.Paths.Output = ResolvePath(project.Paths.Output);
            }

            // Resolve Paths.Analysis
            if (!string.IsNullOrEmpty(project.Paths.Analysis))
            {
                project.Paths.Analysis = ResolvePath(project.Paths.Analysis);
            }

            // Resolve Paths.TaskFiles
            if (project.Paths.TaskFiles.Count > 0)
            {
                var resolvedTaskFiles = new Dictionary<string, string>();
                foreach (var (area, path) in project.Paths.TaskFiles)
                {
                    resolvedTaskFiles[area] = ResolvePath(path);
                }
                project.Paths.TaskFiles = resolvedTaskFiles;
            }
        }

        /// <summary>
        /// Resolves a path relative to the projects directory if it's not already absolute.
        /// Handles paths starting with "./" or relative paths.
        /// </summary>
        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            // Remove leading ./ or .\ if present
            var cleanPath = path.TrimStart('.', '/', '\\');
            return Path.GetFullPath(Path.Combine(_projectsDirectory, cleanPath));
        }

        /// <summary>
        /// Saves projects to disk (synchronous - prefer SaveProjectsAsync)
        /// </summary>
        private void SaveProjects()
        {
            lock (_lock)
            {
                try
                {
                    // Create copies with relative paths for storage (keeps JSON portable)
                    var projectsToSave = _projects.Select(p => CreateProjectWithRelativePaths(p)).ToList();

                    var json = JsonSerializer.Serialize(projectsToSave, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllTextAsync(_projectsFilePath, json).GetAwaiter().GetResult();
                    _logger?.LogDebug("Saved {Count} project(s) to storage", _projects.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error saving projects to storage");
                    throw;
                }
            }
        }

        /// <summary>
        /// Saves projects to disk asynchronously
        /// </summary>
        private async Task SaveProjectsAsync()
        {
            List<Project> projectsToSave;
            int count;

            lock (_lock)
            {
                // Create copies with relative paths for storage (keeps JSON portable)
                projectsToSave = _projects.Select(p => CreateProjectWithRelativePaths(p)).ToList();
                count = _projects.Count;
            }

            try
            {
                var json = JsonSerializer.Serialize(projectsToSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_projectsFilePath, json);
                _logger?.LogDebug("Saved {Count} project(s) to storage", count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving projects to storage");
                throw;
            }
        }

        /// <summary>
        /// Creates a copy of a project with paths converted to relative for storage.
        /// </summary>
        private Project CreateProjectWithRelativePaths(Project project)
        {
            return new Project
            {
                Id = project.Id,
                Name = project.Name,
                Status = project.Status,
                Paths = new ProjectPaths
                {
                    Specification = MakeRelativePath(project.Paths.Specification),
                    Output = MakeRelativePath(project.Paths.Output),
                    Analysis = MakeRelativePath(project.Paths.Analysis),
                    TaskFiles = project.Paths.TaskFiles.ToDictionary(kv => kv.Key, kv => MakeRelativePath(kv.Value))
                },
                Timestamps = new ProjectTimestamps
                {
                    CreatedAt = project.Timestamps.CreatedAt,
                    UpdatedAt = project.Timestamps.UpdatedAt,
                    AnalyzedAt = project.Timestamps.AnalyzedAt,
                    LastProcessedAt = project.Timestamps.LastProcessedAt
                },
                Tracking = new ProjectTracking
                {
                    PendingAreas = new List<string>(project.Tracking.PendingAreas),
                    ErrorMessage = project.Tracking.ErrorMessage,
                    LastProcessedContentHash = project.Tracking.LastProcessedContentHash,
                    SpecificationId = project.Tracking.SpecificationId,
                    WyvernId = project.Tracking.WyvernId
                },
                Agents = project.Agents,
                Security = project.Security,
                Metadata = new Dictionary<string, string>(project.Metadata)
            };
        }

        /// <summary>
        /// Converts an absolute path to a path relative to the projects directory.
        /// Returns the original path if it cannot be made relative.
        /// </summary>
        private string MakeRelativePath(string? absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return absolutePath ?? "";

            try
            {
                var fullProjectsDir = Path.GetFullPath(_projectsDirectory);
                var fullPath = Path.GetFullPath(absolutePath);

                // Check if the path is under the projects directory
                if (fullPath.StartsWith(fullProjectsDir, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = Path.GetRelativePath(fullProjectsDir, fullPath);
                    return "./" + relativePath.Replace('\\', '/');
                }

                // Path is outside projects directory, keep as absolute
                return absolutePath;
            }
            catch
            {
                return absolutePath;
            }
        }

        /// <summary>
        /// Adds a new project (synchronous - prefer AddAsync)
        /// </summary>
        public void Add(Project project)
        {
            lock (_lock)
            {
                project.Timestamps.CreatedAt = DateTime.UtcNow;
                project.Timestamps.UpdatedAt = DateTime.UtcNow;
                _projects.Add(project);
                SaveProjects();
                _logger?.LogInformation("Added project: {ProjectId} - {ProjectName}", project.Id, project.Name);
            }
        }

        /// <summary>
        /// Adds a new project asynchronously
        /// </summary>
        public async Task AddAsync(Project project)
        {
            lock (_lock)
            {
                project.Timestamps.CreatedAt = DateTime.UtcNow;
                project.Timestamps.UpdatedAt = DateTime.UtcNow;
                _projects.Add(project);
            }
            await SaveProjectsAsync();
            _logger?.LogInformation("Added project: {ProjectId} - {ProjectName}", project.Id, project.Name);
        }

        /// <summary>
        /// Updates an existing project (synchronous - prefer UpdateAsync)
        /// </summary>
        public void Update(Project project)
        {
            lock (_lock)
            {
                var index = _projects.FindIndex(p => p.Id == project.Id);
                if (index >= 0)
                {
                    project.Timestamps.UpdatedAt = DateTime.UtcNow;
                    _projects[index] = project;
                    SaveProjects();
                    _logger?.LogInformation("Updated project: {ProjectId} - {ProjectName}", project.Id, project.Name);
                }
                else
                {
                    throw new InvalidOperationException($"Project not found: {project.Id}");
                }
            }
        }

        /// <summary>
        /// Updates an existing project asynchronously
        /// </summary>
        public async Task UpdateAsync(Project project)
        {
            lock (_lock)
            {
                var index = _projects.FindIndex(p => p.Id == project.Id);
                if (index >= 0)
                {
                    project.Timestamps.UpdatedAt = DateTime.UtcNow;
                    _projects[index] = project;
                }
                else
                {
                    throw new InvalidOperationException($"Project not found: {project.Id}");
                }
            }
            await SaveProjectsAsync();
            _logger?.LogInformation("Updated project: {ProjectId} - {ProjectName}", project.Id, project.Name);
        }

        /// <summary>
        /// Gets a project by ID
        /// </summary>
        public Project? GetById(string id)
        {
            lock (_lock)
            {
                return _projects.FirstOrDefault(p => p.Id == id);
            }
        }

        /// <summary>
        /// Gets a project by name (case-insensitive, also matches sanitized folder name)
        /// </summary>
        public Project? GetByName(string name)
        {
            lock (_lock)
            {
                // First try exact match (case-insensitive)
                var project = _projects.FirstOrDefault(p =>
                    p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (project != null)
                    return project;

                // Also try matching against sanitized version of stored names
                // This handles the case where the folder name (sanitized) is used for lookup
                // but the project was stored with its original name
                var sanitizedSearchName = SanitizeProjectName(name);
                return _projects.FirstOrDefault(p =>
                    SanitizeProjectName(p.Name).Equals(sanitizedSearchName, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Sanitizes project name for comparison (matches ProjectService.SanitizeProjectName)
        /// </summary>
        private static string SanitizeProjectName(string projectName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", projectName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim().Replace(" ", "-").ToLowerInvariant();
        }

        /// <summary>
        /// Gets a project by specification path.
        /// Normalizes the input path for comparison since stored paths are absolute.
        /// </summary>
        public Project? GetBySpecificationPath(string specPath)
        {
            lock (_lock)
            {
                // Resolve the input path for comparison
                var normalizedPath = ResolvePath(specPath);
                return _projects.FirstOrDefault(p =>
                    string.Equals(p.Paths.Specification, normalizedPath, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Gets all projects
        /// </summary>
        public List<Project> GetAll()
        {
            lock (_lock)
            {
                return new List<Project>(_projects);
            }
        }

        /// <summary>
        /// Gets projects by status
        /// </summary>
        public List<Project> GetByStatus(ProjectStatus status)
        {
            lock (_lock)
            {
                return _projects.Where(p => p.Status == status).ToList();
            }
        }

        /// <summary>
        /// Gets projects with multiple statuses
        /// </summary>
        public List<Project> GetByStatuses(params ProjectStatus[] statuses)
        {
            lock (_lock)
            {
                return _projects.Where(p => statuses.Contains(p.Status)).ToList();
            }
        }

        /// <summary>
        /// Deletes a project (synchronous - prefer DeleteAsync)
        /// </summary>
        public void Delete(string id)
        {
            lock (_lock)
            {
                var project = _projects.FirstOrDefault(p => p.Id == id);
                if (project != null)
                {
                    _projects.Remove(project);
                    SaveProjects();
                    _logger?.LogInformation("Deleted project: {ProjectId}", id);
                }
            }
        }

        /// <summary>
        /// Deletes a project asynchronously
        /// </summary>
        public async Task DeleteAsync(string id)
        {
            bool deleted = false;
            lock (_lock)
            {
                var project = _projects.FirstOrDefault(p => p.Id == id);
                if (project != null)
                {
                    _projects.Remove(project);
                    deleted = true;
                }
            }
            if (deleted)
            {
                await SaveProjectsAsync();
                _logger?.LogInformation("Deleted project: {ProjectId}", id);
            }
        }

        /// <summary>
        /// Gets count of projects
        /// </summary>
        public int Count()
        {
            lock (_lock)
            {
                return _projects.Count;
            }
        }

        // ===== Agent Configuration Methods (consolidated from ProjectConfigurationService) =====

        /// <summary>
        /// Gets the agent configuration for a specific agent type in a project
        /// </summary>
        public AgentConfig? GetAgentConfig(string projectId, string agentType)
        {
            var project = GetById(projectId);
            if (project == null) return null;

            return agentType.ToLowerInvariant() switch
            {
                "wyrm" => project.Agents.Wyrm,
                "wyvern" => project.Agents.Wyvern,
                "drake" => project.Agents.Drake,
                "kobold-planner" or "koboldplanner" or "planner" => project.Agents.KoboldPlanner,
                "kobold" => project.Agents.Kobold,
                _ => null
            };
        }

        /// <summary>
        /// Gets the maximum parallel agents for a specific type in a project
        /// </summary>
        public int GetMaxParallel(string projectId, string agentType, int defaultValue = 1)
        {
            var agentConfig = GetAgentConfig(projectId, agentType);
            return agentConfig?.MaxParallel ?? defaultValue;
        }

        /// <summary>
        /// Sets the maximum parallel limit for a specific agent type in a project
        /// </summary>
        public async Task SetAgentLimitAsync(string projectId, string agentType, int maxParallel)
        {
            if (maxParallel < 1)
                throw new ArgumentException("Max parallel must be at least 1", nameof(maxParallel));

            var project = GetById(projectId);
            if (project == null)
                throw new InvalidOperationException($"Project not found: {projectId}");

            var agentConfig = GetAgentConfigInternal(project, agentType);
            agentConfig.MaxParallel = maxParallel;

            await UpdateAsync(project);
        }

        /// <summary>
        /// Sets the timeout for a specific agent type in a project
        /// </summary>
        public async Task SetAgentTimeoutAsync(string projectId, string agentType, int timeoutSeconds)
        {
            if (timeoutSeconds < 0)
                throw new ArgumentException("Timeout must be non-negative", nameof(timeoutSeconds));

            var project = GetById(projectId);
            if (project == null)
                throw new InvalidOperationException($"Project not found: {projectId}");

            var agentConfig = GetAgentConfigInternal(project, agentType);
            agentConfig.Timeout = timeoutSeconds;

            await UpdateAsync(project);
        }

        /// <summary>
        /// Gets the timeout for a specific agent type in a project
        /// </summary>
        public int GetAgentTimeout(string projectId, string agentType)
        {
            var agentConfig = GetAgentConfig(projectId, agentType);
            return agentConfig?.Timeout ?? 0;
        }

        /// <summary>
        /// Sets whether an agent is enabled for a project
        /// </summary>
        public async Task SetAgentEnabledAsync(string projectId, string agentType, bool enabled)
        {
            var project = GetById(projectId);
            if (project == null)
                throw new InvalidOperationException($"Project not found: {projectId}");

            var agentConfig = GetAgentConfigInternal(project, agentType);
            agentConfig.Enabled = enabled;

            await UpdateAsync(project);
        }

        /// <summary>
        /// Checks if an agent is enabled for a project
        /// </summary>
        public bool IsAgentEnabled(string projectId, string agentType)
        {
            var agentConfig = GetAgentConfig(projectId, agentType);
            return agentConfig?.Enabled ?? false;
        }

        /// <summary>
        /// Sets provider override for a project agent
        /// </summary>
        public async Task SetProjectProviderAsync(string projectId, string agentType, string? provider, string? model = null)
        {
            var project = GetById(projectId);
            if (project == null)
                throw new InvalidOperationException($"Project not found: {projectId}");

            var agentConfig = GetAgentConfigInternal(project, agentType);
            agentConfig.Provider = provider;
            agentConfig.Model = model;

            await UpdateAsync(project);
        }

        /// <summary>
        /// Gets provider override for a specific agent type in a project
        /// </summary>
        public string? GetProjectProvider(string projectId, string agentType)
        {
            var agentConfig = GetAgentConfig(projectId, agentType);
            return agentConfig?.Provider;
        }

        /// <summary>
        /// Gets model override for a specific agent type in a project
        /// </summary>
        public string? GetProjectModel(string projectId, string agentType)
        {
            var agentConfig = GetAgentConfig(projectId, agentType);
            return agentConfig?.Model;
        }

        /// <summary>
        /// Adds an allowed external path for a project
        /// </summary>
        public async Task AddAllowedExternalPathAsync(string projectId, string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            var normalizedPath = Path.GetFullPath(path);
            var project = GetById(projectId);
            if (project == null)
                throw new InvalidOperationException($"Project not found: {projectId}");

            lock (_lock)
            {
                if (!project.Security.AllowedExternalPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
                {
                    project.Security.AllowedExternalPaths.Add(normalizedPath);
                    _logger?.LogInformation("Added allowed external path for {Project}: {Path}", projectId, normalizedPath);
                }
            }
            await UpdateAsync(project);
        }

        /// <summary>
        /// Removes an allowed external path from a project
        /// </summary>
        public async Task<bool> RemoveAllowedExternalPathAsync(string projectId, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var normalizedPath = Path.GetFullPath(path);
            var project = GetById(projectId);
            if (project == null)
                return false;

            bool removed = false;
            lock (_lock)
            {
                var existingPath = project.Security.AllowedExternalPaths
                    .FirstOrDefault(p => p.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

                if (existingPath != null)
                {
                    project.Security.AllowedExternalPaths.Remove(existingPath);
                    _logger?.LogInformation("Removed allowed external path for {Project}: {Path}", projectId, normalizedPath);
                    removed = true;
                }
            }

            if (removed)
            {
                await UpdateAsync(project);
            }
            return removed;
        }

        /// <summary>
        /// Gets the allowed external paths for a project
        /// </summary>
        public IReadOnlyList<string> GetAllowedExternalPaths(string projectId)
        {
            var project = GetById(projectId);
            if (project == null)
                return Array.Empty<string>();

            lock (_lock)
            {
                return project.Security.AllowedExternalPaths.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Sets the sandbox mode for a project
        /// </summary>
        public async Task SetSandboxModeAsync(string projectId, string mode)
        {
            var validModes = new[] { "workspace", "relaxed", "strict" };
            if (!validModes.Contains(mode.ToLowerInvariant()))
                throw new ArgumentException($"Invalid sandbox mode: {mode}. Valid modes: {string.Join(", ", validModes)}");

            var project = GetById(projectId);
            if (project == null)
                throw new InvalidOperationException($"Project not found: {projectId}");

            project.Security.SandboxMode = mode.ToLowerInvariant();
            await UpdateAsync(project);
        }

        /// <summary>
        /// Gets the sandbox mode for a project
        /// </summary>
        public string GetSandboxMode(string projectId)
        {
            var project = GetById(projectId);
            return project?.Security.SandboxMode ?? "workspace";
        }

        /// <summary>
        /// Internal helper to get agent config with proper type checking
        /// </summary>
        private AgentConfig GetAgentConfigInternal(Project project, string agentType)
        {
            return agentType.ToLowerInvariant() switch
            {
                "wyrm" => project.Agents.Wyrm,
                "wyvern" => project.Agents.Wyvern,
                "drake" => project.Agents.Drake,
                "kobold-planner" or "koboldplanner" or "planner" => project.Agents.KoboldPlanner,
                "kobold" => project.Agents.Kobold,
                _ => throw new ArgumentException($"Unknown agent type: {agentType}")
            };
        }
    }
}
