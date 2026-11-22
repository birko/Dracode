using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// A project's numbers over REST, for the workspace's Stats tab (TASK-158, FEATURE-026 D18): <c>GET
/// /projects/{id}/stats</c> — tasks by status and agent type, success rate, average task duration, cost by provider and
/// model and by day, and spend against the project budget (<see cref="ProjectStatsService"/>). Owner-scoped like
/// <c>/projects</c>: another owner's project is 404.
/// </summary>
public static class StatsEndpoints
{
    public static RouteGroupBuilder MapStatsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id}/stats", async (string id, ICurrentUser user, ProjectService projects, ProjectStatsService stats,
                CancellationToken ct) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                return Results.Ok(await stats.ForAsync([project], ct));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);
        return api;
    }
}
