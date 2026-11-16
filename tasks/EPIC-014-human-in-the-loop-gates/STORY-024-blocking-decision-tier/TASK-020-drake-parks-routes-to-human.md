---
id: TASK-020
parent: STORY-024
feature: FEATURE-027
status: verify
priority: P1
assignee: agent
created: 2026-05-29
depends-on: [TASK-019]
blocks: [TASK-021]
pr: 2
github-issue: null
jira-key: null
---

# Drake parks task & routes decision to human instead of auto-resolving

## Context

With the `AwaitingHumanDecision` state and `PendingDecision` record in place (TASK-019), wire `Drake.HandleEscalationAsync` (`Drake.cs:765-916`) so that when the `HumanDecisionRequired` disposition is selected it parks the task and emits the pending decision, instead of calling `RevisePlanAsync` / `RefineTaskAsync` / reset. The *choice* of when to use this disposition comes from policy (TASK-023); this task implements the branch itself with a hard-coded/test trigger until the policy lands.

## Acceptance criteria

- [x] `HumanDecisionRequired` branch in `HandleEscalationAsync`: sets task `AwaitingHumanDecision`, writes a `PendingDecision`, emits a `requiresResponse` notification (schema-only here; Dragon rendering is TASK-024).
- [x] Drake does not block — it returns and continues processing other tasks (preserve the existing fire-and-forget `Task.Run` pattern).
- [x] On resolution callback, the task transitions back to runnable and resumes from its saved plan/conversation checkpoint.
- [x] Resolution payload is recorded so STORY-029 (loop closure) can feed it back to the Kobold.
- [x] Falls through to the existing auto-resolve routes for every disposition that is not `HumanDecisionRequired` (zero behaviour change by default).
- [x] Unit/integration test: an escalation tagged `HumanDecisionRequired` parks; a simulated answer resumes the task.
- [x] Parking stops the task's Kobold if one is still running, so no run continues on a parked task (from the TASK-019 grill, 2026-11-16).

## Out of scope

- The policy that decides when to pick this disposition — TASK-023.
- Async `ask_human` agent tool — TASK-021.
- Deferred to TASK-168 — the worktree cleanup that destroys uncommitted work on server shutdown.
- Deferred to TASK-169 — reasoning-monitor escalations reach the project's first Drake rather than the one holding
  the task, so in a multi-area project they miss the human route (and the automatic ones).
- A run on the non-plan path (planning disabled) is not cancelled by a park: `StartWorkingAsync` takes no token.
  Decided not to change: that path gets no `reflect` tool and the reasoning monitor skips Kobolds without a plan, so
  no escalation can park its task. TASK-021 adds a parker; revisit there if `ask_human` reaches that path.

## Human test plan

- [x] Trigger an escalation flagged for human decision, confirm the task parks and a pending decision appears (via repository/notification inspection).
  - **Passed 2026-11-16:** automated — `EscalationToHumanTests.An_escalation_chosen_for_a_person_parks_the_task_and_raises_a_decision_card` green (the stored decision and the card's metadata are asserted; proved red with the branch disabled)
- [ ] Submit a resolution through the resolution callback and confirm the task resumes and completes.
  - Runs once TASK-024 gives a person a way to answer (Dragon), against a local server with a provider set up: one
    end-to-end session for the EPIC-014 chain. The resume half is automated
    (`An_answer_resumes_the_task_and_is_recorded_once`); "completes" needs a real Kobold run.

## Implementation plan

### Settled in the grill (owner, 2026-11-16)

- **Policy seam:** `IEscalationDispositionPolicy`, default `AutoResolve`, injected into Drake; an explicit disposition
  argument overrides it; the timeout sweep always passes `AutoResolve`.
- **Card lifecycle:** a `decision_required` project notification carries the metadata keys; when the decision is
  answered or times out, Drake marks that notification read and pushes `decision_resolved` with the `decisionId`, so
  no stale card stays answerable.
- **Every park stops the task's running Kobold** (per-task cancellation token, cancelled after the park is written).
- **Answering with no live Drake** resolves in the database; reconcile unparks on the Drake's next start.
- **An escalation's answer:** the choices are "Handle it automatically" (runs today's auto-resolve route, exactly as a
  timeout) and "Resume with my instructions", plus free text. Anything but "Handle it automatically" unparks the task
  with the answer recorded for TASK-022.

### Established from the code

- **How the disposition arrives (no policy yet).** An optional seam TASK-023 fills without touching callers:
  `IEscalationDispositionPolicy { EscalationRoute Choose(EscalationAlert alert, TaskRecord? task); }` and
  `record EscalationRoute(EscalationDisposition Disposition, TimeSpan? Timeout = null)` in `Models/Tasks`. `Drake`
  takes it as an optional constructor argument; `DrakeFactory` gets a `DispositionPolicy` property, wired like
  `OnEscalation`; the default always answers `AutoResolve`. `HandleEscalationAsync(EscalationAlert alert,
  EscalationDisposition? disposition = null)` asks the policy when none is given, so both callers — the Kobold
  `OnEscalation` handler inside `Task.Run` (Drake.cs:766) and `ReasoningMonitorService.RouteEscalationWithRetryAsync`
  — stay unchanged. Tests inject a stub policy. `ExpirePendingDecisionsAsync` passes `AutoResolve` explicitly, or a
  policy could re-park the task every time its decision times out.
- **Timeout for an escalation park:** `EscalationRoute.Timeout`, set by whatever parks (D4). No config here —
  TASK-023 owns the timeout value and puts it on the route. Null waits until answered; nothing in production chooses
  this disposition before TASK-023.
- **Today's checkpoint is Unassigned.** `DrakeExecutionService` picks the task up; `ExecuteTaskAsync` →
  `EnsurePlanAsync` reloads the saved plan (unless `Failed`) and resumes at `CurrentStepIndex`. The enhanced path's
  `LoadResumableCheckpointAsync` also restores the conversation (checkpoints every `CheckpointInterval` and on
  cancellation); the basic path (`StartWorkingWithPlanAsync`) saves only the plan on cancellation.
- **Stopping a Kobold:** no per-Kobold cancellation exists; the only token is the shutdown token, passed through
  `ExecuteTaskAsync` to `Agent.RunAsync`. Both plan paths already treat cancellation as an orderly stop
  (`catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)`): save the plan (and the
  conversation on the enhanced path), stay `Working`, return; `ExecuteTaskAsync`'s `finally` then unsummons (map and
  factory removal only). `SyncTaskFromKoboldAsync` already ignores a parked task. The non-plan path
  (`StartWorkingAsync`) has no token, but `ReflectionTool` exists only on plan paths, so a Kobold-raised escalation
  always comes from a cancellable run. Whether Birko's provider aborts the in-flight HTTP call is unverified.
- **Work-loss bug in scope:** after the final sync, `ExecuteTaskAsync` calls `CleanupWorktreeAsync`
  (`git worktree remove --force`), which would destroy a parked task's uncommitted worktree. `CreateWorktreeAsync`
  reuses an existing worktree, so keeping it suffices. (The same cleanup also loses work on server shutdown today —
  outside this task, → deferred to TASK-168.)

### Steps

1. **Repository:** `GetByIdAsync(id)` and `GetResolvedForTaskAsync(taskId)` (newest first — how TASK-022 / STORY-029
   read the answer back).
2. **Per-task run cancellation in Drake:** `ConcurrentDictionary<string, CancellationTokenSource> _runCancellations`.
   `ExecuteTaskAsync` creates a source linked to the caller's token, registers it under `task.Id`, passes its token
   to every run path (`ExecuteTaskParallelAsync` included); `finally` removes only its own source
   (`TryRemove(KeyValuePair)`) and disposes it. Skip `CleanupWorktreeAsync` while the task is
   `AwaitingHumanDecision`, as it is already skipped for `CommitFailed`.
3. **`ParkTaskAsync` additions** (serve TASK-021 and TASK-025 too): after the status write, cancel
   `_runCancellations[task.Id]` if present — park first, then cancel, so the sync guard catches a Kobold finishing at
   that moment — then raise a new `_onDecisionRequired(projectName, decision)` callback. Re-parks by reconcile do not
   raise it.
4. **Notification:** `DrakeFactory.OnDecisionRequired`, wired in `Program.cs` beside `OnEscalation`, calls
   `notificationService.Notify(projectName, "decision_required", decision.Question, metadata)`. Metadata keys live in
   one shared static class `PendingDecisionNotification` (`Models/Tasks`) for TASK-024/021/025: `requiresResponse`
   (`"true"`), `decisionId`, `taskId`, `source`, `disposition`, `question`, `escalationType`, `timeoutAt` (ISO-8601 UTC
   or empty), `responseSchema` (JSON, e.g. `{"kind":"choice","options":[...],"allowFreeText":true}`; an escalation's
   options come from its type). TASK-024 makes these typed fields (its own criterion); delivery is the existing
   `OnNotification` → `project_notification` push. On resolution (answer or timeout) a `_onDecisionResolved` callback
   (`DrakeFactory.OnDecisionResolved`) marks the notification read and pushes `decision_resolved` with `decisionId`.
5. **The branch in `HandleEscalationAsync`**, before the `switch`: resolve the route. On `HumanDecisionRequired`:
   - task not in this Drake's tracker (the monitor can route to another area's Drake), or `Done`/`Failed` (the Kobold
     won the race) → today's path;
   - already parked → log, return with resolution "already awaiting a human decision" — never auto-resolve it (a Wyvern
     refinement could fail the parked task);
   - otherwise build a `PendingDecision` (`Source=Escalation`, `Escalation=alert`, `Question` from the summary and
     type, `Timeout=route.Timeout`) and `ParkTaskAsync`. True → leave `alert.Status = InProgress`, persist, return
     without the generic escalation notification (the decision card replaces it). False (no SQL store) → today's path.
   Nothing awaits an answer, so the `Task.Run` caller stays fire-and-forget.
6. **`ResolvePendingDecisionAsync(string decisionId, string answer) → bool`:** load the decision; false when unknown,
   another project's, or its task is not in this tracker; `ResolveAsync(Answered, answer, UtcNow)` — false when a
   timeout or another answer won; mark the escalation resolved ("Human: …"); append a plan log entry
   ("Human decision: …") via `_planService.LoadPlanAsync` / `SavePlanDebouncedAsync`; when the answer is "Handle it
   automatically" and the decision carries an escalation, run `HandleEscalationAsync(alert, AutoResolve)` while still
   parked (as the timeout does); then if still parked → `Unassigned` (WAL) + `SaveTasksToFile`; raise
   `_onDecisionResolved`; true. `ExpirePendingDecisionsAsync` raises `_onDecisionResolved` too. Plus `DrakeFactory.ResolvePendingDecisionAsync(projectId, decisionId, answer)`
   trying each of the project's Drakes; with none alive, resolve in the repository and let reconcile unpark on the
   next start.

### Tests (failing first, in `ParkedTaskDrakeTests`)

- Stub policy → `HumanDecisionRequired`: `HandleEscalationAsync` returns while the decision is unanswered; the task is
  parked; the stored decision carries the alert and timeout; the notification callback received the metadata keys.
- Default policy: every `EscalationType` routes as today (`WrongAgentType` resets to `Unassigned`); no decision written.
- An explicit `AutoResolve` argument overrides the stub; expiry never re-parks.
- An escalation for a task already parked: no second decision, status unchanged.
- No store: falls through to auto-resolve.
- Simulated answer: `ResolvePendingDecisionAsync` true; the task `Unassigned` and offered by `GetUnassignedTasks`;
  `GetResolvedForTaskAsync` returns the answer; a second call false; an answer after a timeout false; the resolved
  callback fired for the answer and for a timeout.
- "Handle it automatically": runs the escalation's auto-resolve route (`WrongAgentType` → reset) and unparks.
- Kobold stop: register a run through an internal `BeginRun(taskId, token)` seam; after `ParkTaskAsync` its token is
  cancelled; a Done Kobold synced afterwards leaves the task parked.

### Tradeoffs

- Notification metadata rather than typed fields keeps this task schema-only and leaves TASK-024's criterion to it,
  at the cost of stringly-typed keys (in one shared class).
- Cancelling inside `ParkTaskAsync` makes every kind of park stop its run, which TASK-021's async `ask_human` wants.

### Risks

- A provider that ignores the token lets one LLM call finish; the sync guard drops its result.
- A basic-path resume loses the conversation (plan only).
- The parallel path's cancellation is untested.

## Outcome

Code complete 2026-11-16; parked at `verify` until the second human-test step runs (at TASK-024). Suite 373/373 (358
before; 15 new in `EscalationToHumanTests`), red first, with mutations proving the human branch and the run cancel.

Review gate (2026-11-16) — one verdict per pass:

- **Standards:** CLAUDE.md now records the escalation-to-person route and the decision-card contract (fixed); a
  misplaced XML doc in `DrakeFactory` fixed. `GetResolvedForTaskAsync` is read only by tests until TASK-022: kept, it
  is the answer's read-back the criteria ask for.
- **Intent:** every criterion met. Beyond them, all settled in the grill or needed so parking loses no work: the
  `decision_resolved` notice, "Handle it automatically", the factory's no-Drake path, keeping a parked task's worktree.
- **Correctness:** no blockers. Fixed, red-first: two escalations at once could park one task twice (parking is now
  serialised and the loser absorbed); a parked task ran an automatic route for a late escalation under the default
  policy (now absorbed unless a disposition is explicit); "Handle it automatically" answered with no Drake running was
  lost (reconcile now runs the route, and the factory retires the card); the timeout test could not tell an
  automatic route from a bare unpark (now asserts it). The answer's plan-log entry is saved immediately, so a
  cancelled Kobold's shutdown save cannot overwrite it. Filed: TASK-169 (monitor escalations reach the wrong Drake).
  Noted, not changed: the parallel path logs a cancelled batch as a failure (the task is not failed); the resume
  prompt calls a park a "server restart"; a parked Kobold is not unregistered from the shared planning context.
- **Security:** no endpoint yet; project and area ownership hold. TASK-024's handler must check the session owns the
  project, and render the card's LLM-written question escaped.
- **Comments:** stale and misplaced comments fixed; the rest hold content that lives nowhere else.
- **Known test gap:** run cancellation is proved through `BeginRun` directly, not through a real `ExecuteTaskAsync`
  run; the end-to-end session at TASK-024 covers it.
