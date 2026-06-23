---
id: TASK-044
parent: STORY-016
feature: FEATURE-018
status: review
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-042, TASK-037]
blocks: [TASK-045]
pr: null
github-issue: null
jira-key: null
---

# Runs endpoints (start + status)

## Context

`POST /api/v1/runs` starts an ad-hoc Kobold run and returns a `runId`; `GET /api/v1/runs/{id}` returns current status. The actual run uses the same Kobold machinery as STORY-015 and publishes to TASK-037's run-event source (which TASK-045's SSE then streams). One run engine, reachable from WS (`/kobold`) and REST.

## Acceptance criteria

- [x] `POST /api/v1/runs` accepts a run spec, starts the run, returns `{ runId }` immediately — `RunsEndpoints.MapRunEndpoints`: 202 Accepted + `Location: /api/v1/runs/{runId}` + `{ runId, mode, worktree }`; reuses the `/kobold` `IKoboldRunModeHandler` dispatch (adhoc + project). Bad/unknown mode → 400
- [x] `GET /api/v1/runs/{id}` returns status (pending/running/completed/failed) + summary — reads `RunRegistry`; `{ runId, mode, status, startedAt, completedAt, completedSteps, totalSteps, summary }`
- [x] Runs publish to the TASK-037 event source (consumed by TASK-045) — unchanged engine; `RunRegistry` subscribes per-run and folds the stream into queryable status that **outlives `CompleteRun`** (no-replay-safe), the same source TASK-045's SSE will stream
- [x] Ownership: a run is owned by its caller; scoping applies — owner = `ICurrentUser.UserId`; `GET` returns **404 (not 403)** for unknown OR not-owner-and-not-admin, so a run's existence isn't leaked
- [x] Tests: start → status transitions; unauthorized caller can't read another's run — `RunRegistryTests` (Pending→Running→Completed survives `CompleteRun`; RunError→Failed; `Fail`) + `RunsEndpointsTests` (401/202/400, own→200, other-caller→404, unknown→404); full suite 126/126 green

## Out of scope

- The SSE event stream (TASK-045)
- Ad-hoc git/worktree specifics already covered by STORY-015 machinery

## Human test plan

**Deferred — needs a live LLM-backed run (no non-interactive seam); task stays in `review` until run.** The protocol/registry/ownership are unit-tested with a stub handler (126/126); the live round-trip is manual:
- [ ] `curl -XPOST /api/v1/runs -d '{"mode":"adhoc","cwd":"...","prompt":"..."}'` (Bearer token) → get a `runId`; poll `GET /api/v1/runs/{id}` until `status: completed`

## Implementation plan

**Decisions (confirmed with user):** POST accepts **both** modes (reuse the `/kobold` `IKoboldRunModeHandler` dispatch — one run engine, two transports); status is backed by a **new `RunRegistry` singleton** that records `runId → {owner, mode, state, startedAt}` and attaches a persistent per-run subscriber to TASK-037's `KoboldRunEventSource` so status survives `CompleteRun` (events aren't replayed). The registry is also the natural backing for TASK-046 and lets TASK-045 SSE confirm a run exists.

### Grounding (verified against merged code)
- `ApiV1Endpoints.MapApiV1` returns the authed `RouteGroupBuilder`; `Program.cs:962` calls it inside `if (jwtRuntimeEnabled)`. The `/api/v1` group only exists when JWT is on → endpoints always have an authenticated `ICurrentUser` (no auth-off ambiguity).
- Caller identity in minimal-API: inject `Birko.Security.AspNetCore.ICurrentUser` (`UserId`, `Permissions`) — same as `whoami`. Admin = `Permissions` contains `*` or `ViewAll`.
- `KoboldRunRequest` already carries `Mode/Cwd/Prompt/AgentType/ProjectId/TaskId` → it **is** the POST body model (global camelCase-in is configured). Handlers resolved as `IEnumerable<IKoboldRunModeHandler>`, matched by `Mode` (case-insensitive), exactly like `KoboldEndpointService`.
- Subscribe-before-start: the registry must `Subscribe` before `handler.StartAsync` (mirrors the WS endpoint) or it misses early/terminal events.
- The Kobold emits a terminal `RunCompletedEvent`/`RunErrorEvent` then `CompleteRun(runId)` in its `finally` (TASK-037) → the registry's reader loop ends naturally with the terminal state recorded.

### Ordered steps
1. **`DraCode.KoboldLair/Services/RunRegistry.cs`** (new): `RunState {Pending,Running,Completed,Failed}`, `RunRecord {RunId, Owner, Mode, State, StartedAt, CompletedAt, CompletedSteps, TotalSteps, Summary}`. `Register(runId, owner, mode)` stores Pending + `Subscribe`s + background reader mapping events→state (`PlanStepUpdated/ToolCall/Reflect`→Running, `RunCompleted(Done)`→Completed, `RunCompleted(other)`/`RunError`→Failed). `Fail(runId, msg)` for a start that throws before any event (sets Failed + `CompleteRun` to release the reader). `Get(runId)`, `ListByOwner(owner)`. Bounded (`maxRuns`, evict oldest completed).
2. **DI** (`Program.cs`): `AddSingleton<RunRegistry>()`.
3. **`DraCode.KoboldLair.Server/Api/RunsEndpoints.cs`** (new): `MapRunEndpoints(this RouteGroupBuilder api)`:
   - `POST /runs` `(KoboldRunRequest, ICurrentUser, IEnumerable<IKoboldRunModeHandler>, RunRegistry)` → validate `mode`, mint `runId`, `registry.Register(runId, user.UserId, mode)`, `await handler.StartAsync(req, runId, caller, ct)`; on success `202 Accepted` + `Location: /api/v1/runs/{runId}` + `{ runId, mode, worktree }`; on `ArgumentException`/`InvalidOperationException` → `registry.Fail` + `400 { error }`. `.RequirePermission(ViewOwn)`.
   - `GET /runs/{id}` `(Guid id, ICurrentUser, RunRegistry)` → `Get`; **404 if missing OR not owner & not admin** (404 not 403 — don't leak existence); else `200 { runId, mode, status, startedAt, completedAt, completedSteps, totalSteps, summary }`. `.RequirePermission(ViewOwn)`.
4. **Wire** (`Program.cs:962`): `var api = app.MapApiV1(); api.MapRunEndpoints();`.
5. **WS parity** (`KoboldEndpointService`): inject `RunRegistry`, call `registry.Register(runId, caller.Sub, request.Mode)` right before `handler.StartAsync` so WS-started runs are queryable via `GET /runs/{id}` too (one engine). Protocol tests use the static `PumpAsync` — ctor change is safe.
6. **Tests**:
   - `RunRegistryTests` (drive the real `KoboldRunEventSource`): Pending→Running→Completed; RunError→Failed; `Fail` sets Failed + ends reader; `Get` null for unknown; owner listing.
   - `RunsEndpointsTests` (`WebApplicationFactory<Program>`, mirror `ApiV1SkeletonTests`, `MintToken` parametrized by `sub`): POST no-token→401; POST `{mode:"test"}` with a **stub `IKoboldRunModeHandler`** injected via `ConfigureTestServices`→202+`runId`; GET own run→200; GET as a different `sub`→404; GET unknown id→404. The stub publishes a terminal event through the injected event source (no git/LLM), keeping the test deterministic.

### Tradeoffs / risks
- **Registry is in-memory** (like `AdHocRunModeHandler._runs`); a restart loses run status. Acceptable for now — durable run history is a later concern. Bounded to avoid unbounded growth.
- **No-replay race:** an SSE/registry consumer attaching after a fast run misses events — inherent to the event source; the registry closes it for *status* by subscribing at start (before the run begins).
- **Endpoint tests avoid real runs** via a stub handler; the live adhoc/project paths are already covered by TASK-039/040 + their human test plans.

### Critical files
- **New** `DraCode.KoboldLair/Services/RunRegistry.cs`
- **New** `DraCode.KoboldLair.Server/Api/RunsEndpoints.cs`
- **New** `DraCode.KoboldLair.Tests/Api/RunsEndpointsTests.cs`, `DraCode.KoboldLair.Tests/Services/RunRegistryTests.cs`
- `DraCode.KoboldLair.Server/Program.cs` — `AddSingleton<RunRegistry>`, `api.MapRunEndpoints()`
- `DraCode.KoboldLair.Server/Services/KoboldEndpointService.cs` — register WS runs in the registry
