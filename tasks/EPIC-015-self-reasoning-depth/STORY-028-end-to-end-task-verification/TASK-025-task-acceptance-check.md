---
id: TASK-025
parent: STORY-028
feature: FEATURE-031
status: done
priority: P1
assignee: agent
created: 2026-05-29
depends-on: [TASK-024]
blocks: [TASK-165]
pr: 6
github-issue: null
jira-key: null
---

# Post-plan task-acceptance check against acceptance criteria

> Picked 2026-11-17 on `task/TASK-025`, stacked on `task/TASK-024` (the EPIC-014 chain it needs is not merged yet).
> FEATURE-031 decided 2026-11-17: D1, D5 approved; D2, D3, D4 changed to the grill's settlements; D6 deferred (TASK-165).

## Context

A Kobold plan reaching terminal state (all steps Completed/Skipped/Failed) currently flows straight to Done. The only verification is per-step `StepValidationService` (`Kobold.cs:1614`), which checks "file exists + non-empty." Nothing checks the finished task against the acceptance criteria / target files / public API signatures Wyvern wrote into the task description. Add a whole-task acceptance check that runs once at plan completion and gates the Done transition. Mirror the fix-task creation pattern already used by `WyvernVerificationService`.

## Acceptance criteria

- [x] A task-acceptance check runs after a Kobold plan reaches terminal state, before the task is marked Done.
- [x] The check evaluates workspace output against the task's acceptance criteria / target files / declared API signatures (sourced from the task description / `analysis.json`).
- [x] Pass → Done. Fail → task not marked Done; a fix task (or reopen) is created listing the specific unmet criteria.
- [x] Non-machine-checkable criteria degrade to a `requiresResponse` human check (EPIC-014) rather than auto-passing. (Reworded 2026-11-17: no fallback, owner 2026-11-16 — without a decision store such a task fails rather than pass unchecked.)
- [x] Feature-flagged (e.g. `KoboldLair:Verification:TaskAcceptanceEnabled`, default false).
- [x] Distinct from and composable with EPIC-011's per-action evaluator (no duplication).
- [x] Test: a task whose steps "completed" but missed a required API signature is caught and not marked Done.

## Outcome

With `KoboldLair:Verification:TaskAcceptanceEnabled` on, Drake checks a finished task before Done is written: each
target file its description names exists and is not empty, each name in its Exports clause is declared there. A broken
promise reopens the task with what is missing (plan revised, finished steps kept) up to two times, then fails it with a
fix task. What no machine can judge parks it, work committed, on a review card: "Approve…" marks it Done, anything
else rejects it with that note. Orphan recovery takes the same check; older databases gain the attempts column. 22 new
tests; 455/455. Follow-ups: TASK-173.

## Out of scope

- Per-action evaluation — EPIC-011.
- Tech-stack build/test/lint verification — already handled by `WyvernVerificationService`; this is intent-level, not toolchain-level.
- Deferred to TASK-165 — an LLM judge for prose criteria ahead of the human check.

## Human test plan

- [x] Craft a task with a clear acceptance criterion the Kobold won't satisfy; confirm the acceptance check blocks Done and reopens it naming the unmet criterion, and at the third failure fails it with a fix task naming it (FEATURE-031 D3).
  - **Passed 2026-11-19:** scripted run against a real, throwaway server (`task/TASK-025` merged with `main`, Kestrel,
    Testing environment, scratch DB, JWT on, `TaskAcceptanceEnabled=true`, LAN `finstat` model). Project `waver2`: the
    spec asks for `greet` only and forbids any identifier `wave`; after Wyvern's analysis the task `impl-1` was crafted
    by hand to promise `Target files: waver.js` / `Exports: greet, wave`. (A first attempt put the contradiction in the
    spec itself; Sage caught it at approval and removed `wave`, so the check had nothing to fail.) The Kobold wrote
    `waver.js` with `greet` only. "Task f434d5e1 reopened by the acceptance check (attempt 1/2): Export wave is not
    declared in waver.js."; the resumed Kobold saw the conflict and asked a person through `ask_human` (answered "Follow
    the spec"); attempt 2 was recorded on the task; the third miss failed it with "acceptance check failed after 2
    reopen(s): Export wave is not declared in waver.js." and created the fix task "[accept-f434d5e1-…] Fix what the
    acceptance check found unmet in task f434d5e1: Export wave is not declared in waver.js." Found on the way: the
    reopen from a live run logged "No active plan to revise" (added to TASK-173); TASK-178 (verification fix tasks
    never run and are rewritten every cycle).
- [x] Confirm a genuinely complete task passes and is marked Done with the flag on.
  - **Passed 2026-11-17:** scripted run by the agent against a real, throwaway server (`task/TASK-025` `c3d2c8f`, Kestrel, Testing environment, scratch DB, `KoboldLair:Verification:TaskAcceptanceEnabled=true`, LAN `finstat` model). A greeter project went from approval to two Done tasks in under 5 minutes: "Acceptance check passed for task 29e67eab (1 checks)" (`backend-1`, which wrote `workspace/greeter.js` exporting `greet`), then committed. The other task logged "(0 checks)": its description named no target file, so nothing was machine-checkable (the gap TASK-165 covers). Found on the way: TASK-174, TASK-175, TASK-176

## Implementation plan

> Criterion 4 resolved 2026-11-16 (owner): no fallback. The task waits for EPIC-014, so the human bucket always
> routes to its answerable notification and blocks Done until answered. The "no-op if EPIC-014 isn't present" guard
> is no longer needed.

### Settled in the grill (owner, 2026-11-16)

- **Gate placement:** in Drake before the Done write, and the same check in orphan recovery.
- **On failure:** reopen in place up to `MaxAcceptanceReopens` (2), continuing through
  `KoboldPlannerAgent.RevisePlanAsync` with the unmet criteria, so finished steps are kept. At the cap: Failed plus a
  fix task.
- **Counter:** a dedicated `AcceptanceAttempts` column on the task. It is not `RetryCount`, which failure recovery
  uses for transient errors. No migration concern: nothing runs this code yet.
- **Inputs:** only what the task's own description says. Feature-level acceptance criteria are not checked per task.
- **Strictness:** a missing target file fails. A missing declared export fails only when its target file exists and
  the Exports clause parsed cleanly. Anything ambiguous goes to the human check: a clause that will not parse, or a
  name found only in another file.
- **The human check:** one card per task listing the criteria nothing could check, answered Approve or Reject with
  a note. A reject takes the same reopen path as a failed check.
- **While a card is open:** the task is in EPIC-014's awaiting-human-decision state (TASK-019). It is not Done, its
  dependents stay held, and nothing picks it up.
- **Commit timing:** the work is committed to the feature branch when the task enters the wait. A reject's fix
  lands as a further commit. The feature's "ready to merge" notification is held while any card on that branch is
  open.
- **Switch:** global `KoboldLair:Verification:TaskAcceptanceEnabled` only (default false), with no per-project
  override.
- **Events:** only the logging `TaskStatusChangedHandler` subscribes, so gating before the Done write delays
  nothing user-visible.

### Where Done happens today

- Kobold sets `KoboldStatus.Done` when every plan step is Completed/Skipped/Failed (`Kobold.cs` ~980, ~1186).
  `ValidateAndLogStepCompletionAsync` (~966; the `Kobold.cs:1614` reference in Context is stale) only logs.
- Drake turns it into `TaskStatus.Done` in `SyncTaskFromKoboldAsync` (`Drake.cs:1153`). `UpdateTaskWithWalAsync`
  (~1177) writes Done to the WAL and memory and publishes events and a circuit-breaker success **before** the
  side-effect block (~1277), where `RunPostTaskVerificationAsync` only logs and `CommitTaskCompletionAsync` may demote
  to Failed.
- Second Done path: orphan recovery (`Drake.cs` ~2778) marks a task Done because its plan is `Completed`, no checks.

**Gate:** in `SyncTaskFromKoboldAsync`, before the WAL write — when the transition is to Done and the Kobold
succeeded, run the check and rewrite the local status first. Orphan recovery calls the same helper. Gating at the
~1277 block is too late: Done has already reached the WAL and the event bus.

### Inputs as they exist

- `WyvernTask` / `TaskRecord` carry no structured criteria, files or API fields: it is prose in `TaskRecord.Task`,
  shaped by `WyvernAgent.cs:62-70` (target path(s), criteria verbatim, an "Exports: X class with m(a): T, …" clause).
- The only structured list is `Specification.Features[].AcceptanceCriteria` via `TaskRecord.FeatureId`
  (`Drake.GetFeatureFromSpecification`, `Drake.cs:3389`). It is feature-level and not used here (settled above).
- Also available: `TaskRecord.OutputFiles`; `Kobold.ExtractApiSignaturesFromContent` (`Kobold.cs:2272`, top-level
  exports only, so class members need their own match).

### Check design

1. **Deterministic** (on whenever the flag is on): path-like tokens in the description → file exists and is
   non-empty; the Exports/Public API clause → identifiers, each searched in the target files (fallback
   `OutputFiles`) by `\b(class|interface|function|def|enum|type)\s+Name\b` or `\bName\s*[(<]`. Fail and
   human-bucket rules as settled above.
2. **Prose criteria** (the description's "Must implement:" items and verbatim criteria): the human bucket. → deferred to TASK-165 (an
   optional LLM judge ahead of the human bucket).
3. **Human bucket:** one EPIC-014 answerable notification (`requiresResponse`) per task. The task waits in the
   awaiting-human-decision state. Approve → Done; Reject → the reopen path, with the note as an unmet criterion.

### Steps

1. Config: `KoboldLair:Verification:TaskAcceptanceEnabled` (false) and `MaxAcceptanceReopens` (2) in
   `appsettings.json`; a `Verification` section on `KoboldLairConfiguration`; passed from `DrakeFactory.cs:235` into
   an optional `Drake` constructor parameter. Add `AcceptanceAttempts` to `TaskRecord`, `TaskEntity` and
   `EntityMapper`.
2. Failing test first (below).
3. New `DraCode.KoboldLair/Services/TaskAcceptanceService.cs`:
   `Task<AcceptanceResult> EvaluateAsync(TaskRecord, string workspacePath, CancellationToken)`;
   `AcceptanceResult { Passed, Unmet (criterion + evidence), NeedsHuman, Checked }`. Stateless.
4. Drake: `ApplyAcceptanceGateAsync(kobold, task, worktreePath) → TaskStatus`, called before the WAL write in
   `SyncTaskFromKoboldAsync` and from orphan recovery (a failure there sets Unassigned, not Done).
5. On fail, mirroring `WyvernVerificationService.CreateFixTasksAsync`
   (`DraCode.KoboldLair.Server/Services/WyvernVerificationService.cs:409-476`: id `verify-{type}-{guid8}`, priority
   map, dedup, detail truncated to 200 chars) — but through Drake's live `TaskTracker.AddTask` rather than a separate
   markdown file, which would race with its reload:
   - below `MaxAcceptanceReopens`: reopen in place. Revise the plan with `RevisePlanAsync` from the unmet
     criteria, append "Acceptance check failed (attempt n): …" to the description, increment `AcceptanceAttempts`,
     and set the task Unassigned;
   - at the cap: Failed with an error `ErrorClassifier.IsPermanent` recognises (add the pattern, so
     `FailureRecoveryService` does not re-run it), plus a fix task `accept-{taskId8}-{guid8}` listing the unmet
     criteria (Critical when signatures are missing, else High), deduped on task id + unmet set.
6. Human bucket non-empty: commit the work (`CommitTaskCompletionAsync`), move the task to awaiting human decision,
   send the card, and hold the feature's "ready to merge" notification while any card on that branch is open. The
   answer resumes the gate: Approve → Done; Reject → step 5.
7. Events: `TaskStatusChangedEvent` with a reason; no `_circuitBreaker.RecordSuccess` on a failed check.
8. EPIC-011 boundary: its `EvaluatorAgent` (planned, no code) scores single actions inside the tool loop; this runs
   once per task in Drake. Nothing shared today.

### Tests (`DraCode.KoboldLair.Tests/Services`)

- `TaskAcceptanceServiceTests` (temp-directory pattern from `DrakeExecutionSelectionTests`). Red first: Wyvern's
  GOOD example (`src/services/AuthService.ts`, Exports login/refresh/validateToken) against a file with
  `export class AuthService { login(...) {} validateToken(...) {} }` → not passed, `Unmet` names `refresh`.
- Also: all present passes; a missing target file fails; a prose-only criterion lands in `NeedsHuman`, not `Unmet`.
- Gate test on a bare `TaskTracker`: a Done transition with a failed result leaves the task not Done, the reopen
  block names `refresh`, `AcceptanceAttempts` is 1, and no commit is made. At the cap the task is Failed and a fix
  task names `refresh`.
- Human bucket: a task whose only open criterion is prose waits in the awaiting state with its work committed. Its
  feature's ready-to-merge notification is not sent until the card is answered.

### Risks

- Text matching can wrongly fail renamed/overloaded members when Wyvern's clause is loose — the flag defaults off,
  and unparsable clauses go to the human bucket.
- Reopen loops are capped by `MaxAcceptanceReopens`. No Birko framework change.

### Refreshed 2026-11-17 (after TASK-020/022/024)

What changed under the plan, and what it changes in it:

- **The human check is an `Approval` decision.** `PendingDecisionSource.Approval` (value 2) is already reserved for
  this task, and `PendingDecisionNotification.ResponseSchemaFor` answers "any other decision" in free text "until its
  own parker (TASK-025) defines choices". The card's question JSON lists the criteria nothing could check; its schema
  offers `Approve` / `Reject`. It is parked with `Drake.ParkTaskAsync(task, decision, cancelRun: false)` (the run has
  ended), answered through TASK-024's `DecisionDesk` (`/dragon` `answer_decision`, Warden's `pending_decisions`).
- **The answer needs its own branch.** `Drake.ResolvePendingDecisionAsync` today unparks every non-escalation answer
  to Unassigned, which reruns the task. An `Approval` answer instead goes to `ApplyApprovalAsync`: Approve → Done (with
  the side effects the sync path runs for Done, its commit already made at parking); Reject → the reopen path with the
  note as the unmet criterion. The same method serves `ReconcileParkedTasksAsync` (~2640), for an answer given while
  no Drake held the task: today that unparks to Unassigned too.
- **The feature's "ready to merge" notice needs no code.** `UpdateFeatureStatus` (~2360) only reports a feature whose
  tasks are all Done; a task waiting on its card is `AwaitingHumanDecision`, so the notice waits by itself. A test
  pins it.
- **The resumed Kobold hears a reject.** TASK-022 delivers every resolved decision of the task as a tagged user
  message; an `Approval` falls in `HumanAnswer`'s default wording ("A person answered …"). It gets its own wording:
  "A person reviewed your finished work and rejected it: <note>".
- **Reopen reuses the `WrongApproach` route.** `HandleEscalationAsync` (~840) already revises a plan through
  `KoboldPlannerAgent.RevisePlanAsync`, keeping finished steps, for a live Kobold or a parked task's saved plan. The
  reopen builds a `WrongApproach` alert whose summary lists the unmet criteria and calls it with `AutoResolve`; the
  usual escalation notification tells the user. Then the task goes Unassigned with `AcceptanceAttempts + 1`.
- **`AcceptanceAttempts` needs a migration.** The plan's "no migration concern" was wrong: the `tasks` table exists in
  every database and `SqlTaskRepository.InitializeAsync` does not add columns. It calls
  `connector.EnsureColumns(typeof(TaskEntity))` as `SqlUsageRepository` does.
- **Line references moved:** the Done write is `SyncTaskFromKoboldAsync` ~1190 (`UpdateTaskWithWalAsync` ~1219, side
  effects ~1316); orphan recovery ~3249.

### Settled 2026-11-17 (owner)

1. A review card offers `Approve` / `Reject`. An answer starting "Approve" approves, anything after it kept as a remark
   that does not reopen the task; anything else rejects, the whole text being the note the reopened task gets (as its
   unmet criterion, and as the person's answer the resumed Kobold hears).
2. A review card never times out.
3. `AcceptanceAttempts` is a column; `SqlTaskRepository` adds it to an older table at startup (`EnsureColumns`).
4. A failed check reopens through the existing WrongApproach route, so the usual escalation notice tells the user.

### Implementation notes (2026-11-17)

- `TaskAcceptanceService` reads the description only: target paths (outside the Exports clause; "Node.js" and its
  kind are not files; a path with `..` is never read), the `Exports:` / `Public API:` clause split on top-level commas,
  and the `Must implement:` / `Acceptance criteria:` items. `Constraints:` are not sent to the review: they are the
  project's limits, shown to every Kobold, and would put every task in front of a person. A description that promises
  nothing checkable passes.
- An export counts as declared on `class|interface|function|def|enum|type|struct|record|const|let|var|val|fun|func|fn
  Name` or `Name` followed by `(`, `<`, `=` or `:`.
- The gate drops the task's Kobold from Drake's map whenever it does not pass, so the finished Kobold is not synced
  again into a second reopen.
- `ErrorClassifier` treats an error starting "acceptance check failed" as permanent before any pattern: its text quotes
  criteria and notes, which may contain a word it would read as transient.
- Without a decision store (non-SQLite backends), a task needing review fails rather than pass unchecked.
- A task recovered without a Kobold (orphan recovery) is not committed when it parks for review, as recovery never
  commits today; and its plan is revised only when the task is parked (the WrongApproach route loads a parked task's
  saved plan, otherwise needs a live Kobold).
- Not pinned by a test: the feature's "ready to merge" notice waiting for a reviewed task. It follows from
  `Wyvern.UpdateFeatureStatus` counting only Done tasks, which a test would need a Wyvern to show.
- A reopen appends "Acceptance check failed (attempt n): …" to the task, after its Exports clause, where a later check
  read it as more exports and failed the fixed task forever. The check now reads the description up to the first such
  note (`TaskAcceptanceService.ReopenNotePrefix`), found while chasing the flake below.
- Flaky once seen: `TaskAcceptanceGateTests.A_task_recovered_with_a_finished_plan_takes_the_same_check_rather_than_going_straight_to_done`
  ended Done instead of reopened in 2 runs alongside the other service tests (never alone), both before the note fix
  above, and passed in all 9 runs since (6 of them with logging, which showed the check failing the task as it should).
  Not explained: no Drake runs recovery in the background, and the note fix does not obviously bear on it.
- Close review (2026-11-17): a gated task skipped the sync path's unregister from the shared planning context, which
  would leave the finished Kobold holding its files "in use"; the gate now unregisters it. Loose ends → TASK-173.

Closed 2026-11-19: both human-test steps passed; suite 462/462 on this branch merged with `main`.
