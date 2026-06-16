---
id: FEATURE-051
created: 2026-02-04
---

# Drake execution service — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run task execution as an automatic background service polling every 30 seconds | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D2 | Pick up only projects that have completed analysis, then create a Drake per task file | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D3 | Summon Kobolds for ready tasks and mark the project complete when all tasks finish | approved | shipped in 2.4.1 | 2026-02-04 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-04 — feature created; decisions seeded from the shipped 2.4.1 changelog entry that bridged Wyvern analysis to live task execution.
