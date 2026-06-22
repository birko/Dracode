using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// The <c>/api/v1</c> REST facade route group (TASK-042 / STORY-016). Establishes the group + its
/// auth defaults behind TASK-032's JWT/permission pipeline; the resource endpoints (TASK-043/044/046)
/// hang off the same group via <see cref="MapApiV1"/>'s returned builder. Mapped only when JWT is
/// enabled (the authorization pipeline is itself gated on that), so auth is always enforced here.
/// </summary>
public static class ApiV1Endpoints
{
    /// <summary>
    /// Maps the <c>/api/v1</c> group with group-wide <c>RequireAuthorization()</c> and the skeleton
    /// endpoints, and returns the group builder so later tasks attach their resource routes to it.
    /// Per-resource permissions are applied per endpoint (resources differ), not group-wide.
    /// </summary>
    public static RouteGroupBuilder MapApiV1(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();

        // Caller identity + resolved permissions — proves the auth + permission pipeline end-to-end
        // (moved here from Program.cs so the whole /api/v1 surface lives in one place; path unchanged).
        api.MapGet("/whoami", (Birko.Security.AspNetCore.ICurrentUser currentUser) =>
                Results.Ok(new
                {
                    userId = currentUser.UserId,
                    permissions = currentUser.Permissions.ToArray()
                }))
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        // Trivial stub proving a resource route returns through the pipeline with auth enforced
        // (TASK-042 AC#4). Real implementation: TASK-046.
        api.MapGet("/agents/active", () => Results.Ok(Array.Empty<object>()))
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        return api;
    }
}
