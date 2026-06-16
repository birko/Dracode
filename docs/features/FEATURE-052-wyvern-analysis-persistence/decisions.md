---
id: FEATURE-052
created: 2026-02-04
---

# Wyvern analysis persistence — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Persist each project's analysis to a durable analysis.json file | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D2 | Provide save and load operations so analysis can be written and read back | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D3 | Recover persisted analysis on server startup so restarts don't lose work | approved | shipped in 2.4.1 | 2026-02-04 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-04 — feature created; decisions seeded from the shipped 2.4.1 changelog entry that made Wyvern analysis durable and recoverable.
