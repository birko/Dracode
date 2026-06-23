using DraCode.KoboldLair.Server.Models.WebSocket;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>The caller's resolved identity for a <c>/kobold</c> run (mirrors <c>ResolveCaller</c> in Program.cs).</summary>
    public sealed record KoboldCaller(string? Sub, bool IsAdmin, string? Name, string? Email);

    /// <summary>What a mode handler reports back after starting a run — surfaced in <c>kobold_run_started</c>.</summary>
    public sealed record KoboldRunStartInfo(string Mode, string? Worktree);

    /// <summary>
    /// Strategy for the two <c>/kobold</c> run modes (TASK-038 dispatch seam). The endpoint owns the
    /// <c>runId</c> and subscribes to the event source <em>before</em> calling <see cref="StartAsync"/>,
    /// so a handler must start a Kobold that publishes telemetry under the supplied <paramref name="runId"/>
    /// — closing the "no subscriber ⇒ events dropped" race. The concrete mechanics are TASK-039 (ad-hoc)
    /// and TASK-040 (project); the stubs here keep the protocol/dispatch testable until then.
    /// </summary>
    public interface IKoboldRunModeHandler
    {
        /// <summary>Mode discriminator matched (case-insensitively) against <see cref="KoboldRunRequest.Mode"/>.</summary>
        string Mode { get; }

        /// <summary>
        /// Starts a Kobold run publishing under <paramref name="runId"/>. Returns the start metadata for
        /// the <c>kobold_run_started</c> frame. Throwing surfaces an <c>error</c> frame to the client.
        /// </summary>
        Task<KoboldRunStartInfo> StartAsync(KoboldRunRequest request, Guid runId, KoboldCaller caller, CancellationToken ct);
    }
}
