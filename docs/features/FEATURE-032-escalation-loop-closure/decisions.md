---
id: FEATURE-032
created: 2026-05-31
---

# Escalation loop closure — feed resolution back to the Kobold — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Produce a structured resolution note for each resolved escalation: disposition, what changed, and explicit "don't retry X because Y" guidance | proposed | Closes the open feedback loop so the worker learns what changed | — | — | TASK-026 |
| D2 | Inject the resolution note into the worker's context on resume, before its next reasoning step | proposed | The next step must account for the resolution, not resume blind | — | — | TASK-026 |
| D3 | Cover all auto-resolved escalation types (revise / refine / reassign) | proposed | Loop must close consistently regardless of disposition | — | — | TASK-026 |
| D4 | On reassignment, brief the new specialist on what the previous one tried and why it failed | proposed | Avoid the replacement repeating the prior failed attempt | — | — | TASK-026 |
| D5 | When a human resolves a parked question, carry the human's answer through the same channel | proposed | One consistent resolution channel for both automatic and human resolutions | — | — | TASK-026 |
| D6 | Ship behind a feature flag, default off until measured | proposed | No behaviour change on healthy runs until proven | — | — | TASK-026 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-029; decisions seeded from the story behaviour and TASK-026 acceptance criteria, all proposed pending /feature decide.
