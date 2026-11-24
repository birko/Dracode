using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// A project's numbers over REST, for the workspace's Stats tab (TASK-158, FEATURE-026 D18): <c>GET
/// /projects/{id}/stats</c> — tasks by status and agent type, success rate, average task duration, cost by provider and
/// model and by day, and spend against the project budget (<see cref="ProjectStatsService"/>). Owner-scoped like
/// <c>/projects</c>: another owner's project is 404. <c>GET /stats</c> is the homepage's overview across the caller's
/// projects (TASK-148): active projects, tasks by status and per project, tasks done this week, spend this month and cost
/// for each of the last 30 days; <c>scope=all</c> widens it to every project for an admin only, as on <c>/projects</c>.
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

        // The homepage's numbers (TASK-148): the caller's projects, or every project for an admin asking with scope=all
        api.MapGet("/stats", async (string? scope, ICurrentUser user, IProjectRepository repo, ProjectStatsService stats, CancellationToken ct) =>
                Results.Ok(await stats.OverviewAsync(ApiOwnership.VisibleProjects(user, repo, scope).ToList(), DateTime.UtcNow, ct)))
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);
        return api;
    }
}
