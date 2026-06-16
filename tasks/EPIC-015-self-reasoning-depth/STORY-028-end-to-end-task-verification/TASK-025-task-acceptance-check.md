---
id: TASK-025
parent: STORY-028
feature: FEATURE-031
status: todo
priority: P1
assignee: ai
created: 2026-05-29
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Post-plan task-acceptance check against acceptance criteria

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

## Human test plan

- [ ] Craft a task with a clear acceptance criterion the Kobold won't satisfy; confirm the acceptance check blocks Done and spawns a fix task naming the unmet criterion.
- [ ] Confirm a genuinely complete task passes and is marked Done with the flag on.

## Implementation plan

_Populated by `/tasks plan TASK-025` — leave empty until then._
