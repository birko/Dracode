---
id: FEATURE-027
created: 2026-05-31
---

# Blocking "AwaitingHumanDecision" escalation tier — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add an `AwaitingHumanDecision` state and a "human decision required" disposition so a task can park instead of always auto-resolving | proposed | Same-model judgment shouldn't be the only thing allowed to fix the problem it caused | — | — | TASK-019 |
| D2 | Persist the pending decision as a structured record that survives a server restart and is the source of truth | proposed | A waiting decision must not be lost if the server bounces | — | — | TASK-019 |
| D3 | Parking blocks only the one task; other tasks for the project keep executing, and parked tasks are skipped by the work picker and not flagged as stalled | proposed | One waiting task must never freeze the whole project | — | — | TASK-019, TASK-020 |
| D4 | On a human answer, the task resumes from its saved checkpoint; on timeout it falls back to today's auto-resolve | proposed | Make the round-trip useful while guaranteeing a parked task can never wedge forever | — | — | TASK-019, TASK-020 |
| D5 | Drake routes the decision to a human (parks + emits the pending decision) instead of revising/refining/resetting when the disposition is selected | proposed | This is the actual "stop and ask a person" branch in the supervisor | — | — | TASK-020 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-024; decisions seeded from the story's behaviour bullets and its two task files (TASK-019 data model, TASK-020 routing).
