using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// A project's branches over REST, for the workspace's Git tab (TASK-156, FEATURE-026 D20) — what Sentinel's git tools
/// give in the chat: <c>GET /projects/{id}/git/branches</c> lists the branches other than main with how far ahead and
/// behind main each is and whether it merges cleanly; <c>GET /projects/{id}/git/branches/{name}/diff</c> gives a branch's
/// changes against main file by file (the name URL-encoded, as branch names hold slashes). Git runs in the project's git
/// root (an imported project's source path). Read-only: nothing here checks out, merges or writes. Owner-scoped like
/// <c>/projects</c>: another owner's project, or a branch the repository does not have, is 404.
/// </summary>
public static class GitEndpoints
{
    public const string BaseBranch = "main";

    public static RouteGroupBuilder MapGitEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id}/git/branches", async (string id, ICurrentUser user, ProjectService projects, GitService git) =>
            {
                var folder = GitFolder(id, user, projects);
                if (folder is null)
                    return Results.NotFound();
                var repository = await git.IsRepositoryAsync(folder);
                var branches = repository ? await git.ListBranchesAsync(folder, BaseBranch) : [];
                return Results.Ok(new { repository, baseBranch = BaseBranch, branches });
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        api.MapGet("/projects/{id}/git/branches/{name}/diff", async (string id, string name, ICurrentUser user,
                ProjectService projects, GitService git) =>
            {
                var folder = GitFolder(id, user, projects);
                if (folder is null)
                    return Results.NotFound();
                var diff = await git.GetBranchFilesAsync(folder, Uri.UnescapeDataString(name), BaseBranch);
                return diff is null ? Results.NotFound() : Results.Ok(diff);
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        return api;
    }

    /// <summary>The git root of a project the caller can see; null for an unknown or another owner's project.</summary>
    private static string? GitFolder(string id, ICurrentUser user, ProjectService projects)
    {
        var project = projects.GetProject(id);
        if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
            return null;
        return projects.ResolveProjectFolder(project);
    }
}
