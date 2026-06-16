---
id: FEATURE-055
created: 2026-02-04
---

# Robust WyvernAgent JSON handling — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Parse every response shape a provider may return (text, content blocks, collections, object forms) | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D2 | Throw a clear error on empty responses or missing analysis instead of returning an empty result | approved | shipped in 2.4.1 | 2026-02-04 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-04 — feature created; decisions seeded from the shipped 2.4.1 changelog entry that hardened Wyvern's response parsing and error reporting.
