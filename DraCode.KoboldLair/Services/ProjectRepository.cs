using System.Text.Json;
using DraCode.KoboldLair.Models.Projects;

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
        /// Loads projects from disk
        /// </summary>
        private void LoadProjects()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_projectsFilePath))
                    {
                        var json = File.ReadAllText(_projectsFilePath);
                        _projects = JsonSerializer.Deserialize<List<Project>>(json) ?? new List<Project>();
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
        /// Saves projects to disk
        /// </summary>
        private void SaveProjects()
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_projects, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(_projectsFilePath, json);
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
        /// Adds a new project
        /// </summary>
        public void Add(Project project)
        {
            lock (_lock)
            {
                project.CreatedAt = DateTime.UtcNow;
                project.UpdatedAt = DateTime.UtcNow;
                _projects.Add(project);
                SaveProjects();
                _logger?.LogInformation("Added project: {ProjectId} - {ProjectName}", project.Id, project.Name);
            }
        }

        /// <summary>
        /// Updates an existing project
        /// </summary>
        public void Update(Project project)
        {
            lock (_lock)
            {
                var index = _projects.FindIndex(p => p.Id == project.Id);
                if (index >= 0)
                {
                    project.UpdatedAt = DateTime.UtcNow;
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
        /// Gets a project by specification path
        /// </summary>
        public Project? GetBySpecificationPath(string specPath)
        {
            lock (_lock)
            {
                return _projects.FirstOrDefault(p => p.SpecificationPath == specPath);
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
        /// Deletes a project
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
        /// Gets count of projects
        /// </summary>
        public int Count()
        {
            lock (_lock)
            {
                return _projects.Count;
            }
        }
    }
}
