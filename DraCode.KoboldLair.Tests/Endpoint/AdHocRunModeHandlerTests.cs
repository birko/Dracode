using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Endpoint;

/// <summary>
/// Coverage for /kobold ad-hoc mode (TASK-039). The full run (a live LLM-backed Kobold editing the worktree)
/// needs a real provider and lives in the task's human test plan; here we pin the deterministic pieces:
/// the pure request/agent-type/path helpers, the auto git-init + initial-snapshot + worktree scaffolding
/// (real git, no LLM — satisfies "produces a worktree + commit"), and the stale-worktree cleanup policy.
/// </summary>
public class AdHocRunModeHandlerTests
{
    // ---- pure helpers ---------------------------------------------------------------------------

    [Fact]
    public void ValidateRequest_passes_with_cwd_and_prompt()
    {
        var act = () => AdHocRunModeHandler.ValidateRequest("/some/dir", "do the thing");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null, "p")]
    [InlineData("", "p")]
    [InlineData("  ", "p")]
    public void ValidateRequest_rejects_missing_cwd(string? cwd, string prompt)
    {
        var act = () => AdHocRunModeHandler.ValidateRequest(cwd, prompt);
        act.Should().Throw<ArgumentException>().WithMessage("*cwd*");
    }

    [Theory]
    [InlineData("/dir", null)]
    [InlineData("/dir", "")]
    [InlineData("/dir", "   ")]
    public void ValidateRequest_rejects_missing_prompt(string cwd, string? prompt)
    {
        var act = () => AdHocRunModeHandler.ValidateRequest(cwd, prompt);
        act.Should().Throw<ArgumentException>().WithMessage("*prompt*");
    }

    [Fact]
    public void BranchName_and_worktree_path_follow_the_documented_shape()
    {
        var runId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        AdHocRunModeHandler.BranchName(runId).Should().Be($"kobold/adhoc-{runId:N}");

        var wt = AdHocRunModeHandler.WorktreePath("/work", runId);
        Path.GetFileName(wt).Should().Be($"r-{runId:N}");
        wt.Should().Be(Path.Combine("/work", ".koboldlair", ".worktrees", $"r-{runId:N}"));
    }

    [Fact]
    public void ResolveAgentType_prefers_explicit_override_normalized()
    {
        AdHocRunModeHandler.ResolveAgentType("CSharp", cwd: "/nonexistent").Should().Be("csharp");
    }

    [Fact]
    public void ResolveAgentType_falls_back_to_coding_when_nothing_detectable()
    {
        using var dir = new TempDir();
        AdHocRunModeHandler.ResolveAgentType(null, dir.Path).Should().Be("coding");
    }

    [Fact]
    public void DetectAgentTypeFromExtensions_picks_the_dominant_language()
    {
        var exts = new[] { ".cs", ".cs", ".cs", ".md", ".json", ".ts" };
        AdHocRunModeHandler.DetectAgentTypeFromExtensions(exts).Should().Be("csharp");
    }

    [Fact]
    public void DetectAgentTypeFromExtensions_returns_null_with_no_known_code()
    {
        AdHocRunModeHandler.DetectAgentTypeFromExtensions(new[] { ".md", ".txt", ".json" }).Should().BeNull();
    }

    [Fact]
    public void DetectAgentType_reads_files_on_disk()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "a.py"), "print(1)");
        File.WriteAllText(Path.Combine(dir.Path, "b.py"), "print(2)");
        File.WriteAllText(Path.Combine(dir.Path, "readme.md"), "# hi");

        AdHocRunModeHandler.DetectAgentType(dir.Path).Should().Be("python");
    }

    // ---- git scaffolding (real git, no LLM) -----------------------------------------------------

    private static GitService Git() => new(NullLogger<GitService>.Instance);

    [Fact]
    public async Task EnsureRepository_inits_and_snapshots_existing_files_in_a_non_repo_dir()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "hello.cs"), "class C {}");
        var git = Git();

        await AdHocRunModeHandler.EnsureRepositoryAsync(git, dir.Path);

        Directory.Exists(Path.Combine(dir.Path, ".git")).Should().BeTrue("git init must run");
        (await git.GetLastCommitShaAsync(dir.Path)).Should().NotBeNullOrEmpty("an initial commit must exist");

        // The existing files (and the seeded .koboldlair/.gitignore) are committed → the working tree is clean,
        // so a re-stage + re-commit finds nothing to do. (diff-tree can't enumerate a root commit's files.)
        await git.StageAllAsync(dir.Path);
        (await git.CommitChangesAsync(dir.Path, "noop")).Should().Be(CommitResult.NoChanges);
        File.Exists(Path.Combine(dir.Path, ".koboldlair", ".gitignore")).Should().BeTrue();
    }

    [Fact]
    public async Task Adhoc_scaffolding_produces_a_worktree_and_a_commit()
    {
        // The deterministic prefix of StartAsync (everything up to spawning the Kobold): a non-repo cwd is
        // initialized + snapshotted, then a per-run branch + worktree are created under .koboldlair/.worktrees.
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "main.ts"), "export const x = 1;");
        var git = Git();
        var runId = Guid.NewGuid();

        await AdHocRunModeHandler.EnsureRepositoryAsync(git, dir.Path);
        var branch = AdHocRunModeHandler.BranchName(runId);
        var worktreePath = AdHocRunModeHandler.WorktreePath(dir.Path, runId);

        (await git.CreateBranchAsync(dir.Path, branch)).Should().BeTrue();
        var created = await git.CreateWorktreeAsync(dir.Path, branch, worktreePath);

        created.Should().NotBeNull();
        Directory.Exists(worktreePath).Should().BeTrue("the worktree directory must exist on disk");
        // The worktree shares the repo's history → the initial commit is reachable from it.
        (await git.GetLastCommitShaAsync(worktreePath)).Should().NotBeNullOrEmpty();
    }

    // ---- cleanup policy -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupStaleWorktrees_removes_old_runs_and_keeps_fresh_ones()
    {
        using var dir = new TempDir();
        var git = Git();
        await git.InitRepositoryAsync(dir.Path);

        var root = AdHocRunModeHandler.WorktreeRoot(dir.Path);
        Directory.CreateDirectory(root);
        var stale = Path.Combine(root, "r-stale");
        var fresh = Path.Combine(root, "r-fresh");
        var unrelated = Path.Combine(root, "feature-x"); // not an r-* ad-hoc worktree
        Directory.CreateDirectory(stale);
        Directory.CreateDirectory(fresh);
        Directory.CreateDirectory(unrelated);

        var now = DateTime.UtcNow;
        Directory.SetLastWriteTimeUtc(stale, now.AddDays(-30));
        Directory.SetLastWriteTimeUtc(fresh, now.AddHours(-1));
        Directory.SetLastWriteTimeUtc(unrelated, now.AddDays(-30));

        var removed = await AdHocRunModeHandler.CleanupStaleWorktreesAsync(dir.Path, git, TimeSpan.FromDays(7), now);

        removed.Should().Be(1);
        Directory.Exists(stale).Should().BeFalse("worktrees older than the retention window are pruned");
        Directory.Exists(fresh).Should().BeTrue("recent worktrees are kept");
        Directory.Exists(unrelated).Should().BeTrue("non ad-hoc (r-*) directories are left alone");
    }

    [Fact]
    public async Task CleanupStaleWorktrees_is_a_noop_when_no_worktrees_root_exists()
    {
        using var dir = new TempDir();
        var removed = await AdHocRunModeHandler.CleanupStaleWorktreesAsync(dir.Path, Git(), TimeSpan.FromDays(7), DateTime.UtcNow);
        removed.Should().Be(0);
    }

    /// <summary>A throwaway directory that deletes itself on dispose.</summary>
    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kobold-adhoc-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }
}
