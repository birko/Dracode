using System.Text.Json;
using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Data.Migrations;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Integration tests for JSON → SQLite migration tool.
/// Sets up a fake JSON project directory, runs migration, verifies DB contents.
/// </summary>
public class JsonToSqlMigrationTests : IAsyncLifetime
{
    private string _tempDir = null!;
    private string _dbPath = null!;
    private SqlProjectRepository _projectRepo = null!;
    private SqlTaskRepository _taskRepo = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dracode_migration_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _dbPath = Path.Combine(_tempDir, "test.db");
        _projectRepo = new SqlProjectRepository(_dbPath);
        await _projectRepo.InitializeAsync();
        _taskRepo = new SqlTaskRepository(_dbPath);
        await _taskRepo.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private void SetupJsonFiles(List<Project> projects, Dictionary<string, List<TaskRecord>>? tasksByArea = null)
    {
        // Write projects.json
        var projectsJson = JsonSerializer.Serialize(projects, JsonOptions);
        File.WriteAllText(Path.Combine(_tempDir, "projects.json"), projectsJson);

        // Write task files per area
        if (tasksByArea != null)
        {
            foreach (var (areaPath, tasks) in tasksByArea)
            {
                var fullPath = Path.Combine(_tempDir, areaPath);
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var tasksJson = JsonSerializer.Serialize(tasks, JsonOptions);
                File.WriteAllText(fullPath, tasksJson);
            }
        }
    }

    [Fact]
    public async Task Migrate_ShouldTransferProjects()
    {
        var projects = new List<Project>
        {
            new()
            {
                Id = "p1",
                Name = "Project Alpha",
                Status = ProjectStatus.Analyzed,
                Paths = new ProjectPaths
                {
                    Specification = "./project-alpha/specification.md",
                    Output = "./project-alpha/workspace",
                    TaskFiles = new Dictionary<string, string>()
                },
                Timestamps = new ProjectTimestamps
                {
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        };

        SetupJsonFiles(projects);

        var migration = new JsonToSqlMigration(_tempDir, _projectRepo, _taskRepo);
        var result = await migration.MigrateAsync();

        result.ProjectsMigrated.Should().Be(1);
        result.Errors.Should().BeEmpty();

        var loaded = _projectRepo.GetById("p1");
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Project Alpha");
        loaded.Status.Should().Be(ProjectStatus.Analyzed);
    }

    [Fact]
    public async Task Migrate_ShouldTransferTasks()
    {
        var projects = new List<Project>
        {
            new()
            {
                Id = "p1",
                Name = "Project Beta",
                Status = ProjectStatus.InProgress,
                Paths = new ProjectPaths
                {
                    Specification = "./project-beta/specification.md",
                    Output = "./project-beta/workspace",
                    TaskFiles = new Dictionary<string, string>
                    {
                        ["backend"] = "./project-beta/tasks/backend-tasks.md"
                    }
                },
                Timestamps = new ProjectTimestamps
                {
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        };

        var tasks = new Dictionary<string, List<TaskRecord>>
        {
            ["project-beta/tasks/backend-tasks.json"] = new()
            {
                new TaskRecord
                {
                    Id = "t1",
                    Task = "Create API endpoint",
                    AssignedAgent = "csharp",
                    Status = Models.Tasks.TaskStatus.Done,
                    Priority = TaskPriority.High,
                    Dependencies = new List<string> { "t0" }
                },
                new TaskRecord
                {
                    Id = "t2",
                    Task = "Add auth middleware",
                    AssignedAgent = "csharp",
                    Status = Models.Tasks.TaskStatus.Working,
                    Priority = TaskPriority.Normal
                }
            }
        };

        SetupJsonFiles(projects, tasks);

        var migration = new JsonToSqlMigration(_tempDir, _projectRepo, _taskRepo);
        var result = await migration.MigrateAsync();

        result.ProjectsMigrated.Should().Be(1);
        result.TasksMigrated.Should().Be(2);
        result.Errors.Should().BeEmpty();

        var loadedTasks = await _taskRepo.GetByProjectAsync("p1");
        loadedTasks.Should().HaveCount(2);
        loadedTasks.Should().Contain(t => t.Id == "t1" && t.Status == Models.Tasks.TaskStatus.Done);
        loadedTasks.Should().Contain(t => t.Id == "t2" && t.Status == Models.Tasks.TaskStatus.Working);
    }

    [Fact]
    public async Task Migrate_DryRun_ShouldNotWrite()
    {
        var projects = new List<Project>
        {
            new()
            {
                Id = "p1",
                Name = "DryRun Project",
                Status = ProjectStatus.New,
                Paths = new ProjectPaths { TaskFiles = new() },
                Timestamps = new ProjectTimestamps { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            }
        };

        SetupJsonFiles(projects);

        var migration = new JsonToSqlMigration(_tempDir, _projectRepo, _taskRepo);
        var result = await migration.MigrateAsync(dryRun: true);

        result.ProjectsMigrated.Should().Be(1);
        _projectRepo.Count().Should().Be(0); // Nothing actually written
    }

    [Fact]
    public async Task Migrate_Idempotent_ShouldNotDuplicate()
    {
        var projects = new List<Project>
        {
            new()
            {
                Id = "p1",
                Name = "Idempotent",
                Status = ProjectStatus.New,
                Paths = new ProjectPaths { TaskFiles = new() },
                Timestamps = new ProjectTimestamps { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            }
        };

        SetupJsonFiles(projects);

        var migration = new JsonToSqlMigration(_tempDir, _projectRepo, _taskRepo);

        // Run twice
        await migration.MigrateAsync();
        await migration.MigrateAsync();

        _projectRepo.Count().Should().Be(1);
    }
}
