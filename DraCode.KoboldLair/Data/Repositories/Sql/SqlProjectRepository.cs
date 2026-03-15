using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQL-backed project repository using Birko.Data with SQLite.
    /// Replaces JSON file storage with transactional database writes.
    /// </summary>
    public class SqlProjectRepository : IProjectRepository
    {
        private readonly AsyncSqLiteModelRepository<ProjectEntity> _repository;
        private readonly ILogger<SqlProjectRepository>? _logger;

        // In-memory cache for fast reads (synced with DB on writes)
        private readonly object _lock = new();
        private List<Project> _cache = new();
        private bool _cacheLoaded = false;

        public SqlProjectRepository(string dbPath, ILogger<SqlProjectRepository>? logger = null)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<ProjectEntity>();

            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            _repository.SetSettings(new PasswordSettings(dbDir, dbFile));
            _logger?.LogInformation("SQLite project repository initialized at: {Path}", dbPath);
        }

        /// <summary>
        /// Ensures the database schema is created and cache is loaded.
        /// Must be called once at startup.
        /// </summary>
        public async Task InitializeAsync()
        {
            await _repository.InitAsync();
            await LoadCacheAsync();
            _logger?.LogInformation("Loaded {Count} project(s) from database", _cache.Count);
        }

        private async Task LoadCacheAsync()
        {
            var entities = await _repository.ReadAsync(filter: null, orderBy: null, limit: null, offset: null);
            lock (_lock)
            {
                _cache = entities.Select(EntityMapper.ToProject).ToList();
                _cacheLoaded = true;
            }
        }

        private void EnsureCache()
        {
            if (!_cacheLoaded)
                LoadCacheAsync().GetAwaiter().GetResult();
        }

        #region CRUD Operations

        public void Add(Project project)
        {
            AddAsync(project).GetAwaiter().GetResult();
        }

        public async Task AddAsync(Project project)
        {
            project.Timestamps.CreatedAt = DateTime.UtcNow;
            project.Timestamps.UpdatedAt = DateTime.UtcNow;

            var entity = EntityMapper.ToEntity(project);
            await _repository.CreateAsync(entity);

            lock (_lock)
            {
                _cache.Add(project);
            }

            _logger?.LogInformation("Added project: {ProjectId} - {ProjectName}", project.Id, project.Name);
        }

        public void Update(Project project)
        {
            UpdateAsync(project).GetAwaiter().GetResult();
        }

        public async Task UpdateAsync(Project project)
        {
            EnsureCache();
            project.Timestamps.UpdatedAt = DateTime.UtcNow;

            // Find existing entity by ProjectId
            var existing = await _repository.ReadAsync(e => e.ProjectId == project.Id, CancellationToken.None);
            if (existing == null)
                throw new InvalidOperationException($"Project not found: {project.Id}");

            EntityMapper.UpdateEntity(existing, project);
            await _repository.UpdateAsync(existing);

            lock (_lock)
            {
                var index = _cache.FindIndex(p => p.Id == project.Id);
                if (index >= 0)
                    _cache[index] = project;
            }

            _logger?.LogInformation("Updated project: {ProjectId} - {ProjectName}", project.Id, project.Name);
        }

        public void Delete(string id)
        {
            DeleteAsync(id).GetAwaiter().GetResult();
        }

        public async Task DeleteAsync(string id)
        {
            EnsureCache();
            var existing = await _repository.ReadAsync(e => e.ProjectId == id, CancellationToken.None);
            if (existing != null)
            {
                await _repository.DeleteAsync(existing);
                lock (_lock)
                {
                    _cache.RemoveAll(p => p.Id == id);
                }
                _logger?.LogInformation("Deleted project: {ProjectId}", id);
            }
        }

        #endregion

        #region Query Operations

        public Project? GetById(string id)
        {
            EnsureCache();
            lock (_lock)
            {
                return _cache.FirstOrDefault(p => p.Id == id);
            }
        }

        public Project? GetByName(string name)
        {
            EnsureCache();
            lock (_lock)
            {
                var project = _cache.FirstOrDefault(p =>
                    p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (project != null)
                    return project;

                var sanitizedSearchName = SanitizeProjectName(name);
                return _cache.FirstOrDefault(p =>
                    SanitizeProjectName(p.Name).Equals(sanitizedSearchName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public Project? GetBySpecificationPath(string specPath)
        {
            EnsureCache();
            lock (_lock)
            {
                var normalizedPath = Path.GetFullPath(specPath);
                return _cache.FirstOrDefault(p =>
                    string.Equals(p.Paths.Specification, normalizedPath, StringComparison.OrdinalIgnoreCase));
            }
        }

        public List<Project> GetAll()
        {
            EnsureCache();
            lock (_lock)
            {
                return new List<Project>(_cache);
            }
        }

        public List<Project> GetByStatus(ProjectStatus status)
        {
            EnsureCache();
            lock (_lock)
            {
                return _cache.Where(p => p.Status == status).ToList();
            }
        }

        public List<Project> GetByStatuses(params ProjectStatus[] statuses)
        {
            EnsureCache();
            lock (_lock)
            {
                return _cache.Where(p => statuses.Contains(p.Status)).ToList();
            }
        }

        public int Count()
        {
            EnsureCache();
            lock (_lock)
            {
                return _cache.Count;
            }
        }

        #endregion

        #region Agent Configuration

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

        public int GetMaxParallel(string projectId, string agentType, int defaultValue = 1)
        {
            var agentConfig = GetAgentConfig(projectId, agentType);
            return agentConfig?.MaxParallel ?? defaultValue;
        }

        public async Task SetAgentLimitAsync(string projectId, string agentType, int maxParallel)
        {
            if (maxParallel < 1)
                throw new ArgumentException("Max parallel must be at least 1", nameof(maxParallel));

            var project = GetById(projectId)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            GetAgentConfigInternal(project, agentType).MaxParallel = maxParallel;
            await UpdateAsync(project);
        }

        public async Task SetAgentTimeoutAsync(string projectId, string agentType, int timeoutSeconds)
        {
            if (timeoutSeconds < 0)
                throw new ArgumentException("Timeout must be non-negative", nameof(timeoutSeconds));

            var project = GetById(projectId)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            GetAgentConfigInternal(project, agentType).Timeout = timeoutSeconds;
            await UpdateAsync(project);
        }

        public int GetAgentTimeout(string projectId, string agentType)
        {
            var agentConfig = GetAgentConfig(projectId, agentType);
            return agentConfig?.Timeout ?? 0;
        }

        public async Task SetAgentEnabledAsync(string projectId, string agentType, bool enabled)
        {
            var project = GetById(projectId)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            GetAgentConfigInternal(project, agentType).Enabled = enabled;
            await UpdateAsync(project);
        }

        public bool IsAgentEnabled(string projectId, string agentType)
        {
            var agentConfig = GetAgentConfig(projectId, agentType);
            return agentConfig?.Enabled ?? false;
        }

        public async Task SetProjectProviderAsync(string projectId, string agentType, string? provider, string? model = null)
        {
            var project = GetById(projectId)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            var agentConfig = GetAgentConfigInternal(project, agentType);
            agentConfig.Provider = provider;
            agentConfig.Model = model;
            await UpdateAsync(project);
        }

        public string? GetProjectProvider(string projectId, string agentType)
        {
            return GetAgentConfig(projectId, agentType)?.Provider;
        }

        public string? GetProjectModel(string projectId, string agentType)
        {
            return GetAgentConfig(projectId, agentType)?.Model;
        }

        #endregion

        #region Security / External Paths

        public async Task AddAllowedExternalPathAsync(string projectId, string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            var canonicalPath = Path.GetFullPath(path);
            var project = GetById(projectId)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            if (!project.Security.AllowedExternalPaths.Contains(canonicalPath, StringComparer.OrdinalIgnoreCase))
            {
                project.Security.AllowedExternalPaths.Add(canonicalPath);
            }
            await UpdateAsync(project);
        }

        public async Task<bool> RemoveAllowedExternalPathAsync(string projectId, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            var canonicalPath = Path.GetFullPath(path);
            var project = GetById(projectId);
            if (project == null) return false;

            var existing = project.Security.AllowedExternalPaths
                .FirstOrDefault(p => p.Equals(canonicalPath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                project.Security.AllowedExternalPaths.Remove(existing);
                await UpdateAsync(project);
                return true;
            }
            return false;
        }

        public IReadOnlyList<string> GetAllowedExternalPaths(string projectId)
        {
            var project = GetById(projectId);
            return project?.Security.AllowedExternalPaths.ToList().AsReadOnly()
                ?? (IReadOnlyList<string>)Array.Empty<string>();
        }

        public async Task SetSandboxModeAsync(string projectId, string mode)
        {
            var validModes = new[] { "workspace", "relaxed", "strict" };
            if (!validModes.Contains(mode.ToLowerInvariant()))
                throw new ArgumentException($"Invalid sandbox mode: {mode}. Valid modes: {string.Join(", ", validModes)}");

            var project = GetById(projectId)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            project.Security.SandboxMode = mode.ToLowerInvariant();
            await UpdateAsync(project);
        }

        public string GetSandboxMode(string projectId)
        {
            var project = GetById(projectId);
            return project?.Security.SandboxMode ?? "workspace";
        }

        #endregion

        #region Helpers

        private static AgentConfig GetAgentConfigInternal(Project project, string agentType)
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

        private static string SanitizeProjectName(string projectName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", projectName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return sanitized.Trim().Replace(" ", "-").ToLowerInvariant();
        }

        #endregion
    }
}
