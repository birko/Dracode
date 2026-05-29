---
id: STORY-028
parent: EPIC-015
status: planned
created: 2026-05-29
---

# End-to-end task acceptance verification

## User story

As a KoboldLair maintainer, I want a finished task verified against its acceptance criteria before it's marked Done, so that "all steps completed" can't masquerade as "task actually accomplished."

## Behaviour

- After a Kobold's plan reaches terminal state (all steps Completed/Skipped/Failed), run a final **task-acceptance check** before the task transitions to Done.
- The check evaluates the workspace result against the task's acceptance criteria / target files / public API signatures that Wyvern wrote into the task description — not just "file exists + non-empty" (which is all `StepValidationService` does at `Kobold.cs:1614`).
- On pass → task Done. On fail → do not mark Done; create a fix task (or reopen) with the specific unmet criteria, consistent with the existing `WyvernVerificationService` fix-task pattern.
- Distinct from per-step validation: this is whole-task, intent-level, runs once at the end.
- Composes with EPIC-011's evaluator if/when it ships — the evaluator scores individual actions; this scores the finished task. They are complementary, not duplicative.
- Feature-flagged; default off until measured.
- Edge case: a task whose acceptance criteria are non-machine-checkable (e.g. "looks good") should degrade to a `requiresResponse` human check (ties into EPIC-014) rather than auto-passing.
