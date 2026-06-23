using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// <c>/api/v1/runs</c> REST surface (TASK-044): start a Kobold run and poll its status, reusing the same
/// <see cref="IKoboldRunModeHandler"/> dispatch as the <c>/kobold</c> WebSocket (one run engine, two
/// transports). Start is fire-and-forget — the run executes in the background and publishes to TASK-037's
/// event source; <see cref="RunRegistry"/> folds that stream into a queryable status that outlives the run.
/// Live streaming of the same run is TASK-045's SSE endpoint (it subscribes to the same <c>runId</c>).
/// </summary>
public static class RunsEndpoints
{
    /// <summary>Hangs the runs endpoints off the authed <c>/api/v1</c> group from <see cref="ApiV1Endpoints.MapApiV1"/>.</summary>
    public static RouteGroupBuilder MapRunEndpoints(this RouteGroupBuilder api)
    {
        // POST /api/v1/runs — start a run, return its id immediately (202). Body is the same permissive
        // KoboldRunRequest the WS endpoint parses; `mode` selects the handler (adhoc | project).
        api.MapPost("/runs", async (
                KoboldRunRequest request,
                ICurrentUser user,
                IEnumerable<IKoboldRunModeHandler> handlers,
                RunRegistry registry,
                HttpContext ctx) =>
            {
                if (string.IsNullOrWhiteSpace(request.Mode))
                    return Results.BadRequest(new { error = "missing 'mode' in request body" });

                var handler = handlers.FirstOrDefault(h =>
                    string.Equals(h.Mode, request.Mode, StringComparison.OrdinalIgnoreCase));
                if (handler is null)
                    return Results.BadRequest(new { error = $"unknown mode '{request.Mode}'" });

                var owner = user.UserId?.ToString();
                var caller = new KoboldCaller(owner, IsAdmin(user), user.GetClaim("name"), user.Email);

                // Subscribe-before-start: register (which subscribes to the event source) before the run begins,
                // so no early/terminal events are missed (the event source has no replay).
                var runId = Guid.NewGuid();
                registry.Register(runId, owner, handler.Mode);

                // Fire-and-forget: StartAsync kicks the run off on a background task and returns its start info.
                // A synchronous failure (bad payload, project not runnable) surfaces as 400 and fails the run.
                try
                {
                    var info = await handler.StartAsync(request, runId, caller, ctx.RequestAborted);
                    return Results.Accepted($"/api/v1/runs/{runId}",
                        new { runId, mode = info.Mode, worktree = info.Worktree });
                }
                catch (Exception ex)
                {
                    // Any synchronous start failure must fail the run (else it strands in Pending — never pruned,
                    // its event-source reader parked). Caller-input errors → 400; anything else → 500.
                    registry.Fail(runId, ex.Message);
                    return ex is ArgumentException or InvalidOperationException
                        ? Results.BadRequest(new { error = ex.Message })
                        : Results.Problem(detail: "failed to start run", statusCode: StatusCodes.Status500InternalServerError);
                }
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        // GET /api/v1/runs/{id} — current status. 404 (not 403) for unknown OR not-yours, so existence
        // of another caller's run isn't leaked.
        api.MapGet("/runs/{id:guid}", (Guid id, ICurrentUser user, RunRegistry registry) =>
            {
                var run = registry.Get(id);
                if (run is null)
                    return Results.NotFound();

                // Ownership: admins see all; otherwise the caller must have a concrete identity that matches the
                // run's owner. A null caller identity or a null-owner run is never readable by a non-admin (so a
                // subject-less token can't read another subject-less caller's run). 404 (not 403) avoids leaking existence.
                var owner = user.UserId?.ToString();
                if (!IsAdmin(user) && (owner is null || run.Owner is null || run.Owner != owner))
                    return Results.NotFound();

                return Results.Ok(new
                {
                    runId = run.RunId,
                    mode = run.Mode,
                    status = run.State.ToString().ToLowerInvariant(),
                    startedAt = run.StartedAt,
                    completedAt = run.CompletedAt,
                    completedSteps = run.CompletedSteps,
                    totalSteps = run.TotalSteps,
                    summary = run.Summary
                });
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        return api;
    }

    private static bool IsAdmin(ICurrentUser user) =>
        user.Permissions.Contains("*") || user.Permissions.Contains(KoboldLairPermissionChecker.ViewAll);
}
