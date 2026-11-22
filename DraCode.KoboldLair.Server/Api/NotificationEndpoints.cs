using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Server.Services;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// The caller's project notifications over REST (TASK-159, FEATURE-026 D17): the same records Dragon receives over
/// <c>/dragon</c>, from <see cref="ProjectNotificationService"/>, for the web bell and the project badges. Owner-scoped
/// through <see cref="ApiOwnership"/> like <c>/projects</c>: an admin passes <c>?scope=all</c> to see every project's,
/// and a notification of another owner's project reads as 404.
/// </summary>
public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder api)
    {
        // GET /notifications — unread first, then newest first
        api.MapGet("/notifications", (ICurrentUser user, IProjectRepository repo, ProjectNotificationService notifications,
                string? scope) =>
            {
                var views = VisibleProjects(user, repo, scope)
                    .SelectMany(p => notifications.GetNotifications(p.Name).Select(n => (Project: p, Notification: n)))
                    .OrderBy(x => x.Notification.ReadAt == null ? 0 : 1)
                    .ThenByDescending(x => x.Notification.CreatedAt)
                    .Select(x => View(x.Project, x.Notification));
                return Results.Ok(views);
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        // POST /notifications/{id}/read — only a notification of a project the caller can see
        api.MapPost("/notifications/{id}/read", (string id, ICurrentUser user, IProjectRepository repo,
                ProjectNotificationService notifications) =>
            {
                foreach (var project in VisibleProjects(user, repo, "all"))
                {
                    var notification = notifications.GetNotifications(project.Name).FirstOrDefault(n => n.Id == id);
                    if (notification == null)
                        continue;
                    notifications.MarkAsRead(project.Name, new[] { id });
                    return Results.Ok(View(project, notification));
                }
                return Results.NotFound();
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        return api;
    }

    /// <summary>The caller's projects; every project for an admin asking with <c>scope=all</c> (or with no identity).</summary>
    private static IEnumerable<Project> VisibleProjects(ICurrentUser user, IProjectRepository repo, string? scope)
    {
        var admin = ApiOwnership.IsAdmin(user);
        var caller = ApiOwnership.CallerId(user);
        return (admin && (scope == "all" || caller is null)) ? repo.GetAll() : repo.GetAllForOwner(caller ?? string.Empty);
    }

    private static object View(Project project, ProjectNotification n) => new
    {
        n.Id,
        projectId = project.Id,
        projectName = project.Name,
        type = n.Type,
        message = n.Message,
        metadata = n.Metadata,
        createdAt = n.CreatedAt,
        read = n.ReadAt != null,
        readAt = n.ReadAt
    };
}
