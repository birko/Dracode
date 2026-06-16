---
id: FEATURE-049
created: 2026-02-04
---

# Parallel execution & threading — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run projects, supervisors, and tasks in parallel in the task supervisor service while honoring per-project worker limits | approved | shipped in 2.4.2 | 2026-02-04 | team | — |
| D2 | Parallelize assignment, analysis, and re-analysis in the analysis service | approved | shipped in 2.4.2 | 2026-02-04 | team | — |
| D3 | Monitor all supervisors simultaneously in the monitoring service | approved | shipped in 2.4.2 | 2026-02-04 | team | — |
| D4 | Optimize the supervisor factory lock scope using a double-check pattern | approved | shipped in 2.4.2 | 2026-02-04 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-04 — feature created; decisions seeded from the shipped 2.4.2 parallelization work.
