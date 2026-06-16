---
id: FEATURE-034
created: 2026-05-31
---

# Cross-step coherence re-plan check — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Trigger a coherence check only when a step fails or is revised, assessing whether remaining steps' assumptions still hold | proposed | Later steps shouldn't run against assumptions an earlier step just invalidated | — | — | TASK-028 |
| D2 | Default to "continue unchanged" unless a concrete contradiction is found — a cheap gate, not a re-planning loop | proposed | Must not re-plan on every step or harm throughput | — | — | TASK-028 |
| D3 | On contradiction, revise only the affected downstream steps (preserving completed work), or escalate if the change is large | proposed | Targeted, minimal revision instead of full re-plan | — | — | TASK-028 |
| D4 | Debounce so a step revised by the check can't immediately re-trigger another revision of the same steps | proposed | Prevent oscillation / re-planning thrash | — | — | TASK-028 |
| D5 | Ship behind a feature flag, default off until measured for thrash | proposed | Verify it doesn't cause thrash before enabling | — | — | TASK-028 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-031; decisions seeded from the story behaviour and TASK-028 acceptance criteria, all proposed pending /feature decide.
