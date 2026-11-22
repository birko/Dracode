using System.Diagnostics;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// A feature worktree outlives a run that did not commit (TASK-168): before, every run's end force-removed it, so a run
/// cancelled by a shutdown, or a failed one, lost the files it wrote, and the resumed run started from nothing.
/// </summary>
public class DrakeWorktreeTests : IAsyncLifetime
{
    private string _dir = null!;
    private string _repo = null!;
    private GitService _git = null!;
    private KoboldFactory _kobolds = null!;
    private SqlProjectRepository _projects = null!;
    private const string Branch = "feature/f1-store";

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_wt_{Guid.NewGuid():N}");
        _repo = Path.Combine(_dir, "proj");
        Directory.CreateDirectory(Path.Combine(_repo, "tasks"));
        _git = new GitService(NullLogger<GitService>.Instance);
        (await _git.InitRepositoryAsync(_repo)).Should().BeTrue("the tests need git");
        await File.WriteAllTextAsync(Path.Combine(_repo, "README.md"), "# proj");
        Git(_repo, "add", "-A");
        Git(_repo, "commit", "-m", "init");
        (await _git.CreateBranchAsync(_repo, Branch)).Should().BeTrue();

        _projects = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await _projects.InitializeAsync();
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
        _kobolds = new KoboldFactory(_projects, NullLoggerFactory.Instance, config);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            foreach (var f in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(_dir, recursive: true);
        }
        catch { }
        return Task.CompletedTask;
    }

    private static void Git(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        p.ExitCode.Should().Be(0, $"git {string.Join(' ', args)}: {p.StandardError.ReadToEnd()}");
    }

    private (Drake drake, TaskTracker tracker) NewDrake()
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_repo, "tasks", "backend-tasks.md"), "ollama",
            projectId: "proj-wt", gitService: _git);
        return (drake, tracker);
    }

    private async Task<string> Worktree() => (await _git.CreateWorktreeAsync(_repo, Branch))!;

    [Fact]
    public async Task A_run_that_ended_without_committing_keeps_its_worktree_and_files()
    {
        // The shutdown case: the run was cancelled mid-task, the Kobold left Working, nothing committed
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Build the store");
        task.Status = TaskStatus.Working;
        var worktree = await Worktree();
        drake.AcquireWorktree(worktree);
        var written = Path.Combine(worktree, "workspace", "store.js");
        Directory.CreateDirectory(Path.GetDirectoryName(written)!);
        await File.WriteAllTextAsync(written, "module.exports = {};");

        var kept = await drake.ReleaseWorktreeAsync(task, worktree);

        kept.Should().NotBeNull();
        File.Exists(written).Should().BeTrue("the resumed run continues on what was written");
        // The feature's next run reuses it, files and all
        (await Worktree()).Should().Be(worktree);
        File.Exists(written).Should().BeTrue();
    }

    [Fact]
    public async Task A_failed_run_keeps_what_it_wrote()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Build the store");
        task.Status = TaskStatus.Failed;
        var worktree = await Worktree();
        drake.AcquireWorktree(worktree);
        await File.WriteAllTextAsync(Path.Combine(worktree, "half.js"), "// half done");

        (await drake.ReleaseWorktreeAsync(task, worktree)).Should().NotBeNull();
        File.Exists(Path.Combine(worktree, "half.js")).Should().BeTrue();
    }

    [Fact]
    public async Task A_committed_run_that_leaves_the_worktree_clean_removes_it()
    {
        var (drake, tracker) = NewDrake();
        var task = tracker.AddTask("[api-1] Build the store");
        var worktree = await Worktree();
        drake.AcquireWorktree(worktree);
        await File.WriteAllTextAsync(Path.Combine(worktree, "store.js"), "module.exports = {};");
        Git(worktree, "add", "-A");
        Git(worktree, "commit", "-m", "task done");
        task.Status = TaskStatus.Done;

        (await drake.ReleaseWorktreeAsync(task, worktree)).Should().BeNull();
        Directory.Exists(worktree).Should().BeFalse("nothing is left in it that the branch does not have");
    }

    [Fact]
    public async Task A_worktree_another_run_of_the_feature_still_uses_is_kept_even_when_clean()
    {
        var (drake, tracker) = NewDrake();
        var done = tracker.AddTask("[api-1] Build the store");
        done.Status = TaskStatus.Done;
        var worktree = await Worktree();
        drake.AcquireWorktree(worktree); // the finished run
        drake.AcquireWorktree(worktree); // a run of the same feature, still working

        (await drake.ReleaseWorktreeAsync(done, worktree)).Should().Contain("another task");
        Directory.Exists(worktree).Should().BeTrue();

        // When that run is done too and the tree is clean, it goes
        (await drake.ReleaseWorktreeAsync(done, worktree)).Should().BeNull();
        Directory.Exists(worktree).Should().BeFalse();
    }
}
