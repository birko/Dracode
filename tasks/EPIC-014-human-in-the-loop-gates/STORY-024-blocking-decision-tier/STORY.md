---
id: STORY-024
parent: EPIC-014
status: planned
created: 2026-05-29
---

# Blocking "AwaitingHumanDecision" escalation tier

## User story

As a KoboldLair operator, I want a task to be able to park itself and wait for my decision when its escalation is too consequential to auto-resolve, so that the same-model judgment that caused a problem isn't the only thing allowed to fix it.

## Behaviour

- A new escalation disposition `HumanDecisionRequired` sits alongside the existing auto-routes in `Drake.HandleEscalationAsync` (`Drake.cs:765-916`).
- When chosen, Drake sets the task to a new `AwaitingHumanDecision` execution/status state instead of calling `RevisePlanAsync` / `RefineTaskAsync`.
- The parked task **does not progress**; Drake continues picking up and running *other* tasks for the project (non-blocking at the project level, blocking at the task level).
- The escalation is persisted with a structured question payload (not just a summary string), survives server restart, and is the source of truth for the pending decision.
- On a human answer, the task transitions back to a runnable state and resumes from its saved plan/conversation checkpoint.
- Edge case: if no human answers within a configurable timeout, fall back to today's auto-resolve behaviour (so a parked task can never wedge a project forever).
- Edge case: a parked task must not be picked up by `DrakeExecutionService` or flagged as stalled by `ReasoningMonitorService` while it waits.
