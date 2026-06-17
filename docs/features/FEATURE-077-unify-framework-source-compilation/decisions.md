---
id: FEATURE-077
created: 2026-06-17
---

# Unify Birko framework source compilation into DraCode.Birko — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Make one project (`DraCode.Birko`) the single compiler of the shared framework's identity-bearing types | approved | shipped (TASK-071) | 2026-06-16 | ai | TASK-071 |
| D2 | Have the other projects (core, server, tests) consume that compiled framework instead of re-compiling its sources | approved | shipped (TASK-071) | 2026-06-16 | ai | TASK-071 |
| D3 | Move the third-party packages the framework needs onto the single compiler project so consumers inherit them | approved | shipped (TASK-071) | 2026-06-16 | ai | TASK-071 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-06-17 — feature created from the standalone TASK-071 (already shipped in commit 41021d4); decisions recorded as approved to close a DV5 one-tree-only gap (task had `feature: null`, no feature folder).
