using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// TASK-178: a failed verification's fix tasks reach a Drake and run, a failing re-run adds no second fix for a check
/// whose fix is still open, and a Drake that loads nothing from a file holding tasks does not save over it.
/// </summary>
public class VerificationFixTasksTests : IAsyncLifetime
{
    private string _dir = null!;
    private string _fixFile = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private const string ProjectId = "proj-verify";

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_verifyfix_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _fixFile = Path.Combine(_dir, "tasks", VerificationFixTasks.FileName);
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();

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
        _kobolds = new KoboldFactory(projects, NullLoggerFactory.Instance, config);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private Drake NewDrake(string taskFile) =>
        new(_kobolds, new TaskTracker(), taskFile, "ollama", projectId: ProjectId, pendingDecisions: _decisions);

    private static VerificationCheck Failed(string type, VerificationCheckPriority priority = VerificationCheckPriority.Critical) => new()
    {
        CheckType = type,
        Command = $"run-{type}",
        ExitCode = 1,
        Output = $"{type} failed:\n  waver.js line 3",
        Passed = false,
        Priority = priority
    };

    [Fact]
    public async Task A_fix_task_written_for_a_failed_check_is_loaded_by_Drake_and_summoned()
    {
        var added = await VerificationFixTasks.AddAsync(_fixFile, new[] { Failed("syntax"), Failed("lint", VerificationCheckPriority.Medium) });
        added.Should().HaveCount(2);

        var drake = NewDrake(_fixFile);
        (await drake.ReloadTasksFromFileAsync()).Should().Be(2);

        var tasks = drake.GetAllTasks();
        tasks.Should().OnlyContain(t => t.Status == TaskStatus.Unassigned);
        tasks.Select(t => t.Id).Should().BeEquivalentTo(added.Select(t => t.Id), "ids survive the file round trip");
        tasks.Single(t => t.Task.StartsWith("Fix syntax failure:")).Priority.Should().Be(TaskPriority.Critical);
        tasks.Single(t => t.Task.StartsWith("Fix lint failure:")).Task.Should().Contain("run-lint").And.Contain("waver.js line 3");

        var kobold = await drake.SummonKoboldAsync(tasks[0], "javascript");
        kobold.Should().NotBeNull();
        drake.GetAllTasks().Single(t => t.Id == tasks[0].Id).Status.Should().NotBe(TaskStatus.Unassigned);
    }

    [Fact]
    public async Task A_failing_rerun_adds_no_second_fix_for_a_check_whose_fix_is_still_open()
    {
        await VerificationFixTasks.AddAsync(_fixFile, new[] { Failed("syntax") });

        // The Drake's own cycle: reload, then save what it holds (the loop TASK-178 found rewrote the file empty here)
        var drake = NewDrake(_fixFile);
        await drake.ReloadTasksFromFileAsync();
        await drake.SaveTasksToFileAsync();

        var again = await VerificationFixTasks.AddAsync(_fixFile, new[] { Failed("syntax"), Failed("test", VerificationCheckPriority.High) });

        again.Select(t => t.Task).Should().ContainSingle().Which.Should().StartWith("Fix test failure:");
        var tracker = new TaskTracker();
        (await tracker.LoadFromFileAsync(_fixFile)).Should().Be(2);
        tracker.GetAllTasks().Count(t => t.Task.StartsWith("Fix syntax failure:")).Should().Be(1);
    }

    [Fact]
    public async Task A_check_whose_fix_is_done_gets_a_new_fix_when_it_fails_again()
    {
        var first = (await VerificationFixTasks.AddAsync(_fixFile, new[] { Failed("syntax") })).Single();
        var tracker = new TaskTracker();
        await tracker.LoadFromFileAsync(_fixFile);
        tracker.UpdateTask(tracker.GetTaskById(first.Id)!, TaskStatus.Done);
        await tracker.SaveToFileAsync(_fixFile);

        var again = await VerificationFixTasks.AddAsync(_fixFile, new[] { Failed("syntax") });

        again.Should().ContainSingle().Which.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task A_Drake_that_loads_nothing_from_a_markdown_holding_tasks_does_not_save_over_it()
    {
        // The table shape the verification service wrote before TASK-178: a header the markdown loader does not know
        var file = Path.Combine(_dir, "tasks", "legacy-tasks.md");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var legacy = "# Verification Fixes\n\n| ID | Task | Agent | Status |\n|----|------|-------|--------|\n" +
                     "| verify-syntax-1a2b3c4d | Fix syntax failure: node --check waver.js | unassigned | unassigned |\n";
        await File.WriteAllTextAsync(file, legacy);

        var drake = NewDrake(file);
        (await drake.ReloadTasksFromFileAsync()).Should().Be(0);
        await drake.SaveTasksToFileAsync();
        await drake.FlushAndCloseAsync();

        (await File.ReadAllTextAsync(file)).Should().Be(legacy);
        File.Exists(Path.ChangeExtension(file, ".json")).Should().BeFalse();
    }

    [Fact]
    public async Task A_Drake_that_cannot_read_a_JSON_holding_tasks_does_not_save_over_it()
    {
        var file = Path.Combine(_dir, "tasks", "module-tasks.md");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var json = Path.ChangeExtension(file, ".json");
        const string unreadable = "[{\"id\": \"a1\", \"task\": \"Write waver.js\", \"status\": ";
        await File.WriteAllTextAsync(json, unreadable);

        var drake = NewDrake(file);
        (await drake.ReloadTasksFromFileAsync()).Should().Be(0);
        await drake.SaveTasksToFileAsync();

        (await File.ReadAllTextAsync(json)).Should().Be(unreadable);
    }

    [Fact]
    public async Task A_Drake_with_tasks_still_saves_and_an_empty_file_may_be_rewritten()
    {
        var file = Path.Combine(_dir, "tasks", "empty-tasks.md");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(Path.ChangeExtension(file, ".json"), "[]");

        var drake = NewDrake(file);
        await drake.ReloadTasksFromFileAsync();
        await drake.SaveTasksToFileAsync();
        File.Exists(file).Should().BeTrue("an empty list over an empty file loses nothing");

        await VerificationFixTasks.AddAsync(file, new[] { Failed("build") });
        await drake.ReloadTasksFromFileAsync();
        await drake.SaveTasksToFileAsync();

        var tracker = new TaskTracker();
        (await tracker.LoadFromFileAsync(file)).Should().Be(1);
    }
}
