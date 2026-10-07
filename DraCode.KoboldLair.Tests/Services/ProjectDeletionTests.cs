using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Deleting a project removes its folder even with a git repo inside (read-only object files), removes its task and plan
/// rows, never touches an imported project's source folder, and says when something could not be removed (TASK-103 / FIELD-017).
/// It also releases the project's Wyvern and Drakes, so a new project with the same name never inherits them (TASK-093 / FIELD-010).
/// </summary>
public class ProjectDeletionTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlProjectRepository _repo = null!;
    private SqlTaskRepository _tasks = null!;
    private SqlPlanRepository _plans = null!;
    private WyvernFactory _wyverns = null!;
    private DrakeFactory _drakes = null!;
    private ProjectService _service = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_delete_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");

        _repo = new SqlProjectRepository(db);
        await _repo.InitializeAsync();
        _tasks = new SqlTaskRepository(db);
        await _tasks.InitializeAsync();
        _plans = new SqlPlanRepository(db);
        await _plans.InitializeAsync();

        // A provider must exist to build a Wyvern or a Drake; nothing here sends a request to it
        var config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir,
            DefaultProvider = "ollama",
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = "ollama", Type = "ollama", DefaultModel = "m1",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" },
                    Configuration = new()
                }
            }
        };
        var providerConfig = new ProviderConfigurationService(
            NullLogger<ProviderConfigurationService>.Instance, Options.Create(config), Path.Combine(_dir, "user-settings.json"));
        _wyverns = new WyvernFactory(providerConfig, _repo, config);
        _drakes = new DrakeFactory(new KoboldFactory(_repo, NullLoggerFactory.Instance, config), providerConfig, config,
            NullLoggerFactory.Instance, projectRepository: _repo);
        _service = new ProjectService(_repo, _wyverns,
            NullLogger<ProjectService>.Instance, new GitService(NullLogger<GitService>.Instance), config,
            drakeFactory: _drakes, taskRepository: _tasks, planRepository: _plans);
    }

    public Task DisposeAsync()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(_dir, recursive: true);
        }
        catch { }
        return Task.CompletedTask;
    }

    private async Task<(Project Project, string Folder)> ProjectWithGitObjectAsync(string name)
    {
        var folder = Path.Combine(_dir, name);
        var objectFile = Path.Combine(folder, ".git", "objects", "05", "503e9fe3ef40dc2514d1bc4014f9004e3f1a");
        Directory.CreateDirectory(Path.GetDirectoryName(objectFile)!);
        await File.WriteAllTextAsync(objectFile, "blob");
        File.SetAttributes(objectFile, FileAttributes.ReadOnly); // as git leaves its object files on Windows
        await File.WriteAllTextAsync(Path.Combine(folder, "specification.md"), "# p");

        var project = new Project
        {
            Name = name,
            OwnerId = Guid.NewGuid().ToString(),
            Paths = { Specification = Path.Combine(folder, "specification.md") }
        };
        await _repo.AddAsync(project);
        return (project, folder);
    }

    [Fact]
    public async Task Deleting_with_files_removes_a_folder_holding_a_read_only_git_object()
    {
        var (project, folder) = await ProjectWithGitObjectAsync("gitproj");

        var result = await _service.DeleteProjectAsync(project.Id, deleteFiles: true);

        result.Deleted.Should().BeTrue();
        Directory.Exists(folder).Should().BeFalse(result.Message);
        _repo.GetById(project.Id).Should().BeNull();
    }

    [Fact]
    public async Task Deleting_removes_the_projects_task_and_plan_rows_only()
    {
        var (project, _) = await ProjectWithGitObjectAsync("rows");
        var (other, _) = await ProjectWithGitObjectAsync("other");
        await _tasks.AddTaskAsync(project.Id, "cli", new TaskRecord { Task = "[cli-1] a" });
        await _tasks.AddTaskAsync(project.Id, "doc", new TaskRecord { Task = "[doc-1] b" });
        await _tasks.AddTaskAsync(other.Id, "cli", new TaskRecord { Task = "[cli-1] keep" });
        await _plans.SavePlanAsync(new KoboldImplementationPlan { ProjectId = project.Id, TaskId = "t1" });

        await _service.DeleteProjectAsync(project.Id, deleteFiles: false);

        (await _tasks.GetByProjectAsync(project.Id)).Should().BeEmpty();
        (await _plans.GetPlansForProjectAsync(project.Id)).Should().BeEmpty();
        (await _tasks.GetByProjectAsync(other.Id)).Should().HaveCount(1);
    }

    [Fact]
    public async Task An_imported_projects_source_folder_outside_the_projects_path_is_never_deleted()
    {
        var source = Path.Combine(Path.GetTempPath(), $"dracode_delete_test_source_{Guid.NewGuid():N}");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "specification.md"), "# imported");
        try
        {
            var project = new Project
            {
                Name = "imported",
                OwnerId = Guid.NewGuid().ToString(),
                Paths = { Specification = Path.Combine(source, "specification.md") },
                Metadata = { ["IsExistingProject"] = "true", ["SourcePath"] = source }
            };
            await _repo.AddAsync(project);

            var result = await _service.DeleteProjectAsync(project.Id, deleteFiles: true);

            Directory.Exists(source).Should().BeTrue();
            result.Message.Should().Contain("nothing deleted on disk");
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [Fact]
    public async Task Deleting_releases_the_projects_Wyvern_and_Drakes()
    {
        var (project, folder) = await ProjectWithGitObjectAsync("agents");
        _wyverns.CreateWyvern(project.Name, project.Paths.Specification, folder, projectId: project.Id);
        _drakes.CreateDrake(Path.Combine(folder, "tasks", "cli-tasks.md"), "agents-cli", projectId: project.Id);

        await _service.DeleteProjectAsync(project.Id, deleteFiles: false);

        _wyverns.GetWyvern(project.Name).Should().BeNull();
        _drakes.GetActiveDrakeCountForProject(project.Id).Should().Be(0);
    }

    [Fact]
    public async Task Deleting_leaves_another_projects_Wyvern_whose_name_sanitizes_the_same()
    {
        var other = _wyverns.CreateWyvern("Same Name", Path.Combine(_dir, "x", "specification.md"), Path.Combine(_dir, "x"),
            projectId: Guid.NewGuid().ToString());
        var (project, _) = await ProjectWithGitObjectAsync("same-name");

        await _service.DeleteProjectAsync(project.Id, deleteFiles: false);

        _wyverns.GetWyvern("Same Name").Should().BeSameAs(other);
    }

    [Fact]
    public async Task A_new_project_with_a_deleted_projects_name_gets_its_own_Wyvern()
    {
        var (old, _) = await ProjectWithGitObjectAsync("reused");
        await _service.AssignWyvernAsync(old.Id);
        await _service.DeleteProjectAsync(old.Id, deleteFiles: true);

        var (fresh, _) = await ProjectWithGitObjectAsync("reused");
        var wyvern = await _service.AssignWyvernAsync(fresh.Id);

        wyvern.ProjectId.Should().Be(fresh.Id);
    }

    [Fact]
    public async Task A_Wyvern_left_by_another_project_with_the_same_name_is_replaced()
    {
        // The project row went some other way (not through DeleteProjectAsync), so its Wyvern is still registered
        _wyverns.CreateWyvern("stale", Path.Combine(_dir, "stale", "specification.md"), Path.Combine(_dir, "stale"),
            projectId: Guid.NewGuid().ToString());
        var (project, _) = await ProjectWithGitObjectAsync("stale");
        project.Status = ProjectStatus.WyrmAssigned;
        _repo.Update(project);

        var wyvern = await _service.AssignWyvernAsync(project.Id);

        wyvern.ProjectId.Should().Be(project.Id);
    }
}
