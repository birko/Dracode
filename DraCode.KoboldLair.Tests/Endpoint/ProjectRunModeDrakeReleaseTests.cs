using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Endpoint;

/// <summary>
/// /kobold project mode must release the Drake it registers, or the project's background Drake stays locked out by the
/// per-project Drake limit (TASK-100 / FIELD-015).
/// </summary>
public class ProjectRunModeDrakeReleaseTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlProjectRepository _repo = null!;
    private DrakeFactory _drakes = null!;
    private ProjectRunModeHandler _handler = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_projectmode_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        _repo = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await _repo.InitializeAsync();

        // A provider must exist to build a Drake; nothing here sends a request to it
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
        var kobolds = new KoboldFactory(_repo, NullLoggerFactory.Instance, config);
        _drakes = new DrakeFactory(kobolds, providerConfig, config, NullLoggerFactory.Instance, projectRepository: _repo);
        _handler = new ProjectRunModeHandler(_repo, _drakes, new KoboldRunEventSource(), NullLogger<ProjectRunModeHandler>.Instance);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_start_that_fails_on_a_task_that_is_not_ready_releases_its_Drake()
    {
        var specPath = Path.Combine(_dir, "p", "specification.md");
        Directory.CreateDirectory(Path.GetDirectoryName(specPath)!);
        await File.WriteAllTextAsync(specPath, "# p");

        var taskFile = Path.Combine(_dir, "p", "tasks", "cli-tasks.md");
        Directory.CreateDirectory(Path.GetDirectoryName(taskFile)!);
        var tracker = new TaskTracker();
        var done = tracker.AddTask("[cli-1] already finished");
        tracker.UpdateTask(done, TaskStatus.Done);
        tracker.SaveToFile(taskFile);

        var project = new Project
        {
            Name = "p",
            OwnerId = Guid.NewGuid().ToString(),
            Status = ProjectStatus.InProgress,
            ExecutionState = ProjectExecutionState.Running,
            Paths = { Specification = specPath, TaskFiles = { ["cli"] = taskFile } }
        };
        await _repo.AddAsync(project);

        var request = new KoboldRunRequest { Mode = "project", ProjectId = project.Id, TaskId = done.Id };
        var start = () => _handler.StartAsync(request, Guid.NewGuid(), new KoboldCaller(null, true, null, null), CancellationToken.None);

        await start.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not an unassigned, ready task*");
        _drakes.GetActiveDrakeCountForProject(project.Id).Should().Be(0);
    }
}
