---
id: TASK-019
parent: STORY-024
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-05-29
depends-on: []
blocks: [TASK-020]
pr: null
github-issue: null
jira-key: null
---

# AwaitingHumanDecision state + HumanDecisionRequired disposition

## Context

Today escalations always auto-resolve in `Drake.HandleEscalationAsync` (`DraCode.KoboldLair/Orchestrators/Drake.cs:765-916`). There is no state for "this task is parked waiting on a human." This task adds the data model: a new task state and a new escalation disposition, plus a structured pending-decision record that survives restart. It is the foundation TASK-020 builds the routing on.

Relevant existing types: `ExecutionState` enum (Running/Paused/Suspended/Cancelled), `TaskRecord` / `TaskEntity` / `TaskViewModel` / `EntityMapper`, `EscalationAlert` / `EscalationType` (`Models/Agents/ReflectionEntry.cs:22-29`), `ProjectNotificationService`.

## Acceptance criteria

- [ ] New task status/execution state `AwaitingHumanDecision` added to the relevant enum and propagated through `TaskRecord` / `TaskEntity` / `TaskViewModel` / `EntityMapper`.
- [ ] New `PendingDecision` record persisted (DB via the SQL repositories, consistent with plan/history persistence): question payload, escalation/source context, task id, created timestamp, optional timeout, resolution (null until answered).
- [ ] `DrakeExecutionService` skips tasks in `AwaitingHumanDecision` when picking work.
- [ ] `ReasoningMonitorService` does **not** flag an `AwaitingHumanDecision` task as stalled.
- [ ] `decisionTimeoutMinutes` honored: a parked task past timeout falls back to the current auto-resolve path (no permanent wedge).
- [ ] Migration/round-trip test: a parked task + its `PendingDecision` survive a server restart.

## Out of scope

- Drake's routing logic that *chooses* this disposition — TASK-020.
- The `ask_human` tool — TASK-021.
- Dragon UI / answering — TASK-024.

## Human test plan

- [ ] Force a task into `AwaitingHumanDecision` (temporary test hook), restart the server, confirm the task is still parked and its pending decision is intact.
- [ ] Confirm other tasks in the same project keep executing while one is parked.
- [ ] Let a parked task exceed `decisionTimeoutMinutes` and confirm it falls back to auto-resolve.

## Implementation plan

_Populated by `/tasks plan TASK-019` — leave empty until then._
