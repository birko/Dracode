---
id: TASK-040
parent: STORY-015
feature: FEATURE-017
status: review
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-038]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# /kobold project mode

## Context

Project mode runs a Kobold against an already-analyzed project task: `{ mode: "project", projectId, taskId }`. Loads the existing plan + worktree from project state (reuses Drake's worktree machinery), streams identically to ad-hoc, and on completion commits to the project's feature branch (existing Drake behaviour).

## Acceptance criteria

- [x] Loads the task's existing plan + feature-branch worktree — reuses `Drake.ExecuteTaskAsync` (public, Drake.cs), which performs `SetupFeatureBranchWorktreeAsync` → plan load → execute
- [x] Spawns Kobold against that plan; streams via TASK-038 — `ProjectRunModeHandler` runs `ExecuteTaskAsync(runId:)` in the background; the Kobold adopts the endpoint's `runId` (new `Kobold.AssignRunId`) and publishes to `KoboldRunEventSource`, which the `/kobold` pump relays
- [x] On completion, commits to the feature branch and cleans up the worktree — the existing `ExecuteTaskAsync` tail (`SyncTaskFromKoboldAsync` → `CommitTaskCompletionAsync` → `CleanupWorktreeAsync`); unchanged
- [x] Respects per-project parallel Kobold limits and execution state — `EnsureRunnable` rejects non-`Running` projects (Paused/Suspended/Cancelled → `error` frame); the per-project parallel-Kobold limit is enforced in `SummonKoboldAsync` (null → relayed as a terminal `error` frame so the pump ends)
- [~] Tests: project-mode run on a seeded analyzed project commits to the right branch — the **commit-to-branch end-to-end needs a live Kobold** (no fake LLM provider), so it lives in the human test plan; the validation gate (5 cases incl. execution-state) + reuse/runId wiring are unit-tested (`ProjectRunModeHandlerTests`; full suite 97/97 green)

## Out of scope

- Ad-hoc mechanics (TASK-039)
- Changing Drake's background execution loop

## Human test plan

**Now runnable end-to-end (also satisfies TASK-038's deferred live step).** Needs a live LLM-backed Kobold, so it's a manual run. _Provider name→type resolution for DB-backed providers was fixed in `Drake.SummonKoboldAsync` (2026-06-25, found via TASK-039's live run) — the path no longer passes the provider name to the factory, so a live project run can now resolve `pi-zai`→`zai` etc._
- [ ] Run project mode against a seeded analyzed project/task (`websocat` to `/kobold`, payload `{ "mode": "project", "projectId": "...", "taskId": "..." }`) → observe the ordered `kobold_*` stream to `kobold_complete`, confirm the commit lands on the task's feature branch and the worktree is cleaned up

## Implementation plan

**Reuse, don't reinvent:** `Drake.ExecuteTaskAsync` (public, Drake.cs:1800) already does the full per-task flow (worktree → summon → plan → execute → sync/commit → cleanup). Project mode is a thin on-demand caller of it.

**RunId injection (reconciles with TASK-037):** TASK-037 had the Kobold mint its own `RunId` inside `StartWorking*`. Added `Kobold.AssignRunId(Guid)` + `ConsumeAssignedRunId()` (`RunId = _assignedRunId ?? Guid.NewGuid()`), and an optional `Guid? runId` param on `ExecuteTaskAsync` that calls `AssignRunId` right after summon. This preserves TASK-038's subscribe-before-start contract (endpoint owns the id, subscribes, then starts) with no drop-before-subscribe race.

**Steps (done):**
1. `Kobold.AssignRunId` / `ConsumeAssignedRunId` (core seam); both `StartWorkingWithPlan*` now consume it.
2. `Drake.ExecuteTaskAsync` gains optional `runId`, assigns it to the summoned Kobold.
3. `ProjectRunModeHandler`: `EnsureRunnable` (ids + execution-state gate) → `LocateTaskFile` (TaskTracker scan of `project.Paths.TaskFiles`) → `DrakeFactory.CreateDrake` for that area → resolve `(task, agentType)` via `GetUnassignedTasks` → background `ExecuteTaskAsync(runId:)`; kobold-limit/exception → terminal `RunErrorEvent` so the pump ends.
4. DI: `ProjectRunModeHandler` now resolves `IProjectRepository` + `DrakeFactory` + `KoboldRunEventSource` (all singletons; registration unchanged).
5. `ProjectRunModeHandlerTests` (7) — validation gate incl. Paused/Suspended/Cancelled.

**Out of scope kept:** ad-hoc mechanics (TASK-039); no change to Drake's background loop.
