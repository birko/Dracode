---
id: FEATURE-073
created: 2026-01-11
---

# Web client modernization — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Remove the third-party UI framework entirely | approved | shipped in 2.0.1 | 2026-01-11 | team | — |
| D2 | Rebuild the client as fully typed code split into clear modules (types, client, ui, main) with zero runtime dependencies | approved | shipped in 2.0.1 | 2026-01-11 | team | — |
| D3 | Use a modern, hand-built layout (flexible grids, shared style variables) that is responsive without any framework | approved | shipped in 2.0.1 | 2026-01-11 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-01-11 — feature created; decisions seeded from the shipped 2.0.1 changelog entry.
