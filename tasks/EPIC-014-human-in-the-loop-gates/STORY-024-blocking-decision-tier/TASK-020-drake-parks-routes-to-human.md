---
id: TASK-020
parent: STORY-024
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-05-29
depends-on: [TASK-019]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Drake parks task & routes decision to human instead of auto-resolving

## Context

With the `AwaitingHumanDecision` state and `PendingDecision` record in place (TASK-019), wire `Drake.HandleEscalationAsync` (`Drake.cs:765-916`) so that when the `HumanDecisionRequired` disposition is selected it parks the task and emits the pending decision, instead of calling `RevisePlanAsync` / `RefineTaskAsync` / reset. The *choice* of when to use this disposition comes from policy (TASK-023); this task implements the branch itself with a hard-coded/test trigger until the policy lands.

## Acceptance criteria

- [ ] `HumanDecisionRequired` branch in `HandleEscalationAsync`: sets task `AwaitingHumanDecision`, writes a `PendingDecision`, emits a `requiresResponse` notification (schema-only here; Dragon rendering is TASK-024).
- [ ] Drake does not block — it returns and continues processing other tasks (preserve the existing fire-and-forget `Task.Run` pattern).
- [ ] On resolution callback, the task transitions back to runnable and resumes from its saved plan/conversation checkpoint.
- [ ] Resolution payload is recorded so STORY-029 (loop closure) can feed it back to the Kobold.
- [ ] Falls through to the existing auto-resolve routes for every disposition that is not `HumanDecisionRequired` (zero behaviour change by default).
- [ ] Unit/integration test: an escalation tagged `HumanDecisionRequired` parks; a simulated answer resumes the task.

## Out of scope

- The policy that decides when to pick this disposition — TASK-023.
- Async `ask_human` agent tool — TASK-021.

## Human test plan

- [ ] Trigger an escalation flagged for human decision, confirm the task parks and a pending decision appears (via repository/notification inspection).
- [ ] Submit a resolution through the resolution callback and confirm the task resumes and completes.

## Implementation plan

_Populated by `/tasks plan TASK-020` — leave empty until then._
