---
id: FEATURE-010
created: 2026-05-31
---

# Wyvern task breakdown voting — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run the task-breakdown analysis 3× in parallel on the same specification | proposed | Highest-impact, most cascading stage; a single wrong breakdown wastes all downstream work | — | — | — |
| D2 | Score each candidate on requirements coverage, constraint propagation, valid specialist types, and description quality | proposed | Need an objective basis to compare candidates rather than picking arbitrarily | — | — | — |
| D3 | If 2 of 3 agree on shape (area split, critical-task count ±1, constraint set), pick the higher-scoring of the two | proposed | Agreement is a strong signal; score breaks the tie between the agreeing pair | — | — | — |
| D4 | If all 3 diverge, escalate to a 4th synthesis pass that takes all 3 candidates as input | proposed | A genuine three-way split means no candidate is trusted; synthesis combines their strengths | — | — | — |
| D5 | Gate the whole feature behind `KoboldLair:Voting:Wyvern:Enabled`, default off | proposed | Allows A/B comparison against today's single-pass behaviour; opt-in only | — | — | — |
| D6 | Provide a deterministic mode that seeds all 3 votes to the same answer for tests | proposed | Voting otherwise breaks test fixtures; coordinate with the epic-level criterion | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-008 behaviour and acceptance themes.
