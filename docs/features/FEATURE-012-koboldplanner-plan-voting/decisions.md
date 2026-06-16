---
id: FEATURE-012
created: 2026-05-31
---

# KoboldPlanner plan voting — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Generate 2-3 implementation plan candidates in parallel per task | proposed | Trades a little planning cost for potentially large execution savings | — | — | — |
| D2 | Score each plan on step atomicity, dependency cleanliness, acceptance-criteria coverage, and total step count | proposed | Objective basis to compare plan quality rather than picking arbitrarily | — | — | — |
| D3 | Pick the single highest-scoring plan; no synthesis fallback | proposed | Stitching plans together risks inconsistent dependencies and ordering | — | — | — |
| D4 | Skip voting for low-complexity tasks (single plan is sufficient) | proposed | Avoids paying for voting where a trivial plan is already fine | — | — | — |
| D5 | Gate behind `KoboldLair:Voting:Planner:Enabled` (default off) with a per-complexity threshold | proposed | Allows A/B comparison and tuning without code changes | — | — | — |
| D6 | Hold this feature until STORY-006 (EPIC-009) decides whether the planner folds into the worker | proposed | If the planner is merged, this re-scopes to first-iteration plan voting or is cancelled | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-010 behaviour, decision rule, and blocker.
