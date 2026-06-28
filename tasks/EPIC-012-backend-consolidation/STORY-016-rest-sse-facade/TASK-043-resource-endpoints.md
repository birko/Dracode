---
id: TASK-043
parent: STORY-016
feature: FEATURE-018
status: done
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-042, TASK-034]
blocks: []
pr: 631e7ff
github-issue: null
jira-key: null
---

# Project / spec / feature / task / plan REST endpoints

## Context

The CRUD bulk of the facade. Thin minimal-API handlers wrapping the **same services the Dragon council tools already call** (project/spec/feature/task services, `SqlPlanRepository`) — no duplicated logic. Per-caller scoping uses `ICurrentUser` + `ownerId` (TASK-034).

## Acceptance criteria

- [x] Projects: `GET/POST /projects`, `GET/DELETE /projects/{id}` (list filtered by owner; `?scope=all` for admins)
- [x] Spec: `GET/PUT /projects/{id}/specification`
- [x] Features: `GET/POST /projects/{id}/features`, `DELETE …/features/{featureId}`
- [x] Tasks: `GET /projects/{id}/tasks`, `GET /tasks/{id}`, `POST /tasks/{id}/retry`, `POST /tasks/{id}/priority`
- [x] Plans: `GET /projects/{id}/plans`, `GET /plans/{id}` (with step progress; `{id}` = taskId, one plan per task)
- [x] All wrap existing services; ownership enforced via shared `ApiOwnership` (404-not-403); 10 WebApplicationFactory tests cover scoping + each verb (full suite 170/170)

## Out of scope

- Runs/SSE (TASK-044/045); agents/cost (TASK-046)

## Human test plan

- [x] As user A, create a project and list — see it; as user B, list — don't see A's; admin `?scope=all` sees both — **[auto → spec]** executed by `ResourceEndpointsTests.Project_listing_is_owner_scoped_and_admin_sees_all` (WebApplicationFactory boots the real pipeline + SQLite; mints distinct A/B/admin JWTs). Verified 2026-06-28. A live multi-user `curl` run is deferred until the login UI (TASK-056) exists; the scoping mechanism is proven here.

## Implementation plan

> ⚠ Acceptance criteria question: the criteria say "all wrap existing services", but spec + features
> have **no DI service** to wrap — the Dragon tools (`SpecificationManagementTool`, `DeleteFeatureTool`)
> operate on an in-memory `Dictionary<string, Specification>` + raw file I/O (`specification.md` +
> `specification.features.json`). This plan adds a thin `SpecificationService` as a prerequisite. If the
> reviewer would rather the spec/feature endpoints do direct file I/O instead, drop step 1 and inline it.

**Shape:** one `ResourceEndpoints.cs` in `DraCode.KoboldLair.Server/Api/`, exposing
`MapResourceEndpoints(this RouteGroupBuilder api)`, hung off the existing `/api/v1` group in
`Program.cs:1011` (right after `apiV1.MapRunEndpoints()`). Thin minimal-API handlers — exact mirror of
`RunsEndpoints.cs`: `ICurrentUser` for identity, `.RequirePermission(...)` per endpoint, **404-not-403**
for cross-owner reads (so existence isn't leaked — copy the rule at `RunsEndpoints.cs:68`).

**Ownership model:** tasks/plans/specs don't carry `ownerId` — resolve ownership through the parent
**project's** `OwnerId` (one `ProjectService.GetProject` lookup per handler). Lift the
`IsAdmin(ICurrentUser)` + owner-check out of `RunsEndpoints` into a shared `ApiOwnership` static so all
handlers share one rule. Owner-scoped listing reuses `IProjectRepository.GetAllForOwner(sub)` /
`GetAll()` (admin) — already shipped by TASK-034.

### Endpoint → service mapping

| Route | Wraps | Permission |
|-------|-------|-----------|
| `GET /projects` (`?scope=all`) | `IProjectRepository.GetAllForOwner` / `GetAll` | `view_own` |
| `POST /projects` | `ProjectService.CreateProjectFolderAsync` + `RegisterProject(name, spec, ownerId)` | `manage_projects` |
| `GET /projects/{id}` · `DELETE /projects/{id}` | `ProjectService.GetProject` · `IProjectRepository.DeleteAsync` | `view_own` · `manage_projects` |
| `GET/PUT /projects/{id}/specification` | **new `SpecificationService`** | `view_own` · `manage_projects` |
| `GET/POST /projects/{id}/features` · `DELETE …/{featureId}` | **new `SpecificationService`** (+ `SpecificationEventService` for history) | `view_own` · `manage_projects` |
| `GET /projects/{id}/tasks` · `GET /tasks/{id}` | `ITaskRepository.GetByProjectAsync` · `GetByIdAsync` | `view_own` |
| `POST /tasks/{id}/retry` | retry logic from `RetryFailedTaskTool` (ProjectService + DrakeFactory) | `execute_agents` |
| `POST /tasks/{id}/priority` | priority logic from `SetTaskPriorityTool` | `manage_projects` |
| `GET /projects/{id}/plans` · `GET /plans/{id}` | `SqlPlanRepository` / `KoboldPlanService` (incl. step progress) | `view_own` |

### Build sequence (sub-steps)

1. **Extract `SpecificationService`** (`DraCode.KoboldLair/Services/`) — load/save `specification.md` +
   `specification.features.json` (wrapped version+hash format), add/delete feature, version bump. Retro-fit
   `SpecificationManagementTool` / `DeleteFeatureTool` / `FeatureManagementTool` onto it (no behavior
   change — their existing tests must stay green). **Keystone; only non-mechanical step.**
2. **Projects + tasks + plans endpoints** (clean wraps) + the shared `ApiOwnership` helper.
3. **Spec + features endpoints** on the new service.
4. Wire `apiV1.MapResourceEndpoints()` in `Program.cs`; full test pass.

### Tests (mirror `RunsEndpointsTests`, `WebApplicationFactory<Program>`)

- Per verb: 401 unauth · 200 own · 404 other-owner · `?scope=all` admin sees all.
- Create→list round-trip; delete; spec PUT bumps version; feature POST/DELETE; task retry resets status;
  priority override persists; plan step-progress shape.
- `SpecificationService` gets standalone round-trip unit tests.

### Risks / notes

- **`depends-on: [TASK-042, TASK-034]` — both done**, so unblocked; owner-scoping seam already exists.
- The `/api/v1` group is only mapped when JWT is enabled (`Program.cs:1002`) — tests already drive this via
  `WebApplicationFactory` config (see `RunsEndpointsTests`); reuse that harness.
- Estimate ~1–1.5 days; step 1 carries the only real design work, 2–4 are mechanical given the pattern exists.
