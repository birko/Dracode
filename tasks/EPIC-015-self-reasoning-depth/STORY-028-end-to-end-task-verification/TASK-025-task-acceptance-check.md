---
id: TASK-025
parent: STORY-028
feature: FEATURE-031
status: todo
priority: P1
assignee: ai
created: 2026-05-29
depends-on: [TASK-024]
blocks: [TASK-165]
pr: null
github-issue: null
jira-key: null
---

# Post-plan task-acceptance check against acceptance criteria

> Waits on EPIC-014 (decided by the owner 2026-11-16). Criterion 4 needs a human channel: prose criteria nobody can
> judge go to a human check that blocks Done, and that channel is EPIC-014's answerable notifications (TASK-024, the
> end of the TASK-019 → 020 → 022 → 024 chain). The plan below was drafted at the 2026-11-16 pick, which was unpicked.
> FEATURE-031's decisions are still `proposed`: run `/feature decide FEATURE-031` before picking this again.

## Context

A Kobold plan reaching terminal state (all steps Completed/Skipped/Failed) currently flows straight to Done. The only verification is per-step `StepValidationService` (`Kobold.cs:1614`), which checks "file exists + non-empty." Nothing checks the finished task against the acceptance criteria / target files / public API signatures Wyvern wrote into the task description. Add a whole-task acceptance check that runs once at plan completion and gates the Done transition. Mirror the fix-task creation pattern already used by `WyvernVerificationService`.

## Acceptance criteria

- [ ] A task-acceptance check runs after a Kobold plan reaches terminal state, before the task is marked Done.
- [ ] The check evaluates workspace output against the task's acceptance criteria / target files / declared API signatures (sourced from the task description / `analysis.json`).
- [ ] Pass → Done. Fail → task not marked Done; a fix task (or reopen) is created listing the specific unmet criteria.
- [ ] Non-machine-checkable criteria degrade to a `requiresResponse` human check (EPIC-014) rather than auto-passing — guarded so it no-ops cleanly if EPIC-014 isn't present yet.
- [ ] Feature-flagged (e.g. `KoboldLair:Verification:TaskAcceptanceEnabled`, default false).
- [ ] Distinct from and composable with EPIC-011's per-action evaluator (no duplication).
- [ ] Test: a task whose steps "completed" but missed a required API signature is caught and not marked Done.

## Out of scope

- Per-action evaluation — EPIC-011.
- Tech-stack build/test/lint verification — already handled by `WyvernVerificationService`; this is intent-level, not toolchain-level.
- Deferred to TASK-165 — an LLM judge for prose criteria ahead of the human check.

## Human test plan

- [ ] Craft a task with a clear acceptance criterion the Kobold won't satisfy; confirm the acceptance check blocks Done and spawns a fix task naming the unmet criterion.
- [ ] Confirm a genuinely complete task passes and is marked Done with the flag on.

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
