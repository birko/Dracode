---
id: FEATURE-046
created: 2026-02-06
---

# Network error handling for tasks — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Treat an error response with empty content as a task failure, not a success | approved | shipped in 2.5.1 | 2026-02-06 | team | — |
| D2 | Inject the error message into the conversation so the worker correctly fails | approved | shipped in 2.5.1 | 2026-02-06 | team | — |
| D3 | Apply the behaviour uniformly across all providers and all agent types | approved | shipped in 2.5.1 | 2026-02-06 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-06 — feature created; decisions seeded from the shipped 2.5.1 changelog entry.
