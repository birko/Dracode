---
id: FEATURE-075
created: 2026-01-20
---

# Multi-task sequential execution — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Execute multiple tasks sequentially, with a fresh agent instance per task for full context isolation | approved | shipped in 2.1 | 2026-01-20 | team | — |
| D2 | Show running progress as Task N / Total | approved | shipped in 2.1 | 2026-01-20 | team | — |
| D3 | Keep going when a task fails so later tasks still run | approved | shipped in 2.1 | 2026-01-20 | team | — |
| D4 | Accept tasks three ways (comma-separated CLI, multi-line interactive, JSON Tasks array); migrate config from single TaskPrompt to a Tasks array, keeping backward compatibility | approved | shipped in 2.1 | 2026-01-20 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-01-20 — feature created; decisions seeded from the shipped 2.1 changelog entry.
