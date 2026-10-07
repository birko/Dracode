using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Events.Run;
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

                var owner = user.UserGuid?.ToString();
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
                var owner = user.UserGuid?.ToString();
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

        // GET /api/v1/runs/{id}/events — the run's events as a text/event-stream, from now until the run ends
        // (TASK-045). Same frames as the /kobold WebSocket: `event:` is the frame type, `data:` its JSON. Browsers
        // authenticate with ?token= (EventSource cannot set headers). Same ownership rule as GET /runs/{id}.
        api.MapGet("/runs/{id:guid}/events", (Guid id, ICurrentUser user, RunRegistry registry,
                KoboldRunEventSource events, HttpContext ctx) =>
            {
                var run = registry.Get(id);
                var owner = user.UserGuid?.ToString();
                if (run is null || (!IsAdmin(user) && (owner is null || run.Owner is null || run.Owner != owner)))
                    return Task.FromResult<IResult>(Results.NotFound());

                return StreamRunEventsAsync(id, registry, events, ctx);
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        return api;
    }

    /// <summary>How often the stream sends a comment line, keeping proxies from closing an idle connection and
    /// letting the stream notice a run that ended without delivering its terminal event.</summary>
    public static TimeSpan SseHeartbeat { get; set; } = TimeSpan.FromSeconds(15);

    private static async Task<IResult> StreamRunEventsAsync(Guid id, RunRegistry registry, KoboldRunEventSource events,
        HttpContext ctx)
    {
        // Subscribe before the headers go out, so a client that has the headers is already receiving events
        using var subscription = events.Subscribe(id, out var reader);

        var response = ctx.Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no"; // nginx: do not buffer the stream
        await response.Body.FlushAsync(ctx.RequestAborted);

        var ct = ctx.RequestAborted;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // The run may have ended before the subscription existed (no replay) — then report how it ended
                if (registry.Get(id) is { IsTerminal: true } ended && !reader.TryPeek(out _))
                {
                    await WriteFrameAsync(response, TerminalFrame(ended), ct);
                    break;
                }

                using var beat = CancellationTokenSource.CreateLinkedTokenSource(ct);
                beat.CancelAfter(SseHeartbeat);
                KoboldRunEvent evt;
                try
                {
                    if (!await reader.WaitToReadAsync(beat.Token))
                    {
                        // Reader completed (run dropped); the registry check above ends the stream once it has folded the end
                        await Task.Delay(50, ct);
                        continue;
                    }
                    if (!reader.TryRead(out evt!))
                        continue;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    await response.WriteAsync(": heartbeat\n\n", ct);
                    await response.Body.FlushAsync(ct);
                    continue;
                }

                await WriteFrameAsync(response, KoboldWireMessage.From(evt), ct);
                if (evt is RunCompletedEvent or RunErrorEvent)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — disposing the subscription detaches it
        }

        return Results.Empty;
    }

    private static async Task WriteFrameAsync(HttpResponse response, KoboldWireMessage frame, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(frame, KoboldWireMessage.JsonOptions);
        await response.WriteAsync($"id: {frame.Seq}\nevent: {frame.Type}\ndata: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    /// <summary>The closing frame for a run that had already ended, rebuilt from its registry record.</summary>
    private static KoboldWireMessage TerminalFrame(RunRegistry.RunRecord run) =>
        run.State == RunRegistry.RunState.Completed
            ? new KoboldWireMessage
            {
                Type = "kobold_complete", RunId = run.RunId, FinalStatus = run.Summary,
                CompletedSteps = run.CompletedSteps, TotalSteps = run.TotalSteps
            }
            : new KoboldWireMessage { Type = "error", RunId = run.RunId, Message = run.Summary };

    private static bool IsAdmin(ICurrentUser user) => ApiOwnership.IsAdmin(user);
}
