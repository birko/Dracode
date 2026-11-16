---
id: FEATURE-027
created: 2026-05-31
---

# Blocking "AwaitingHumanDecision" escalation tier — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add an `AwaitingHumanDecision` state and a "human decision required" disposition so a task can park instead of always auto-resolving | approved | Same-model judgment shouldn't be the only thing allowed to fix the problem it caused | 2026-11-16 | owner | TASK-019 |
| D2 | Persist the pending decision as a structured record that survives a server restart and is the source of truth | approved | A waiting decision must not be lost if the server bounces | 2026-11-16 | owner | TASK-019 |
| D3 | Parking blocks only the one task; other tasks for the project keep executing, and parked tasks are skipped by the work picker and not flagged as stalled | approved | One waiting task must never freeze the whole project | 2026-11-16 | owner | TASK-019, TASK-020 |
| D4 | On a human answer, the task resumes from its saved checkpoint. A timeout is optional and set by whatever parks the task: an escalation falls back to today's auto-resolve when it expires; a decision that must not auto-resolve has no timeout and only reminds the user | changed | Was: every parked task falls back to auto-resolve on timeout. Changed so a gate that must not pass by default (the task acceptance check's human card) can wait without ever auto-resolving, while escalations still can never wedge forever | 2026-11-16 | owner | TASK-019, TASK-020 |
| D5 | Drake routes the decision to a human (parks + emits the pending decision) instead of revising/refining/resetting when the disposition is selected | approved | This is the actual "stop and ask a person" branch in the supervisor | 2026-11-16 | owner | TASK-020 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-024; decisions seeded from the story's behaviour bullets and its two task files (TASK-019 data model, TASK-020 routing).
- 2026-11-16 — D1 proposed → approved (owner)
- 2026-11-16 — D2 proposed → approved (owner)
- 2026-11-16 — D3 proposed → approved (owner)
- 2026-11-16 — D4 proposed → changed (owner): the timeout is per parked decision; escalations fall back to auto-resolve on expiry, a decision that must not auto-resolve has no timeout and only reminds
- 2026-11-16 — D5 proposed → approved (owner)
