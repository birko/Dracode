---
id: TASK-034
parent: STORY-017
feature: FEATURE-019
status: review
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-032]
blocks: [TASK-043]
pr: null
github-issue: null
jira-key: null
---

# User entity, project ownership, and projects.json migration

## Context

Per-caller identity needs a DraCode `User` record (keyed by stable `sub`) and an `ownerId` on each project. Existing `projects.json` rows migrate to a system `ownerId = "legacy"` sentinel; future creates require a real owner. `ICurrentUser` (from TASK-032) supplies the caller identity for scoping.

## Acceptance criteria

- [x] `User` entity + `users` table (Birko.Data.SQL, same DB), keyed by stable `sub`; upserted on create from the caller's claims (best-effort name/email)
- [x] `projects` gain `ownerId` (string holding the raw `sub`; logical indexed reference) — model + persistence + view model + mapper
- [x] Greenfield ownership: every project create requires a real owner `sub`; creates inherit the `sub` of the human who started the Dragon session. **No legacy sentinel / no migration** — there is no pre-auth `projects.json` data to backfill
- [x] **Dragon/Wyvern WS identity (surgical legacy-auth switch):** `/dragon` and `/wyvern` validate via JWT bearer (`context.User`) instead of the legacy static-token validator, so the session carries a real `sub`. The session captures it and threads it into create calls. (Full teardown of `WebSocketAuthenticationService` + IP-binding config stays TASK-036.)
- [x] Per-caller scoping default — enforced **now** on the live Dragon surface: the Dragon project list/switcher shows only the session owner's projects; a caller with `*`/`ViewAll` (admin, incl. loopback dev) sees all. Repo seam (`GetAllForOwner` / unscoped `GetAll`) is the same surface TASK-043 reuses for REST
- [x] Local-dev loopback bypass principal (`sub = Guid.Empty`, scope `*`) owns its creates as `Guid.Empty` and sees all via the admin path — no special-casing
- [x] Tests: a create with no owner is rejected; `GetAllForOwner` filters by owner; admin/unscoped path returns all; `OwnerId` round-trips through the mapper (62/62 green)

## Out of scope

- The REST endpoints that surface scoping (TASK-043 consumes the repository seam)
- Service-account ownership semantics beyond `sub = "service:…"` (TASK-035)

## Human test plan

**Automated** (covered by `DragonWebSocketAuthTests` — the WS auth switch — plus `SqlProjectRepositoryTests`/`ProjectServiceOwnershipTests` for the scoping + ownership mechanism; 67/67 green):
- [x] `/dragon` with a valid JWT passes the upgrade gate; with no token / the old static token → rejected (401) — proves the legacy WS validator no longer gates Dragon
- [x] `/wyvern` with no token → rejected (401)
- [x] loopback bypass → upgrade allowed with no token
- [x] `GetAllForOwner` filters by owner; unscoped `GetAll` returns all; create with empty owner rejected; `OwnerId` round-trips + persists

**Deferred to TASK-056 (web login UI) — no non-LLM seam to drive project create/list over `/dragon` in a test, and no browser login flow exists yet. This task stays in `review` until then; the matching item is also listed on TASK-056's human test plan, and TASK-034 closes to `done` once it passes there:**
- [ ] End-to-end in the running app: log in as user A, create a project, confirm user B's project list omits it and an admin/`ViewAll` sees both; confirm loopback-dev creates are owned by `Guid.Empty` and see all. (The scoping *mechanism* is unit-tested above; this verifies it end-to-end through the real UI once login lands.)

## Implementation plan

> ✅ **Design decisions (confirmed with owner via grill, 2026-06-20):**
> 1. **`ownerId : string` holding the raw `sub` claim** (read via
>    `ICurrentUser.GetClaim(ClaimTypes.NameIdentifier)`), **not** `Guid? UserId`. The `sub`
>    is what arrives at create time; handles interactive Guid subs and future
>    `sub = "service:…"` (TASK-035) uniformly with no lookup. The "foreign key" is a logical
>    indexed reference — Birko.Data.SQL emits no FK constraints.
> 2. **Greenfield — no legacy sentinel, no migration backfill.** No pre-auth `projects.json`
>    data, so the "existing rows → `ownerId='legacy'`" migration is dropped entirely (also
>    removes the schema-column-backfill risk).
> 3. **Surgical legacy-auth switch folded in (grill outcome).** Dragon today has *no* per-user
>    identity — `/dragon` gates on the legacy static-token validator
>    (`WebSocketAuthenticationService`) and the handler discards `context.User`. Building a
>    `Guid.Empty`-fallback owner on a soon-deleted scheme is throwaway, and the project is
>    being rebuilt from scratch with **no legacy consumers**. So this task switches `/dragon`
>    + `/wyvern` to **JWT bearer** auth (real `sub` via the existing `MapAuthEndpoints` login —
>    *not* dependent on TASK-033/GitHub federation). The **full teardown** of the legacy
>    service + IP-binding config stays TASK-036 (its scope shrinks to "delete the dead code").
> 4. **Every create requires a real owner `sub`**, threaded from the Dragon session. All create
>    paths live in `DragonService` / the Seeker `AddExistingProjectTool` (both in-session) —
>    verified no headless project creation exists.
> 5. **Scoping bites now on the live Dragon surface.** The Dragon project list/switcher reads
>    `GetAllForOwner(session.sub)`; a session with `*`/`ViewAll` (admin, incl. loopback) reads
>    unscoped `GetAll()`. TASK-043 reuses the identical repo seam for REST.
> 6. **Loopback bypass owns as `Guid.Empty` and sees all** (its principal already carries `*`)
>    — no special-casing for local dev.

### What exists today (grounded)
- **Entity pattern** (`DraCode.KoboldLair/Data/`): `[Table("…")] class X : AbstractDatabaseLogModel` with `[RequiredField]`/`[MaxLengthField]`/`[IndexedField]`, Guid PK from base, scalar + `…Json` text columns, overridden `CopyTo`/`LoadFrom`. See `ProjectEntity.cs`, `CircuitBreakerEntity.cs`, `OAuthClientEntity` (Server `Auth/SqliteOAuthStores.cs`).
- **Mapping**: centralized static methods in `Data/EntityMapper.cs` (`ToEntity`/`ToProject`/`UpdateEntity`).
- **Repository**: `Data/Repositories/Sql/SqlProjectRepository.cs` wraps `AsyncSqLiteModelRepository<ProjectEntity>`; **all reads served from an in-memory `_cache` under a lock** → this is where scoping is applied. `IProjectRepository` defines `GetAll`/`GetById`/`GetByStatus`. JSON fallback: legacy `Services/ProjectRepository.cs` (must implement any interface change too).
- **Create flow**: `Services/ProjectService.cs` (`_repository.Add`, `new Project{…}` at ~189/~900) — a **singleton in `DraCode.KoboldLair`, no HTTP context** → `ICurrentUser` is not reachable here.
- **Migration**: `Data/Migrations/JsonToSqlMigration.cs` reads `projects.json` → `AddAsync`; wired once in `Program.cs:698-718`, guarded by a `dbPath + ".migrated"` marker.
- **`ICurrentUser`** (`Birko.Security.AspNetCore`): `Guid? UserId`, `GetClaim(type)`, `Permissions`, `Roles`; scoped, server-pipeline only. Admin gate for `?scope=all` = `Permissions.Contains(KoboldLairPermissionChecker.ViewAll)`.
- **Tests** (`DraCode.KoboldLair.Tests`): xUnit + FluentAssertions, `IAsyncLifetime`, real SQLite over temp file (`SqlProjectRepositoryTests`, `JsonToSqlMigrationTests`).

### Key decisions
1. `ownerId : string` keyed on raw `sub`. Logical indexed reference, no enforced FK.
2. **Scoping lives at the repository layer** as owner-parameterized methods on `IProjectRepository` (`GetAllForOwner(string ownerId)`; keep unscoped `GetAll()` for background services). This is the seam both the Dragon list (this task) and TASK-043's REST endpoints consume. Do **not** inject `ICurrentUser` into the `DraCode.KoboldLair` lib — the server-side caller (Dragon endpoint / future REST endpoint) resolves the `sub` + admin-ness from `context.User`/`ICurrentUser` and passes them down.
3. Admin = `ICurrentUser.Permissions` contains `ViewAll` (or `*`); admin/loopback reads unscoped `GetAll()`.
4. `User` row upserted on create per `sub`; minimal repo (upsert + get-by-sub).
5. **`GetAll()` stays unscoped** — background processors (`DrakeExecutionService`, `FailureRecoveryJob`, startup, metrics) enumerate every project regardless of owner; scoping is opt-in via the new method only.

### Ordered steps
**Data layer**
1. `User` model — `Models/Users/User.cs`: `Sub` (stable key), `DisplayName?`, `Email?`, `CreatedAt`.
2. `UserEntity` — `Data/Entities/UserEntity.cs`: `[Table("users")] : AbstractDatabaseLogModel`, indexed `Sub`, scalar cols, `CopyTo`/`LoadFrom` (mirror `CircuitBreakerEntity`).
3. `ProjectEntity.OwnerId` — `[MaxLengthField(256)][IndexedField("ix_projects_owner_id")] string OwnerId = ""`; add to `CopyTo`.
4. `Project.OwnerId` — add to `Models/Projects/Project.cs`.
5. `EntityMapper` — wire `OwnerId` through `ToEntity`/`ToProject`/`UpdateEntity`; add User mapping.
6. View model — add `OwnerId` to `Models/Projects/ProjectInfo.cs` + set it in the projection at `Server/Services/DragonService.cs:1781`.
7. `IUserRepository` + `SqlUserRepository` (`GetBySubAsync`/`UpsertAsync`) mirroring `SqlProjectRepository`; add `CreateUserRepositoryAsync` to `RepositoryFactory.cs`; register + `InitializeAsync` in `Program.cs` (~241, SQLite-vs-null pattern).
8. Scoping API on `IProjectRepository` — add `List<Project> GetAllForOwner(string ownerId)` (`_cache.Where(p => p.OwnerId == ownerId)`); implement in **both** SQL and legacy JSON repos. In-memory, no SQL change. Leave `GetAll()` unscoped.
9. Require owner on create — add required `ownerId` (`sub`) param to `ProjectService.RegisterProject` (line 164) + the second create path (`~900`); set `project.OwnerId`, reject empty. Upsert the `User` row by `sub` on create. (`CreateProjectFolder` only makes the folder — owner is set where the `Project` is persisted.)

**Auth + identity wiring (server)**
10. **Switch `/dragon` + `/wyvern` to JWT bearer** — in `Program.cs:939/946`, replace `MapWebSocket(..., requireAuthentication: true)` (legacy validator) with the JWT-authenticated mapping; read `context.User` in the handler and pass the `sub` (`NameIdentifier`) + `Permissions` into `HandleWebSocketAsync`. Existing `MapAuthEndpoints` login issues the JWT — no TASK-033 dependency.
11. Capture + thread the session owner — add `OwnerSub` (+ `IsAdmin`/permissions) to `DragonSession`; set from `context.User` at connect; pass to `ProjectService` create calls. Same for Wyrm/Wyvern create paths if any.
12. **Scope the Dragon list now** — point the `getAllProjects` reads (`DragonService.cs:555, 564, 630, 715`) at `GetAllForOwner(session.OwnerSub)`, except when the session is admin (`*`/`ViewAll`) → unscoped `GetAll()`.

**Tests**
13. `ProjectService`/repo create with empty owner is rejected; `SqlUserRepositoryTests` upsert/get-by-sub round-trip; `SqlProjectRepositoryTests` `GetAllForOwner` filters by owner + unscoped `GetAll` returns all; `EntityMapperTests` `OwnerId` round-trips.

### Risks
- **Client breakage on remote binds** — switching `/dragon` to JWT means the web client must send a JWT in `?token=` instead of the static token; the static-token path stops working remotely. Local dev is unaffected (loopback bypass). The client login UI is TASK-056; until then, remote Dragon needs a manually-supplied JWT. **Acceptable per "rebuilding from scratch, no legacy consumers."**
- **Greenfield schema** — `CreateSchemaAsync` creates `owner_id` + `users` from scratch; no backfill. Verify any pre-change dev DB is dropped/recreated rather than silently missing the column.
- **Interface ripple** — adding `GetAllForOwner` to `IProjectRepository` forces the legacy JSON repo (`Services/ProjectRepository.cs`) to implement it too.
- **Don't over-scope** — leaving `GetAll()` unscoped is deliberate; double-check no background processor accidentally gets switched to the owner-scoped method (would stall autonomous execution).

### Critical files
- `DraCode.KoboldLair/Models/Users/User.cs` (new) + `Data/Entities/UserEntity.cs` (new) + `ProjectEntity.cs` (`OwnerId`)
- `DraCode.KoboldLair/Data/EntityMapper.cs`; `Models/Projects/Project.cs` + `ProjectInfo.cs`
- `DraCode.KoboldLair/Data/Repositories/IProjectRepository.cs` + `Sql/SqlProjectRepository.cs` + `Services/ProjectRepository.cs` (the `GetAllForOwner` seam)
- `DraCode.KoboldLair/Data/Repositories/IUserRepository.cs` (new) + `Sql/SqlUserRepository.cs` (new) + `RepositoryFactory.cs`
- `DraCode.KoboldLair/Services/ProjectService.cs` (require owner on create)
- `DraCode.KoboldLair.Server/Program.cs` (`/dragon` + `/wyvern` → JWT; register `IUserRepository`) + `Server/Services/DragonService.cs` (`DragonSession.OwnerSub`, scope `getAllProjects` reads, set `OwnerId` in projection)
