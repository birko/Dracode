using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Server.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// What waits on the caller (TASK-147, FEATURE-026 D10), for the homepage's "Needs you" list and the header's pill:
/// for each of the caller's projects, its open decisions (the decision store, the source of truth TASK-160's cards read,
/// so an answered one is gone), failed and blocked tasks, and feature branches ready to merge (their unread
/// notifications). Owner-scoped through <see cref="ApiOwnership"/> like <c>/projects</c>: an admin passes
/// <c>?scope=all</c> to see every project's; <c>?projectId=</c> narrows to one project, and another owner's reads as 404.
/// </summary>
public static class NeedsYouEndpoints
{
    public const string Decision = "decision";
    public const string FailedTask = "failed_task";
    public const string BlockedTask = "blocked_task";
    public const string MergeReady = "merge_ready";
    /// <summary>An answer an agent gave instead of the person, until they keep or overturn it (TASK-208).</summary>
    public const string AgentAnswer = "agent_answer";

    public sealed record Item(string Id, string Kind, string ProjectId, string ProjectName, string Title, DateTime CreatedAt);

    public static RouteGroupBuilder MapNeedsYouEndpoints(this RouteGroupBuilder api)
    {
        // GET /needs-you — newest first
        api.MapGet("/needs-you", async (ICurrentUser user, IProjectRepository repo, ProjectNotificationService notifications,
                IServiceProvider services, string? scope, string? projectId) =>
            {
                var projects = ApiOwnership.VisibleProjects(user, repo, projectId == null ? scope : "all").ToList();
                if (projectId != null)
                {
                    projects = projects.Where(p => p.Id == projectId).ToList();
                    if (projects.Count == 0)
                        return Results.NotFound();
                }

                var tasks = services.GetService<ITaskRepository>();
                var decisions = services.GetService<SqlPendingDecisionRepository>();
                var items = new List<Item>();
                foreach (var project in projects)
                {
                    if (decisions != null)
                    {
                        foreach (var d in await decisions.GetUnresolvedForProjectAsync(project.Id))
                            items.Add(new Item(d.Id, Decision, project.Id, project.Name, PendingDecisionNotification.QuestionText(d), d.CreatedAt));
                        // An agent answered instead of the person: shown until they keep or overturn it (TASK-208)
                        foreach (var d in await decisions.GetAwaitingReviewForProjectAsync(project.Id))
                            items.Add(new Item(d.Id, AgentAnswer, project.Id, project.Name,
                                $"{AgentAnswerReview.LevelName(d.AnsweredBy!.Value)} answered: {PendingDecisionNotification.QuestionText(d)} → {d.Resolution}",
                                d.ResolvedAt ?? d.CreatedAt));
                    }
                    if (tasks != null)
                    {
                        foreach (var t in await tasks.GetByProjectAsync(project.Id))
                        {
                            var kind = t.Status switch
                            {
                                TaskStatus.Failed => FailedTask,
                                TaskStatus.BlockedByFailure => BlockedTask,
                                _ => null
                            };
                            if (kind != null)
                                items.Add(new Item(t.Id, kind, project.Id, project.Name, TaskTitle(t.Task), t.UpdatedAt ?? t.CreatedAt));
                        }
                    }
                    foreach (var n in notifications.GetPendingNotifications(project.Name).Where(n => n.Type == "feature_branch_ready"))
                        items.Add(new Item(n.Id, MergeReady, project.Id, project.Name, n.Message, n.CreatedAt));
                }
                return Results.Ok(items.OrderByDescending(i => i.CreatedAt));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);
        return api;
    }

    /// <summary>A task's title: its first non-empty line, without Markdown heading or list marks or bold.</summary>
    public static string TaskTitle(string task)
    {
        var line = task.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        line = line.TrimStart('#').TrimStart();
        if (line.StartsWith("- ") || line.StartsWith("* "))
            line = line[2..];
        return line.Replace("**", "");
    }
}
