using DraCode.KoboldLair.Server.Models.WebSocket;

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
    /// Project run mode — execute an existing project task, reusing its plan/worktree. Stub: the plan and
    /// worktree-reuse mechanics land in TASK-040.
    /// </summary>
    public sealed class ProjectRunModeHandler : IKoboldRunModeHandler
    {
        public string Mode => "project";

        public Task<KoboldRunStartInfo> StartAsync(KoboldRunRequest request, Guid runId, KoboldCaller caller, CancellationToken ct)
            => throw new NotImplementedException("/kobold project mode is implemented in TASK-040.");
    }
}
