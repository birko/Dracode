using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Server.Api;

/// <summary>
/// The CRUD bulk of the <c>/api/v1</c> facade (TASK-043): projects, specification, features, tasks and
/// plans — thin minimal-API handlers over the <b>same services the Dragon council tools call</b>
/// (<see cref="ProjectService"/>, <see cref="IProjectRepository"/>, <see cref="ITaskRepository"/>,
/// <see cref="KoboldPlanService"/>, <see cref="SpecificationService"/>), so there is no duplicated logic.
/// Per-caller scoping goes through <see cref="ApiOwnership"/> (404-not-403). Hung off the authed
/// <c>/api/v1</c> group from <see cref="ApiV1Endpoints.MapApiV1"/>.
/// </summary>
public static class ResourceEndpoints
{
    public static RouteGroupBuilder MapResourceEndpoints(this RouteGroupBuilder api)
    {
        MapProjectEndpoints(api);
        MapTaskEndpoints(api);
        MapPlanEndpoints(api);
        return api;
    }

    // ---- Projects -------------------------------------------------------

    private static void MapProjectEndpoints(RouteGroupBuilder api)
    {
        // GET /projects — owner-scoped; admins may pass ?scope=all to see every project.
        api.MapGet("/projects", (ICurrentUser user, IProjectRepository repo, string? scope) =>
            {
                var admin = ApiOwnership.IsAdmin(user);
                var list = (admin && scope == "all")
                    ? repo.GetAll()
                    : admin && user.UserId is null
                        ? repo.GetAll()                       // auth-off single-user → see all
                        : repo.GetAllForOwner(user.UserId?.ToString() ?? string.Empty);
                return Results.Ok(list.Select(ProjectView));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        // POST /projects — create a project owned by the caller; optional initial specification content.
        api.MapPost("/projects", async (CreateProjectDto dto, ICurrentUser user,
                ProjectService projects, IProjectRepository repo, SpecificationService specs) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Results.BadRequest(new { error = "name is required" });

                // Validate everything BEFORE any file write, so a rejected request never orphans a
                // project folder / specification.md on disk.
                var owner = user.UserId?.ToString();
                if (string.IsNullOrWhiteSpace(owner))
                    // RegisterProject requires a real owner sub (FEATURE-019 D8); a subject-less caller
                    // (e.g. a service-account token) gets a clean 400, not a 500 + orphaned files.
                    return Results.BadRequest(new { error = "an authenticated caller identity (sub) is required to create a project" });
                // The project folder is name-derived, so names are unique. Refuse a collision with 409 —
                // otherwise SaveContentAsync would overwrite another owner's specification.md and
                // RegisterProject would hand back their project (cross-tenant write + leak).
                if (repo.GetByName(dto.Name) is not null)
                    return Results.Conflict(new { error = $"a project named '{dto.Name}' already exists" });

                await projects.CreateProjectFolderAsync(dto.Name);
                var spec = await specs.SaveContentAsync(dto.Name, dto.SpecificationContent ?? string.Empty);
                var project = projects.RegisterProject(dto.Name, spec.FilePath, owner);
                return Results.Created($"/api/v1/projects/{project.Id}", ProjectView(project));
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageProjects);

        // GET /projects/{id}
        api.MapGet("/projects/{id}", (string id, ICurrentUser user, ProjectService projects) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                return Results.Ok(ProjectView(project));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        // DELETE /projects/{id}
        api.MapDelete("/projects/{id}", async (string id, ICurrentUser user,
                ProjectService projects, IProjectRepository repo) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                await repo.DeleteAsync(id);
                return Results.NoContent();
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageProjects);

        // GET/PUT /projects/{id}/specification
        api.MapGet("/projects/{id}/specification", async (string id, ICurrentUser user,
                ProjectService projects, SpecificationService specs) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                var spec = await specs.LoadByNameAsync(project.Name);
                return spec is null
                    ? Results.Ok(new { project.Name, content = "", version = 0 })
                    : Results.Ok(new { spec.Name, spec.Content, spec.Version, spec.ContentHash });
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        api.MapPut("/projects/{id}/specification", async (string id, UpdateSpecDto dto, ICurrentUser user,
                ProjectService projects, SpecificationService specs) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                if (dto.Content is null)
                    return Results.BadRequest(new { error = "content is required" });
                var spec = await specs.SaveContentAsync(project.Name, dto.Content);
                return Results.Ok(new { spec.Name, spec.Version, spec.ContentHash });
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageProjects);

        // GET/POST /projects/{id}/features, DELETE …/features/{featureId}
        api.MapGet("/projects/{id}/features", async (string id, ICurrentUser user,
                ProjectService projects, SpecificationService specs) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                var spec = await specs.LoadByNameAsync(project.Name);
                return Results.Ok((spec?.Features ?? new List<Feature>()).Select(FeatureView));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        api.MapPost("/projects/{id}/features", async (string id, CreateFeatureDto dto, ICurrentUser user,
                ProjectService projects, SpecificationService specs) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Description))
                    return Results.BadRequest(new { error = "name and description are required" });

                var spec = await specs.LoadByNameAsync(project.Name);
                if (spec is null)
                    return Results.NotFound();

                var feature = new Feature
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    Priority = dto.Priority ?? "medium",
                    SpecificationId = spec.Id,
                    Status = FeatureStatus.Draft
                };
                spec.WithFeatures(f => f.Add(feature));
                await SpecificationService.PersistFeaturesAsync(spec);
                return Results.Created($"/api/v1/projects/{id}/features/{feature.Id}", FeatureView(feature));
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageProjects);

        api.MapDelete("/projects/{id}/features/{featureId}", async (string id, string featureId,
                ICurrentUser user, ProjectService projects, SpecificationService specs) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                var spec = await specs.LoadByNameAsync(project.Name);
                if (spec is null)
                    return Results.NotFound();

                var removed = spec.WithFeatures(f =>
                {
                    var match = f.FirstOrDefault(x => x.Id == featureId);
                    if (match is null)
                        return false;
                    f.Remove(match);
                    return true;
                });
                if (!removed)
                    return Results.NotFound();
                await SpecificationService.PersistFeaturesAsync(spec);
                return Results.NoContent();
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageProjects);
    }

    // ---- Tasks ----------------------------------------------------------

    private static void MapTaskEndpoints(RouteGroupBuilder api)
    {
        // GET /projects/{id}/tasks
        api.MapGet("/projects/{id}/tasks", async (string id, ICurrentUser user,
                ProjectService projects, ITaskRepository? tasks) =>
            {
                if (tasks is null) return TaskStoreUnavailable();
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                var list = await tasks.GetByProjectAsync(id);
                return Results.Ok(list.Select(TaskView));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        // GET /tasks/{id}
        api.MapGet("/tasks/{id}", async (string id, ICurrentUser user,
                ProjectService projects, ITaskRepository? tasks) =>
            {
                if (tasks is null) return TaskStoreUnavailable();
                var task = await tasks.GetByIdAsync(id);
                if (task is null || !CanAccessTask(user, projects, task))
                    return Results.NotFound();
                return Results.Ok(TaskView(task));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        // POST /tasks/{id}/retry — reset a failed task so the Drake picks it up again.
        api.MapPost("/tasks/{id}/retry", async (string id, ICurrentUser user,
                ProjectService projects, ITaskRepository? tasks) =>
            {
                if (tasks is null) return TaskStoreUnavailable();
                var task = await tasks.GetByIdAsync(id);
                if (task is null || !CanAccessTask(user, projects, task))
                    return Results.NotFound();
                if (task.Status != TaskStatus.Failed)
                    return Results.BadRequest(new { error = $"task is {task.Status}; only Failed tasks can be retried" });
                await tasks.ClearErrorAsync(id);
                await tasks.SetStatusAsync(id, TaskStatus.Unassigned);
                return Results.Ok(new { id, status = TaskStatus.Unassigned.ToString() });
            })
            .RequirePermission(KoboldLairPermissionChecker.ExecuteAgents);

        // POST /tasks/{id}/priority — manual priority override.
        api.MapPost("/tasks/{id}/priority", async (string id, SetPriorityDto dto, ICurrentUser user,
                ProjectService projects, ITaskRepository? tasks) =>
            {
                if (tasks is null) return TaskStoreUnavailable();
                if (!Enum.TryParse<TaskPriority>(dto.Priority, ignoreCase: true, out var priority))
                    return Results.BadRequest(new { error = "priority must be one of: low, normal, high, critical" });
                var task = await tasks.GetByIdAsync(id);
                if (task is null || !CanAccessTask(user, projects, task))
                    return Results.NotFound();
                task.Priority = priority;
                await tasks.UpdateTaskAsync(task);
                return Results.Ok(new { id, priority = priority.ToString() });
            })
            .RequirePermission(KoboldLairPermissionChecker.ManageProjects);
    }

    // ---- Plans ----------------------------------------------------------

    private static void MapPlanEndpoints(RouteGroupBuilder api)
    {
        // GET /projects/{id}/plans
        api.MapGet("/projects/{id}/plans", async (string id, ICurrentUser user,
                ProjectService projects, KoboldPlanService plans) =>
            {
                var project = projects.GetProject(id);
                if (project is null || !ApiOwnership.CanAccess(user, project.OwnerId))
                    return Results.NotFound();
                var list = await plans.GetPlansForProjectAsync(id);
                return Results.Ok(list.Select(PlanSummary));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);

        // GET /plans/{id} — {id} is the taskId (one plan per task); returns step progress.
        api.MapGet("/plans/{id}", async (string id, ICurrentUser user,
                ProjectService projects, ITaskRepository? tasks, KoboldPlanService plans) =>
            {
                if (tasks is null) return TaskStoreUnavailable();
                var task = await tasks.GetByIdAsync(id);
                if (task is null || task.ProjectId is null || !CanAccessTask(user, projects, task))
                    return Results.NotFound();
                var plan = await plans.LoadPlanAsync(task.ProjectId, id);
                return plan is null ? Results.NotFound() : Results.Ok(PlanDetail(plan));
            })
            .RequirePermission(KoboldLairPermissionChecker.ViewOwn);
    }

    // ---- Helpers + projections -----------------------------------------

    private static IResult TaskStoreUnavailable() =>
        Results.Problem(detail: "task repository is unavailable (requires the SQLite backend)",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static bool CanAccessTask(ICurrentUser user, ProjectService projects, TaskRecord task)
    {
        if (ApiOwnership.IsAdmin(user))
            return true;
        if (task.ProjectId is null)
            return false;
        var project = projects.GetProject(task.ProjectId);
        return project is not null && ApiOwnership.CanAccess(user, project.OwnerId);
    }

    private static object ProjectView(Project p) => new
    {
        p.Id,
        p.Name,
        ownerId = p.OwnerId,
        status = p.Status.ToString(),
        executionState = p.ExecutionState.ToString(),
        createdAt = p.Timestamps.CreatedAt
    };

    private static object FeatureView(Feature f) => new
    {
        f.Id,
        f.Name,
        f.Description,
        f.Priority,
        status = f.Status.ToString()
    };

    private static object TaskView(TaskRecord t) => new
    {
        t.Id,
        task = t.Task,
        projectId = t.ProjectId,
        status = t.Status.ToString(),
        priority = t.Priority.ToString(),
        assignedAgent = t.AssignedAgent,
        featureId = t.FeatureId,
        errorMessage = t.ErrorMessage,
        commitFailed = t.CommitFailed
    };

    private static object PlanSummary(KoboldImplementationPlan p) => new
    {
        taskId = p.TaskId,
        projectId = p.ProjectId,
        status = p.Status.ToString(),
        totalSteps = p.Steps.Count,
        completedSteps = p.Steps.Count(s => s.Status == StepStatus.Completed),
        createdAt = p.CreatedAt
    };

    private static object PlanDetail(KoboldImplementationPlan p) => new
    {
        taskId = p.TaskId,
        projectId = p.ProjectId,
        taskDescription = p.TaskDescription,
        status = p.Status.ToString(),
        totalSteps = p.Steps.Count,
        completedSteps = p.Steps.Count(s => s.Status == StepStatus.Completed),
        createdAt = p.CreatedAt,
        steps = p.Steps.Select(s => new
        {
            s.Index,
            s.Title,
            s.Description,
            status = s.Status.ToString()
        })
    };

    // ---- Request DTOs ---------------------------------------------------

    public sealed record CreateProjectDto(string Name, string? SpecificationContent);
    public sealed record UpdateSpecDto(string? Content);
    public sealed record CreateFeatureDto(string Name, string Description, string? Priority);
    public sealed record SetPriorityDto(string Priority);
}
