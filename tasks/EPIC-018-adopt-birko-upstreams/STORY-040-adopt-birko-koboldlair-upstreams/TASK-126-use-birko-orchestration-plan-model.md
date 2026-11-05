---
id: TASK-126
parent: STORY-040
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: null
pr: null
github-issue: null
jira-key: null
---

# Use Birko.AI.Orchestration's plan model, analyzer and dispatcher; migrate stored plan statuses

## Context

DraCode forks `ITaskDispatcher` / `DirectTaskDispatcher`, `StepDependencyAnalyzer`, `EscalationAlert`, `ReflectionEntry`, `TaskAssignment`, and keeps `KoboldImplementationPlan.cs` (735) plus the step validators. ⚠ Plan and step status are stored as integers and the enum orders differ (`StepStatus` …Skipped, Failed vs Birko …Failed, Skipped; `PlanStatus` has `Planning, Ready` that Birko lacks) — a type swap without a mapping turns every Failed step into Skipped.

Framework side: Birko TASK-528 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

### Unblocked — Birko TASK-528 landed (2026-10-11, Birko.Framework `3b1d83b5`)

The full mapping table is in `Framework/Birko.AI.Orchestration/CLAUDE.md` § "Adopting from DraCode.KoboldLair".
What it changes for this task:

- **The integer warning above no longer holds.** Birko's enums were renumbered to DraCode's values and pinned:
  `PlanStatus` Created(=DraCode `Planning`)=0, Ready=1, InProgress=2, Completed=3, Failed=4; `StepStatus`
  Pending=0, InProgress=1, Completed=2, Skipped=3, Failed=4. **Stored integers need no value mapping** — only
  `Planning` → `Created` changes, and only by name (matters for string-serialized JSON).
- **Member renames** (JSON property names change — map on load, or keep `[JsonPropertyName]` on the derived type):
  `LastErrorMessage` → `ErrorMessage`; `AssignStepsToKobold` → `AssignSteps(workerId, …)`;
  `EscalationAlert.KoboldId` (`Guid`) → `WorkerId` (`string`); `plan.ValidateStepOrdering(analyzer)` /
  `plan.ReorderSteps(analyzer)` → `analyzer.ValidateOrdering(plan)` / `analyzer.Reorder(plan)`;
  `ValidationResult` / `CombinedValidationResult` → `StepValidationResult` / `CombinedStepValidationResult`.
- **The retry block in `UpdatePlanStepTool`** becomes `step.RetryOrFail(output, category, category == Transient)`.
- **KoboldLair-only fields stay local:** `PlanFilename`, `Feature*`, `Specification*` → derive
  `KoboldImplementationPlan : ImplementationPlan` (Birko's is not sealed; `CaptureLessonsLearned` is virtual).
- **`QueueTaskDispatcher` stays in DraCode** — Birko declined it (no `Birko.MessageQueue` edge for one class).
  Keep it, re-pointed at Birko's `ITaskDispatcher`.
- **Behaviour that changes on adoption** (each now pinned by a Birko test — expect DraCode tests asserting the old
  behaviour to need updating, not the framework):
  - ordering validation no longer flags a creator placed *before* its modifier;
  - a kobold missing from a split plan gets **no** steps (it used to get every step and re-run the others');
  - `ContentExpectationValidator` fails a step it cannot check (no files / unreadable file) instead of passing it;
  - `Reorder` throws on a started or assigned plan instead of swallowing;
  - `ValidationAttempts` counts once per validation, not once per validator;
  - DraCode's own `StepDependencyAnalyzer.SuggestOptimalOrder` still has the `Reverse()` Birko removed at CR-H003
    (dependents first) — switching to Birko's fixes it.
- ⚠ KoboldLair.Server currently does not build because of Birko TASK-527's `CheckpointInterval` removal (that is
  TASK-125's adoption); do this after, or alongside, that fix.

## Acceptance criteria

- [x] Local forks deleted; KoboldLair uses the Birko types (`KoboldImplementationPlan` derives from Birko's)
- [x] Stored plans load unchanged — integers need no mapping; the renamed JSON properties (`LastErrorMessage`,
      `KoboldId`, `Planning` if string-serialized) are mapped on load, tested on a copy of real data
- [x] `QueueTaskDispatcher` kept in DraCode against Birko's `ITaskDispatcher` (framework declined it)
- [x] Plan / step tests green, with tests asserting DraCode's old behaviour (list above) updated

## Out of scope

- The framework change itself (Birko TASK-528)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan

Done 2026-11-05.

1. `KoboldImplementationPlan : Birko.AI.Orchestration.Models.ImplementationPlan` keeps only `PlanFilename`,
   `FeatureName`, `FeatureDescription`, `Specification*` (Birko has `FeatureId`). Deleted: DraCode's
   `ImplementationStep`, `StepExecutionMetrics`, `PlanExecutionMetrics`, `PlanLogEntry`, `PlanStatus`, `StepStatus`,
   `EscalationAlert` (+ its enums), `ReflectionEntry` (+ its enums), `Models/Validation/*`,
   `Services/StepDependencyAnalyzer`, `MessageQueue/ITaskDispatcher`, `MessageQueue/DirectTaskDispatcher`,
   `Messages/TaskAssignmentMessage`.
2. Call sites: `LastErrorMessage` → `ErrorMessage`; `PlanStatus.Planning` → `Created`; `AssignStepsToKobold` →
   `AssignSteps`; `plan.ValidateStepOrdering/ReorderSteps(analyzer)` → `analyzer.ValidateOrdering/Reorder(plan)`
   (planner only, on a fresh all-Pending plan, so `Reorder`'s new throw cannot fire there); escalation `KoboldId` →
   `WorkerId` (ReflectionTool, ReasoningMonitorService; Drake parses it back to a Guid for `GetKobold`);
   `UpdatePlanStepTool`'s retry block → `step.RetryOrFail(output, ErrorClassifier.ToStepCategory(category),
   category == Transient)`; `MetricsCommandHandler.PlanMetrics.TotalTokens` int → long (Birko's sum is long).
3. `QueueTaskDispatcher` kept, against Birko's `ITaskDispatcher` / `TaskAssignment`; Program.cs registers Birko's
   `DirectTaskDispatcher` for the default.
4. `Services/LegacyPlanJson`: every plan reader (`KoboldPlanService` ×2, `SqlPlanRepository` ×2) goes through
   `LegacyPlanJson.Deserialize`, which upgrades the stored JSON first — step `lastErrorMessage` → `errorMessage`,
   escalation `koboldId` → `workerId`, step `errorCategory` string → Birko's typed value. Walks every object, since the
   old serializer also wrote the computed `currentStep` copy. Statuses need nothing (integers, same values).
5. `ErrorClassifier.ToStepCategory`: Transient/Permanent map across; `Unknown` → null (not classified) — decided
   2026-11-05, for stored plans and new failures alike.

## Close notes

- Closed 2026-11-05. Suite 290/290 (279 + `LegacyPlanLoadTests` 7 + `UpdatePlanStepRetryTests` 4).
- **Real data:** no stored plan exists on this machine (both `koboldlair.db` files have 0 rows in `plans`; no
  `*-plan.json`). Per decision 2026-11-05 the fixtures (`Tests/Fixtures/LegacyPlans/`) were generated by the
  pre-migration code at fe6d687 — three plans for the real `C:/source/aicode/flappy-dragon` tasks (chasecam:
  transient-retried + permanently failed + skipped steps, reflections, a resolved escalation; collision: an `Unknown`
  failure, step assignments for two Kobolds; world: `Planning`) — saved through `SqlPlanRepository` itself
  (`*.plan-data.json` is the stored column) and with `KoboldPlanService`'s file options (`*-plan.json`).
- Red without the upgrade: every plan with a failed step fails to load (`JsonException` on the string
  `errorCategory`; the repository returns null). With only the renames disabled, `ErrorMessage` comes back null.
  `world` loads either way (nothing legacy in it).
- "Tests asserting DraCode's old behaviour": none existed — no DraCode test covered the analyzer, validators,
  assignments or reorder. The behaviour changes listed above apply as Birko pins them; effect here:
  - `GetAssignedSteps` (a worker missing from a split plan gets none): no DraCode caller — Kobolds filter by their own
    `_assignedStepIndices`.
  - `ContentExpectationValidator` failing a step it cannot check: auto-advance (TASK-125 hooks) is stricter for a step
    with `ExpectedContent` but no files.
  - `SuggestOptimalOrder` no longer reversed: the planner's reorder now puts creators before dependents.
  - `Reorder` throwing on a started/assigned plan: unreachable from the planner (fresh plan).
- Also changed by Birko's step methods: a final failure's text is in `ErrorMessage` (DraCode wrote it to `Output` and
  kept the raw error in `LastErrorMessage`); `Start()` clears `CompletedAt`; plans gain `CompletedAt`.
- Wire: the `/kobold` frame embeds `EscalationAlert`, so its `koboldId` is now `workerId` (string). No client code
  read it.
- `TaskAssignmentMessage`'s 7 extra fields dropped with it (decided 2026-11-05): nothing produced or consumed them;
  `ITaskDispatcher` is registered but has no caller.
- Human test plan: N/A (per task).
