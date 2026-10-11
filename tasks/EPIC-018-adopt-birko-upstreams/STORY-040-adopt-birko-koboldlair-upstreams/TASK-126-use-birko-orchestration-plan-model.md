---
id: TASK-126
parent: STORY-040
feature: null
status: todo
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

- [ ] Local forks deleted; KoboldLair uses the Birko types (`KoboldImplementationPlan` derives from Birko's)
- [ ] Stored plans load unchanged — integers need no mapping; the renamed JSON properties (`LastErrorMessage`,
      `KoboldId`, `Planning` if string-serialized) are mapped on load, tested on a copy of real data
- [ ] `QueueTaskDispatcher` kept in DraCode against Birko's `ITaskDispatcher` (framework declined it)
- [ ] Plan / step tests green, with tests asserting DraCode's old behaviour (list above) updated

## Out of scope

- The framework change itself (Birko TASK-528)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
