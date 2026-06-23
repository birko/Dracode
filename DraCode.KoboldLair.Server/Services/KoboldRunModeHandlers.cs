using System.Collections.Concurrent;
using Birko.AI;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Ad-hoc run mode (TASK-039) — run a single Kobold against the caller's own working directory, with no
    /// project record. The directory is validated, <c>git init</c>'d (+ an initial snapshot) if it is not yet
    /// a repository, and a fresh worktree is created under <c>&lt;cwd&gt;/.koboldlair/.worktrees/r-&lt;runId&gt;/</c>.
    /// A Kobold is spawned there via <see cref="KoboldFactory"/> (agent type from the payload or auto-detected
    /// from the cwd's files), adopts the endpoint-owned <paramref name="runId"/> so the WebSocket transport
    /// already subscribed receives its telemetry, and on completion its edits are committed to the ad-hoc
    /// branch so the caller can later <c>git merge</c> them into the cwd. Runs are tracked only in memory,
    /// keyed by <c>runId</c> (off-registry); stale worktrees are pruned per-cwd before each new run.
    /// </summary>
    public sealed class AdHocRunModeHandler : IKoboldRunModeHandler
    {
        /// <summary>Ad-hoc worktrees older than this are pruned at the start of each run for the same cwd.</summary>
        public static readonly TimeSpan WorktreeRetention = TimeSpan.FromDays(7);

        private readonly KoboldFactory _koboldFactory;
        private readonly GitService _git;
        private readonly ProviderConfigurationService _providers;
        private readonly KoboldRunEventSource _eventSource;
        private readonly ILogger<AdHocRunModeHandler> _logger;

        /// <summary>Off-registry run tracking (criterion: "tracked off-registry by runId").</summary>
        private readonly ConcurrentDictionary<Guid, AdHocRun> _runs = new();

        public AdHocRunModeHandler(
            KoboldFactory koboldFactory,
            GitService git,
            ProviderConfigurationService providers,
            KoboldRunEventSource eventSource,
            ILogger<AdHocRunModeHandler> logger)
        {
            _koboldFactory = koboldFactory;
            _git = git;
            _providers = providers;
            _eventSource = eventSource;
            _logger = logger;
        }

        public string Mode => "adhoc";

        /// <summary>Off-registry tracking is bounded so a long-lived server doesn't accumulate run records.</summary>
        private const int MaxAdhocRuns = 1000;

        /// <summary>An in-flight or finished ad-hoc run (kept only in memory). <c>running</c> until terminal.</summary>
        public sealed class AdHocRun
        {
            public required Guid RunId { get; init; }
            public required string Cwd { get; init; }
            public required string Branch { get; init; }
            public required string Worktree { get; init; }
            public required DateTime StartedAt { get; init; }
            /// <summary>Resolved once detection runs in the background; "(detecting)" until then.</summary>
            public string AgentType { get; set; } = "(detecting)";
            /// <summary>"running" while live; otherwise the Kobold's terminal status ("Done"/"Failed").</summary>
            public string Status { get; set; } = "running";
            public bool IsActive => Status == "running";
        }

        /// <summary>Snapshot of a tracked ad-hoc run, or null if unknown.</summary>
        public AdHocRun? GetRun(Guid runId) => _runs.TryGetValue(runId, out var r) ? r : null;

        public async Task<KoboldRunStartInfo> StartAsync(KoboldRunRequest request, Guid runId, KoboldCaller caller, CancellationToken ct)
        {
            var cwd = request.Cwd ?? request.WorkingDirectory;
            var prompt = request.Prompt ?? request.Task;
            ValidateRequest(cwd, prompt);

            // Fast, synchronous pre-flight only (so the caller gets an immediate 400 on obviously-bad input).
            var fullCwd = Path.GetFullPath(cwd!);
            if (!Directory.Exists(fullCwd))
                throw new InvalidOperationException($"cwd '{cwd}' does not exist");
            if (!await _git.IsGitInstalledAsync())
                throw new InvalidOperationException("git is not installed; /kobold ad-hoc mode requires git");

            // The worktree path + branch are deterministic from runId, so we report them immediately while the
            // heavy work (stale-prune, git init/snapshot, recursive agent-type detection, worktree creation,
            // and the Kobold run) happens on a background task — POST/WS must return without blocking (criterion:
            // "returns a runId immediately"). Setup failures surface as a terminal RunErrorEvent, not a throw.
            var branch = BranchName(runId);
            var worktreePath = WorktreePath(fullCwd, runId);
            var explicitAgentType = string.IsNullOrWhiteSpace(request.AgentType)
                ? null : request.AgentType!.Trim().ToLowerInvariant();

            _runs[runId] = new AdHocRun
            {
                RunId = runId, Cwd = fullCwd, Branch = branch, Worktree = worktreePath,
                StartedAt = DateTime.UtcNow, AgentType = explicitAgentType ?? "(detecting)"
            };
            PruneAdhocRuns();

            _ = Task.Run(() => RunAdhocAsync(runId, fullCwd, branch, worktreePath, prompt!, explicitAgentType),
                CancellationToken.None);

            return new KoboldRunStartInfo("adhoc", worktreePath);
        }

        /// <summary>
        /// The background body of an ad-hoc run: git scaffolding → Kobold execution → commit. On any failure it
        /// removes the worktree and deletes the (commit-less) ad-hoc branch so nothing is orphaned, then emits a
        /// terminal <see cref="RunErrorEvent"/> + <c>CompleteRun</c> so every subscriber (pump, registry) finishes.
        /// </summary>
        private async Task RunAdhocAsync(Guid runId, string fullCwd, string branch, string worktreePath, string prompt, string? explicitAgentType)
        {
            string? createdPath = null;
            var branchCreated = false;
            try
            {
                // Prune this cwd's stale ad-hoc worktrees, but never one a concurrent run is still using.
                await CleanupStaleWorktreesAsync(fullCwd, _git, WorktreeRetention, DateTime.UtcNow, ActiveWorktrees());

                // Validate/initialize the repo: auto git-init + initial snapshot when cwd is not yet a repository.
                await EnsureRepositoryAsync(_git, fullCwd);

                var agentType = explicitAgentType ?? DetectAgentType(fullCwd) ?? "coding";
                if (_runs.TryGetValue(runId, out var r)) r.AgentType = agentType;

                // The ad-hoc branch forks from the cwd's CURRENT HEAD (wherever the caller is), not a fixed
                // 'main' — intentional, so the caller merges the result back into the same context they ran from.
                if (!await _git.CreateBranchAsync(fullCwd, branch))
                    throw new InvalidOperationException($"failed to create ad-hoc branch '{branch}'");
                branchCreated = true;
                createdPath = await _git.CreateWorktreeAsync(fullCwd, branch, worktreePath)
                    ?? throw new InvalidOperationException($"failed to create worktree at '{worktreePath}'");

                // Spawn the Kobold in the worktree (its WorkingDirectory is the sandbox root for file ops).
                var provider = _providers.GetProviderForKoboldAgentType(agentType);
                var options = new AgentOptions { WorkingDirectory = createdPath };
                var kobold = _koboldFactory.CreateKobold(provider, agentType, options);
                kobold.AssignRunId(runId); // already subscribed transports see telemetry under this id
                kobold.AssignTask(Guid.NewGuid(), prompt, projectId: null);

                _logger.LogInformation(
                    "/kobold ad-hoc run {RunId} starting: cwd={Cwd} agent={AgentType} branch={Branch}",
                    runId, fullCwd, agentType, branch);

                await kobold.StartWorkingWithPlanAsync(planService: null, cancellationToken: CancellationToken.None);

                await _git.StageAllAsync(createdPath);
                var commit = await _git.CommitChangesAsync(
                    createdPath, CommitMessage(prompt, runId), authorName: $"Kobold-{agentType}");
                _logger.LogInformation(
                    "/kobold ad-hoc run {RunId} finished ({Status}); worktree commit: {Commit}",
                    runId, kobold.Status, commit);

                if (_runs.TryGetValue(runId, out var rec)) rec.Status = kobold.Status.ToString();
                // The Kobold emitted its own terminal event + CompleteRun in its finally — nothing more to do.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "/kobold ad-hoc run {RunId} failed", runId);
                if (_runs.TryGetValue(runId, out var rec)) rec.Status = "Failed";

                // Don't orphan the worktree/branch — nothing else reclaims the branch, and a failed run committed
                // nothing, so the branch is safe to force-delete.
                if (createdPath != null)
                    try { await _git.RemoveWorktreeAsync(fullCwd, createdPath); }
                    catch (Exception cleanup) { _logger.LogWarning(cleanup, "ad-hoc run {RunId}: worktree cleanup failed", runId); }
                if (branchCreated)
                    try { await _git.DeleteBranchAsync(fullCwd, branch, force: true); }
                    catch (Exception cleanup) { _logger.LogWarning(cleanup, "ad-hoc run {RunId}: branch cleanup failed", runId); }

                // Terminal error + complete the run so the pump and the registry reader both finish. If the
                // Kobold had already started it emitted its own terminal event + CompleteRun, so these are no-ops.
                _eventSource.Publish(new RunErrorEvent { RunId = runId, Message = ex.Message });
                _eventSource.CompleteRun(runId);
            }
        }

        /// <summary>Normalized worktree paths of runs still live — never prune one of these.</summary>
        private IReadOnlySet<string> ActiveWorktrees() =>
            _runs.Values.Where(r => r.IsActive)
                .Select(r => NormalizePath(r.Worktree))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>Bounds memory: when over capacity, drop the oldest terminal (non-running) runs.</summary>
        private void PruneAdhocRuns()
        {
            if (_runs.Count <= MaxAdhocRuns) return;
            foreach (var stale in _runs.Values.Where(r => !r.IsActive)
                         .OrderBy(r => r.StartedAt).Take(_runs.Count - MaxAdhocRuns))
            {
                _runs.TryRemove(stale.RunId, out _);
            }
        }

        private static string NormalizePath(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        /// <summary>Validates the cwd + prompt are present. Pure (no IO) — unit-testable.</summary>
        public static void ValidateRequest(string? cwd, string? prompt)
        {
            if (string.IsNullOrWhiteSpace(cwd))
                throw new ArgumentException("ad-hoc mode requires 'cwd' (working directory)");
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("ad-hoc mode requires 'prompt' (the task to run)");
        }

        /// <summary>The ad-hoc feature branch for a run.</summary>
        public static string BranchName(Guid runId) => $"kobold/adhoc-{runId:N}";

        /// <summary>The <c>.koboldlair/.worktrees</c> root under a cwd.</summary>
        public static string WorktreeRoot(string cwd) => Path.Combine(cwd, ".koboldlair", ".worktrees");

        /// <summary>The per-run worktree path: <c>&lt;cwd&gt;/.koboldlair/.worktrees/r-&lt;runId&gt;/</c>.</summary>
        public static string WorktreePath(string cwd, Guid runId) => Path.Combine(WorktreeRoot(cwd), $"r-{runId:N}");

        private static string CommitMessage(string prompt, Guid runId)
        {
            var firstLine = prompt.Split('\n', 2)[0].Trim();
            if (firstLine.Length > 72) firstLine = string.Concat(firstLine.AsSpan(0, 69), "...");
            return $"feat: {firstLine}\n\nKoboldLair ad-hoc run r-{runId:N}";
        }

        /// <summary>
        /// Ensures <paramref name="cwd"/> is a git repository with at least one commit. When it is not a repo,
        /// runs <c>git init</c>, seeds <c>.koboldlair/.gitignore</c> (so worktrees stay untracked in the parent
        /// tree and the snapshot always has content), and commits the existing files.
        /// </summary>
        public static async Task EnsureRepositoryAsync(GitService git, string cwd)
        {
            if (!await git.IsRepositoryAsync(cwd))
            {
                if (!await git.InitRepositoryAsync(cwd))
                    throw new InvalidOperationException($"failed to initialize a git repository at '{cwd}'");

                var koboldDir = Path.Combine(cwd, ".koboldlair");
                Directory.CreateDirectory(koboldDir);
                var gitignore = Path.Combine(koboldDir, ".gitignore");
                if (!File.Exists(gitignore))
                    await File.WriteAllTextAsync(gitignore, ".worktrees/\n");

                await git.StageAllAsync(cwd);
                await git.CommitChangesAsync(cwd, "chore: koboldlair initial snapshot", authorName: "KoboldLair");
            }

            if (string.IsNullOrEmpty(await git.GetLastCommitShaAsync(cwd)))
                throw new InvalidOperationException(
                    $"cwd '{cwd}' is a git repository with no commits; create an initial commit before running ad-hoc mode");
        }

        /// <summary>Agent type: explicit payload override → auto-detect from cwd files → general <c>coding</c>.</summary>
        public static string ResolveAgentType(string? requested, string cwd)
        {
            if (!string.IsNullOrWhiteSpace(requested)) return requested.Trim().ToLowerInvariant();
            return DetectAgentType(cwd) ?? "coding";
        }

        /// <summary>Auto-detects the dominant agent type from the files in <paramref name="cwd"/>, or null.</summary>
        public static string? DetectAgentType(string cwd)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(cwd, "*", SearchOption.AllDirectories)
                    .Where(f => !IsIgnoredPath(f));
            }
            catch
            {
                return null;
            }
            return DetectAgentTypeFromExtensions(files.Select(Path.GetExtension));
        }

        /// <summary>Pure extension → agent-type vote tally (the most common mapped extension wins). Testable.</summary>
        public static string? DetectAgentTypeFromExtensions(IEnumerable<string?> extensions)
        {
            var votes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var ext in extensions)
            {
                var agent = MapExtension(ext);
                if (agent is null) continue;
                votes[agent] = votes.GetValueOrDefault(agent) + 1;
            }
            return votes.Count == 0 ? null : votes.OrderByDescending(kv => kv.Value).First().Key;
        }

        private static string? MapExtension(string? ext) => (ext ?? "").ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".ts" or ".tsx" => "typescript",
            ".js" or ".jsx" or ".mjs" or ".cjs" => "javascript",
            ".py" => "python",
            ".php" => "php",
            ".cpp" or ".cc" or ".cxx" or ".hpp" or ".hh" or ".c" or ".h" => "cpp",
            ".asm" or ".s" => "assembler",
            ".css" or ".scss" => "css",
            ".html" or ".htm" => "html",
            _ => null
        };

        private static bool IsIgnoredPath(string path)
        {
            var p = path.Replace('\\', '/');
            return p.Contains("/.git/") || p.Contains("/.koboldlair/") || p.Contains("/node_modules/")
                || p.Contains("/bin/") || p.Contains("/obj/") || p.Contains("/dist/");
        }

        /// <summary>
        /// Removes ad-hoc worktrees under <c>&lt;cwd&gt;/.koboldlair/.worktrees</c> last modified more than
        /// <paramref name="maxAge"/> ago, skipping any in <paramref name="inUseWorktrees"/> (a live run's worktree
        /// must never be force-removed mid-execution). Idempotent; safe to call before every run. Pure-ish
        /// (filesystem + the injected <paramref name="git"/>) so tests can drive it with a controllable <paramref name="nowUtc"/>.
        /// </summary>
        public static async Task<int> CleanupStaleWorktreesAsync(
            string cwd, GitService git, TimeSpan maxAge, DateTime nowUtc, IReadOnlySet<string>? inUseWorktrees = null)
        {
            var root = WorktreeRoot(cwd);
            if (!Directory.Exists(root)) return 0;

            int removed = 0;
            foreach (var dir in Directory.GetDirectories(root))
            {
                if (!Path.GetFileName(dir).StartsWith("r-", StringComparison.Ordinal)) continue;
                if (inUseWorktrees != null && inUseWorktrees.Contains(NormalizePath(dir))) continue;
                if (nowUtc - Directory.GetLastWriteTimeUtc(dir) <= maxAge) continue;
                if (await git.RemoveWorktreeAsync(cwd, dir)) removed++;
            }
            return removed;
        }
    }

    /// <summary>
    /// Project run mode (TASK-040) — run a single Kobold against an already-analyzed project task, reusing
    /// Drake's existing per-task flow (<see cref="Orchestrators.Drake.ExecuteTaskAsync"/>: worktree setup →
    /// summon → plan → execute → commit to the feature branch → cleanup). The run executes in the background
    /// and publishes telemetry under the endpoint-owned <c>runId</c> (via <c>Kobold.AssignRunId</c>), which the
    /// WebSocket transport is already subscribed to. Per-project parallel-Kobold limits are enforced inside
    /// <c>SummonKoboldAsync</c>; project execution state is checked up front.
    /// </summary>
    public sealed class ProjectRunModeHandler : IKoboldRunModeHandler
    {
        private readonly IProjectRepository _projects;
        private readonly DrakeFactory _drakeFactory;
        private readonly KoboldRunEventSource _eventSource;
        private readonly ILogger<ProjectRunModeHandler> _logger;

        public ProjectRunModeHandler(
            IProjectRepository projects,
            DrakeFactory drakeFactory,
            KoboldRunEventSource eventSource,
            ILogger<ProjectRunModeHandler> logger)
        {
            _projects = projects;
            _drakeFactory = drakeFactory;
            _eventSource = eventSource;
            _logger = logger;
        }

        public string Mode => "project";

        public async Task<KoboldRunStartInfo> StartAsync(KoboldRunRequest request, Guid runId, KoboldCaller caller, CancellationToken ct)
        {
            var project = string.IsNullOrWhiteSpace(request.ProjectId) ? null : _projects.GetById(request.ProjectId);
            EnsureRunnable(project, request);

            // Find the area task-file that holds the task, build a Drake for it, resolve the task + agent type.
            var (taskFilePath, area) = LocateTaskFile(project!, request.TaskId!);
            var drake = _drakeFactory.CreateDrake(
                taskFilePath: taskFilePath,
                drakeName: $"{project!.Name}:{area}:kobold-{runId:N}",
                specificationPath: project.Paths.Specification,
                projectId: project.Id);

            await drake.ReloadTasksFromFileAsync();

            var match = drake.GetUnassignedTasks()
                .FirstOrDefault(t => string.Equals(t.Task.Id, request.TaskId, StringComparison.OrdinalIgnoreCase));
            if (match.Task is null)
                throw new InvalidOperationException(
                    $"task '{request.TaskId}' is not an unassigned, ready task in project '{project.Name}' (already running/done, or blocked by dependencies)");

            var (task, agentType) = match;

            // Run in the background; the Kobold publishes under runId (the endpoint is already subscribed).
            // The pump terminates when the event source completes the run or we publish a terminal error below.
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await drake.ExecuteTaskAsync(task, agentType, runId: runId, cancellationToken: CancellationToken.None);
                    if (result is null)
                    {
                        // No Kobold was summoned (parallel limit) → it never emits a terminal event / CompleteRun,
                        // so publish the terminal error AND complete the run ourselves, else the pump and the
                        // RunRegistry reader would wait on a run that never finishes.
                        _eventSource.Publish(new RunErrorEvent { RunId = runId, Message = "kobold parallel limit reached for this project; try again shortly" });
                        _eventSource.CompleteRun(runId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "/kobold project run {RunId} (task {TaskId}) failed", runId, request.TaskId);
                    // If the throw happened before the Kobold started, no CompleteRun is coming — emit one (no-op
                    // if the Kobold already completed the run in its finally).
                    _eventSource.Publish(new RunErrorEvent { RunId = runId, Message = ex.Message });
                    _eventSource.CompleteRun(runId);
                }
            }, CancellationToken.None);

            return new KoboldRunStartInfo("project", null);
        }

        /// <summary>
        /// Validates the request can run: ids present, project exists, and its execution state is
        /// <see cref="ProjectExecutionState.Running"/> (Paused/Suspended/Cancelled are rejected). Pure — no IO.
        /// </summary>
        public static void EnsureRunnable(Project? project, KoboldRunRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ProjectId))
                throw new ArgumentException("project mode requires 'projectId'");
            if (string.IsNullOrWhiteSpace(request.TaskId))
                throw new ArgumentException("project mode requires 'taskId'");
            if (project is null)
                throw new InvalidOperationException($"project '{request.ProjectId}' not found");
            if (project.ExecutionState != ProjectExecutionState.Running)
                throw new InvalidOperationException(
                    $"project '{project.Name}' is {project.ExecutionState}; resume it before running tasks");
        }

        /// <summary>Finds the project task-file (and its area key) that contains <paramref name="taskId"/>.</summary>
        private static (string TaskFilePath, string Area) LocateTaskFile(Project project, string taskId)
        {
            foreach (var (area, path) in project.Paths.TaskFiles)
            {
                var full = Path.GetFullPath(path);
                if (!File.Exists(full)) continue;

                var tracker = new TaskTracker();
                tracker.LoadFromFile(full);
                if (tracker.GetAllTasks().Any(t => string.Equals(t.Id, taskId, StringComparison.OrdinalIgnoreCase)))
                    return (full, area);
            }
            throw new InvalidOperationException($"task '{taskId}' not found in any task file of project '{project.Name}'");
        }
    }
}
