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
    /// Ad-hoc run mode — execute a free-form task in a transient workspace. Stub: the git/worktree and
    /// Kobold-summoning mechanics land in TASK-039. Until then the endpoint dispatches here and the
    /// thrown error is relayed as a <c>kobold_*</c> <c>error</c> frame.
    /// </summary>
    public sealed class AdHocRunModeHandler : IKoboldRunModeHandler
    {
        public string Mode => "adhoc";

        public Task<KoboldRunStartInfo> StartAsync(KoboldRunRequest request, Guid runId, KoboldCaller caller, CancellationToken ct)
            => throw new NotImplementedException("/kobold ad-hoc mode is implemented in TASK-039.");
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
                        _eventSource.Publish(new RunErrorEvent { RunId = runId, Message = "kobold parallel limit reached for this project; try again shortly" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "/kobold project run {RunId} (task {TaskId}) failed", runId, request.TaskId);
                    _eventSource.Publish(new RunErrorEvent { RunId = runId, Message = ex.Message });
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
