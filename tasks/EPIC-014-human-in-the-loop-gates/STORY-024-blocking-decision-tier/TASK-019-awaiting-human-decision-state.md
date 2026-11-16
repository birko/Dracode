---
id: TASK-019
parent: STORY-024
feature: FEATURE-027
status: done
priority: P1
assignee: ai
created: 2026-05-29
depends-on: []
blocks: [TASK-020, TASK-167]
pr: 1
github-issue: null
jira-key: null
---

# AwaitingHumanDecision state + HumanDecisionRequired disposition

## Context

Today escalations always auto-resolve in `Drake.HandleEscalationAsync` (`DraCode.KoboldLair/Orchestrators/Drake.cs:765-916`). There is no state for "this task is parked waiting on a human." This task adds the data model: a new task state and a new escalation disposition, plus a structured pending-decision record that survives restart. It is the foundation TASK-020 builds the routing on.

Relevant existing types: `ExecutionState` enum (Running/Paused/Suspended/Cancelled), `TaskRecord` / `TaskEntity` / `TaskViewModel` / `EntityMapper`, `EscalationAlert` / `EscalationType` (`Models/Agents/ReflectionEntry.cs:22-29`), `ProjectNotificationService`.

## Acceptance criteria

- [x] New task status `AwaitingHumanDecision` added to `TaskStatus` and propagated through `TaskRecord` / `TaskEntity` / `EntityMapper` and every place that shows or counts task statuses; a DraCode-local `EscalationDisposition` (AutoResolve / HumanDecisionRequired) is stored on the pending decision. (Reworded 2026-11-16, owner: there is no task `ExecutionState` enum and no `TaskViewModel`; the framework escalation enums stay untouched.)
- [x] New `PendingDecision` record persisted (DB via the SQL repositories, consistent with plan/history persistence): question payload, escalation/source context, task id, created timestamp, optional timeout, resolution (null until answered).
- [x] `DrakeExecutionService` skips tasks in `AwaitingHumanDecision` when picking work.
- [x] `ReasoningMonitorService` does **not** flag an `AwaitingHumanDecision` task as stalled.
- [x] The timeout is per pending decision (FEATURE-027 D4, changed 2026-11-16): a parked task past its timeout falls back to the current auto-resolve path (no permanent wedge); a pending decision with no timeout never auto-resolves.
- [x] Migration/round-trip test: a parked task + its `PendingDecision` survive a server restart.

## Out of scope

- Drake's routing logic that *chooses* this disposition — TASK-020.
- The `ask_human` tool — TASK-021.
- Dragon UI / answering — TASK-024.
- Stopping a Kobold that is still running when its task is parked — TASK-020 (criterion added 2026-11-16).
- Deferred to TASK-167 — an index on `pending_decisions` and a retention limit for resolved decisions.

## Human test plan

- [x] Force a task into `AwaitingHumanDecision` (temporary test hook), restart the server, confirm the task is still parked and its pending decision is intact.
  - **Passed 2026-11-16:** automated — `PendingDecisionRoundTripTests.A_parked_task_and_its_pending_decision_survive_a_restart` (fresh repositories on the same database after dropping every connection) and `ParkedTaskDrakeTests.After_a_restart_the_stored_decisions_decide_which_tasks_are_parked` green
- [x] Confirm other tasks in the same project keep executing while one is parked.
  - **Passed 2026-11-16:** automated — `ParkedTaskDrakeTests.A_parked_task_and_its_dependent_are_not_offered_as_work_while_others_are` green
- [x] Let a parked task exceed its timeout and confirm it falls back to auto-resolve; park one with no timeout and confirm it stays parked.
  - **Passed 2026-11-16:** automated — `ParkedTaskDrakeTests.An_expired_decision_unparks_the_task_and_is_recorded_as_timed_out`, `A_decision_with_no_timeout_never_expires` and `An_area_with_a_parked_task_keeps_its_Drake_so_the_timeout_can_fire` green (the last proved red with its fix removed)

## Implementation plan

> Acceptance criteria question resolved 2026-11-16 (owner): the new value goes in `TaskStatus`, the disposition in a
> DraCode-local enum; criterion 1 reworded to match.

### Settled in the grill (owner, 2026-11-16)

- **Timeout sweep** runs inside `DrakeMonitoringService` (60s per Drake). While Drake is off for a project nothing
  there runs anyway; `TimeoutAt` is absolute, so decisions that expired meanwhile fall back on the first cycle after.
- **The pending decision is authoritative:** a reconcile pass at Drake start re-parks a task whose decision is
  unanswered and returns a parked task with no unanswered decision to `Unassigned`.
- **A Kobold still running at park** is stopped by TASK-020 (added to its criteria); this task only guards against
  a lingering Kobold overwriting the parked status.
- **No SQL backend** (JsonFile): parking is unavailable — `ParkTaskAsync` reports it cannot park and the caller keeps
  today's auto-resolve. No in-memory decisions.
- **A timed-out decision with no escalation** (an `ask_human` question or an approval card): the task returns to
  `Unassigned` and the decision is recorded TimedOut; telling the agent nobody answered is TASK-022's (added to its
  criteria).

### Established from the code

- **Enum:** `TaskStatus` (`DraCode.KoboldLair/Models/Tasks/TaskStatus.cs`). Append `AwaitingHumanDecision = 6` and pin
  explicit values on every existing member: it is stored as an int in `tasks.Status` (`EntityMapper` casts at 183,
  213, 244) and in the tracker JSON (no string-enum converter). Never renumber.
- **Disposition:** new `Models/Tasks/EscalationDisposition { AutoResolve = 0, HumanDecisionRequired = 1 }`, stored on
  the decision; TASK-020 chooses it. No framework change.
- **Persistence:** copy `SqlPlanRepository` — `AsyncSqLiteModelRepository<T>`, `PasswordSettings(dir, file)`,
  `InitializeAsync()` → `CreateSchemaAsync()`. There is no migration framework. (`CircuitBreakerEntity` exists but is
  not wired, so it is not the model.) DI singleton in `Program.cs` beside `SqlPlanRepository` (~381; null when not
  SQLite). `TaskEntity` needs no new column.
- **Picker:** `Drake.GetUnassignedTasks` (Drake.cs:2888) already takes only `Unassigned || BlockedByFailure`, and
  dependency gating counts only `Done`, so dependents wait unchanged — pinned by a test.

### Steps

1. **Enum and display.** Add the value, then teach every status switch:
   - `TaskTracker.GenerateMarkdown` (267) and its summary counts;
   - `TaskTracker.ParseStatus` (651) — it maps unknown words to `Unassigned`, so a parked task loaded from markdown
     would silently re-run (it also lacks `failed` / `blockedbyfailure` today);
   - `ViewTaskDetailsTool` (filter 110, counts, icon 437), `ProjectProgressTool` counts;
   - `Drake.GetStatistics` (2518): an awaiting count on `DrakeStatistics`.
2. **Guard every path that could un-park a task:**
   - `Drake.SyncTaskFromKoboldAsync` (1153): return early for a parked task, or `MonitorTasksAsync` overwrites it
     from a lingering Kobold's Working/Done (TASK-025 parks with its Kobold already Done);
   - `BatchTaskTool` reassign (221–235): refuse a parked task (it resets anything not Done/Working/Failed);
   - `RecoverOrphanedTasksAsync`, `FailureRecoveryService` / `FailureRecoveryJob` and the REST retry touch only
     NotInitialized/Working/Failed — unchanged, pinned by tests.
3. **Model:** `Models/Tasks/PendingDecision.cs` — `Id`, `ProjectId`, `TaskId`, `Disposition`, `Source`
   (Escalation / AskHuman / Approval), `Question` (payload), `Escalation` (`EscalationAlert?`), `CreatedAt`,
   `Timeout` (`TimeSpan?`), `TimeoutAt` (derived), `Resolution` (string?), `ResolutionKind` (Answered / TimedOut,
   nullable), `ResolvedAt`.
4. **Entity + repository:** `Data/Entities/PendingDecisionEntity.cs` (`[Table("pending_decisions")]`,
   `AbstractDatabaseLogModel`, `CopyTo`), with `TimeoutAt` as an absolute nullable column (the sweep is one predicate),
   `EscalationJson` and `QuestionJson`. `Data/Repositories/Sql/SqlPendingDecisionRepository.cs`: `CreateAsync`,
   `GetUnresolvedForProjectAsync`, `GetUnresolvedForTaskAsync`, `GetExpiredAsync(now)`,
   `ResolveAsync(id, kind, text, at)` — succeeds only while `Resolution` is null (idempotent).
5. **Wiring:** register in `Program.cs`; pass through `DrakeFactory` (ctor ~68, `new Drake(` ~235) into `Drake` as an
   optional dependency.
6. **Drake API** (plumbing TASK-020/021/025 call):
   - `ParkTaskAsync(task, PendingDecision) → bool` — false, changing nothing, when no decision repository is
     registered (JsonFile backend); otherwise persist the decision first (source of truth), then set the status,
     drop the `_taskToKoboldMap` entry, `SaveTasksToFile`;
   - `IsTaskParked(taskId)`;
   - `ReconcilePendingDecisionsAsync()` on Drake start / first monitor cycle: an unresolved decision whose task is
     not parked → re-park; a parked task with no unresolved decision → `Unassigned`. A crash between the two writes
     cannot wedge a task.
7. **Timeout sweep:** `Drake.ExpirePendingDecisionsAsync(DateTime now)`, called from
   `DrakeMonitoringService.MonitorSingleDrakeAsync` (already every 60s per Drake, and the fallback needs a Drake).
   Per expired decision: `ResolveAsync(TimedOut)`; task → `Unassigned` (resumes from its saved plan); when it carries
   an escalation, deserialize it, reset `Status = Pending`, `await HandleEscalationAsync(alert)`. A null timeout is
   never selected. A parked area keeps its Drake alive (`IsAreaFullyBlocked` is false with no Unassigned tasks;
   `IsAreaComplete` is false).
   - Required tweak: the `WrongApproach` branch (Drake.cs:808) needs a live Kobold, which a parked task no longer has
     ("No active plan to revise"). Fall back to `_planService.LoadPlanAsync(projectId, taskId)` when it is null.
8. **Monitors:** `ReasoningMonitorService.CheckKoboldAsync` skips when the project's Drake reports
   `IsTaskParked(kobold.TaskId)`; `Drake.HandleStuckKoboldsAsync` (2368) skips parked tasks, so a lingering Kobold is
   not timed out and failed.

### Tests (failing first)

- **`Data/PendingDecisionRoundTripTests` (red first):** temp SQLite file (pattern: `SqlTaskRepositoryTests`,
  `LegacyPlanLoadTests`). Add a parked task through `SqlTaskRepository`, create a decision with a timeout and an
  escalation, `SqliteConnection.ClearAllPools()`, build fresh repositories on the same path (the restart) and
  `InitializeAsync` again (idempotent schema), assert the status, every decision field and `Resolution == null`. Also
  against a DB created before the table existed (`CreateSchemaAsync` adds it).
- **`TaskTracker`:** JSON round trip; markdown `ParseStatus` of the parked word (red today: returns `Unassigned`).
- **`EntityMapperTests`:** status 6 both ways.
- **Drake** (harness of `ProjectRunModeDrakeReleaseTests`): `GetUnassignedTasks` excludes the parked task and its
  dependent; a Done Kobold does not overwrite the parked status; `ParkTaskAsync` without a repository returns false
  and leaves the task as it was; reconcile re-parks and un-parks as settled above; a past `TimeoutAt` unparks and records TimedOut; no
  timeout stays parked at `now + 1y`.

### Tradeoffs

- The sweep in `DrakeMonitoringService` reuses its interval and Drake instance; nothing expires while Drake is
  disabled for a project (TASK-095). A standalone service would have to rebuild Drakes.
- Question and alert as JSON blobs (like `PlanDataJson`) keep the schema stable for TASK-021/025 payloads; they are
  not queryable, and need not be.
- `TimeoutAt` stored absolutely keeps the sweep restart-safe.

### Risks

- `TaskTracker.SyncToRepository` is fire-and-forget, so the tracker JSON stays a second source of status; the
  reconciliation in step 6 is what makes `PendingDecision` authoritative.
- An older build reading status 6 fails the cast or treats it as unknown — one-way change.
- `HandleEscalationAsync` re-runs after a long delay with possibly stale context — acceptable for a fallback.
- Stopping a still-running Kobold on park is not covered here, only the guards: TASK-020 must cancel the run.

## Outcome

Closed 2026-11-16. Suite 358/358 (335 before; 23 new tests across `PendingDecisionRoundTripTests`,
`ParkedTaskTrackerTests`, `ParkedTaskDrakeTests` and `EntityMapperTests`).

Review gate (2026-11-16) — one verdict per pass:

- **Standards:** two findings, both fixed. CLAUDE.md now records parked tasks and the stored-enum numbering rule
  (register-on-introduce), and a `using` was out of order. Kept: the concrete `SqlPendingDecisionRepository`, which
  follows the `SqlPlanRepository` precedent.
- **Intent:** every criterion met by the diff; nothing outside the plan.
- **Correctness:** two blockers, both fixed with red-first tests. One area's Drake could expire another area's
  decision. Resolving was read-then-write, so a timeout and an answer could both win; it now takes one lock per
  database file. Also fixed: the fallback runs before the task is unparked; reconcile survives a database error and
  never re-parks Done or Failed tasks; the `WrongApproach` saved-plan fallback applies to parked tasks only; times
  are stored as UTC; a parked task cannot be parked twice.
- **Security:** one finding, fixed. A decision is stored under the Drake's own project, whatever the caller passed.
  Unbounded growth of the table is filed as TASK-167. No injection (expression predicates) and nothing exposed.
- **Comments:** false or duplicated comments corrected or removed. The rest hold content that lives nowhere else.
- **Found in passing:** `TaskTracker.ParseStatus` reads `failed` and `blockedbyfailure` as Unassigned — TASK-166
  (FIELD-021).
- **Known test gap:** the expiry tests run without a Wyvern or planner, so they show the fallback is invoked
  but not what it resolves. Its routing is today's `HandleEscalationAsync`, unchanged here apart from the
  parked-only plan fallback.
