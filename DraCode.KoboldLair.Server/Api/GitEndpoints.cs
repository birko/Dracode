using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Server.Services;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// A project's branches over REST, for the workspace's Git tab (TASK-156, FEATURE-026 D20) — what Sentinel's git tools
/// give in the chat: <c>GET /projects/{id}/git/branches</c> lists the branches other than main with how far ahead and
/// behind main each is and whether it merges cleanly; <c>GET /projects/{id}/git/branches/{name}/diff</c> gives a branch's
/// changes against main file by file (the name URL-encoded, as branch names hold slashes). Git runs in the project's git
/// root (an imported project's source path). The two reads check nothing out and write nothing; the merge (TASK-157,
/// manage-projects permission) merges a branch into main and deletes it, or answers 409 with the conflicting files
/// without touching the repository. Owner-scoped like <c>/projects</c>: another owner's project, or a branch the
/// repository does not have, is 404.
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

        // POST /projects/{id}/git/branches/{name}/merge — merge into main and delete the branch (TASK-157). The merge is
        // checked read-only first (git merge-tree), so a conflict answers 409 with the files before git touches the
        // repository; a clean branch goes through GitService.MergeBranchAsync, as Sentinel's git_merge does.
        api.MapPost("/projects/{id}/git/branches/{name}/merge", async (string id, string name, ICurrentUser user,
                ProjectService projects, GitService git, ProjectNotificationService notifications) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                var folder = projects.ResolveProjectFolder(project);
                var branch = Uri.UnescapeDataString(name);
                if (folder is null || branch == BaseBranch || !await git.BranchExistsAsync(folder, branch))
                    return Results.NotFound();

                var (clean, conflicts) = await git.CheckMergeAsync(folder, branch, BaseBranch);
                if (clean == false)
                    return Results.Conflict(new { error = "conflicts", message = $"{branch} does not merge into {BaseBranch} cleanly.", conflicts });

                var merge = await git.MergeBranchAsync(folder, branch, BaseBranch);
                if (!merge.Success)
                {
                    return merge.HasConflicts
                        ? Results.Conflict(new { error = "conflicts", message = $"{branch} does not merge into {BaseBranch} cleanly.", conflicts = merge.ConflictFiles })
                        : Results.Conflict(new { error = "merge_failed", message = merge.ErrorMessage ?? "The merge did not go through.", conflicts = new List<string>() });
                }

                var deleted = await git.DeleteBranchAsync(folder, branch);
                // Its "ready to merge" notification is done with: it leaves the bell and Needs you (TASK-147)
                var ready = notifications.GetPendingNotifications(project.Name)
                    .Where(n => n.Type == "feature_branch_ready" && n.Metadata.TryGetValue("branchName", out var b) && b == branch)
                    .Select(n => n.Id)
                    .ToList();
                if (ready.Count > 0)
                    notifications.MarkAsRead(project.Name, ready);
                return Results.Ok(new { merged = true, branch, mergeCommit = merge.MergeCommitHash, deleted });
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageProjects);

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
