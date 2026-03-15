using System.Text.Json;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Migrations
{
    /// <summary>
    /// Migrates existing JSON file storage to SQLite database.
    /// Reads projects.json and per-area task JSON files, writes to SQLite via Birko.Data repositories.
    /// </summary>
    public class JsonToSqlMigration
    {
        private readonly string _projectsPath;
        private readonly SqlProjectRepository _projectRepo;
        private readonly SqlTaskRepository _taskRepo;
        private readonly ILogger? _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public JsonToSqlMigration(
            string projectsPath,
            SqlProjectRepository projectRepo,
            SqlTaskRepository taskRepo,
            ILogger? logger = null)
        {
            _projectsPath = projectsPath;
            _projectRepo = projectRepo;
            _taskRepo = taskRepo;
            _logger = logger;
        }

        /// <summary>
        /// Creates a migration instance from a DataStorageConfig.
        /// Initializes the SQLite repositories.
        /// </summary>
        public static async Task<JsonToSqlMigration> CreateAsync(
            DataStorageConfig config,
            ILoggerFactory? loggerFactory = null)
        {
            var dbPath = Path.IsPathRooted(config.SqLitePath)
                ? config.SqLitePath
                : Path.Combine(config.ProjectsPath, config.SqLitePath);

            var projectRepo = new SqlProjectRepository(dbPath, loggerFactory?.CreateLogger<SqlProjectRepository>());
            await projectRepo.InitializeAsync();

            var taskRepo = new SqlTaskRepository(dbPath, loggerFactory?.CreateLogger<SqlTaskRepository>());
            await taskRepo.InitializeAsync();

            return new JsonToSqlMigration(
                config.ProjectsPath, projectRepo, taskRepo,
                loggerFactory?.CreateLogger<JsonToSqlMigration>());
        }

        /// <summary>
        /// Runs the full migration from JSON files to SQLite.
        /// </summary>
        /// <param name="dryRun">If true, only reports what would be migrated without writing.</param>
        /// <returns>Migration result with counts and any errors.</returns>
        public async Task<MigrationResult> MigrateAsync(bool dryRun = false)
        {
            var result = new MigrationResult();
            _logger?.LogInformation("Starting JSON → SQLite migration (dryRun: {DryRun})...", dryRun);

            // 1. Migrate projects
            await MigrateProjectsAsync(result, dryRun);

            // 2. Migrate tasks for each project
            await MigrateTasksAsync(result, dryRun);

            _logger?.LogInformation(
                "Migration complete: {ProjectCount} projects, {TaskCount} tasks migrated. {ErrorCount} errors.",
                result.ProjectsMigrated, result.TasksMigrated, result.Errors.Count);

            return result;
        }

        private async Task MigrateProjectsAsync(MigrationResult result, bool dryRun)
        {
            var projectsFile = Path.Combine(_projectsPath, "projects.json");
            if (!File.Exists(projectsFile))
            {
                _logger?.LogWarning("No projects.json found at {Path}", projectsFile);
                return;
            }

            try
            {
                var json = await File.ReadAllTextAsync(projectsFile);
                var projects = JsonSerializer.Deserialize<List<Project>>(json, JsonOptions);

                if (projects == null || projects.Count == 0)
                {
                    _logger?.LogInformation("No projects found in projects.json");
                    return;
                }

                _logger?.LogInformation("Found {Count} projects to migrate", projects.Count);

                foreach (var project in projects)
                {
                    try
                    {
                        // Resolve relative paths
                        ResolveProjectPaths(project);

                        if (!dryRun)
                        {
                            // Check if already migrated
                            var existing = _projectRepo.GetById(project.Id);
                            if (existing != null)
                            {
                                _logger?.LogDebug("Project {Name} already exists in DB, updating", project.Name);
                                await _projectRepo.UpdateAsync(project);
                            }
                            else
                            {
                                await _projectRepo.AddAsync(project);
                            }
                        }

                        result.ProjectsMigrated++;
                        _logger?.LogDebug("Migrated project: {Name} ({Id})", project.Name, project.Id);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Project '{project.Name}': {ex.Message}");
                        _logger?.LogError(ex, "Failed to migrate project {Name}", project.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"projects.json: {ex.Message}");
                _logger?.LogError(ex, "Failed to read projects.json");
            }
        }

        private async Task MigrateTasksAsync(MigrationResult result, bool dryRun)
        {
            var projectsFile = Path.Combine(_projectsPath, "projects.json");
            if (!File.Exists(projectsFile))
                return;

            try
            {
                var json = await File.ReadAllTextAsync(projectsFile);
                var projects = JsonSerializer.Deserialize<List<Project>>(json, JsonOptions);
                if (projects == null) return;

                foreach (var project in projects)
                {
                    ResolveProjectPaths(project);

                    foreach (var (areaName, taskFilePath) in project.Paths.TaskFiles)
                    {
                        var resolvedPath = ResolvePath(taskFilePath);
                        var jsonPath = Path.ChangeExtension(resolvedPath, ".json");

                        if (!File.Exists(jsonPath))
                        {
                            // Try markdown fallback
                            if (File.Exists(resolvedPath))
                            {
                                await MigrateTaskFileAsync(project.Id, areaName, resolvedPath, isJson: false, result, dryRun);
                            }
                            continue;
                        }

                        await MigrateTaskFileAsync(project.Id, areaName, jsonPath, isJson: true, result, dryRun);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Task migration: {ex.Message}");
                _logger?.LogError(ex, "Failed to migrate tasks");
            }
        }

        private async Task MigrateTaskFileAsync(
            string projectId, string areaName, string filePath, bool isJson,
            MigrationResult result, bool dryRun)
        {
            try
            {
                var tracker = new TaskTracker();

                if (isJson)
                {
                    tracker.LoadFromJsonFile(filePath);
                }
                else
                {
                    tracker.LoadFromFile(filePath);
                }

                var tasks = tracker.GetAllTasks();
                _logger?.LogDebug("Found {Count} tasks in {Area} for project {ProjectId}",
                    tasks.Count, areaName, projectId);

                foreach (var task in tasks)
                {
                    try
                    {
                        task.ProjectId = projectId;

                        if (!dryRun)
                        {
                            var existing = await _taskRepo.GetByIdAsync(task.Id);
                            if (existing != null)
                            {
                                await _taskRepo.UpdateTaskAsync(task);
                            }
                            else
                            {
                                await _taskRepo.AddTaskAsync(projectId, areaName, task);
                            }
                        }

                        result.TasksMigrated++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Task '{task.Id}' in {areaName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Task file '{filePath}': {ex.Message}");
                _logger?.LogError(ex, "Failed to migrate task file {Path}", filePath);
            }
        }

        private void ResolveProjectPaths(Project project)
        {
            if (!string.IsNullOrEmpty(project.Paths.Specification))
                project.Paths.Specification = ResolvePath(project.Paths.Specification);
            if (!string.IsNullOrEmpty(project.Paths.Output))
                project.Paths.Output = ResolvePath(project.Paths.Output);
            if (!string.IsNullOrEmpty(project.Paths.Analysis))
                project.Paths.Analysis = ResolvePath(project.Paths.Analysis);

            var resolved = new Dictionary<string, string>();
            foreach (var (area, path) in project.Paths.TaskFiles)
            {
                resolved[area] = ResolvePath(path);
            }
            project.Paths.TaskFiles = resolved;
        }

        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            var cleanPath = path.TrimStart('.', '/', '\\');
            return Path.GetFullPath(Path.Combine(_projectsPath, cleanPath));
        }
    }

    /// <summary>
    /// Result of a JSON → SQLite migration run.
    /// </summary>
    public class MigrationResult
    {
        public int ProjectsMigrated { get; set; }
        public int TasksMigrated { get; set; }
        public List<string> Errors { get; set; } = new();
        public bool Success => Errors.Count == 0;
    }
}
