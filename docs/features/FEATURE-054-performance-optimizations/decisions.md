---
id: FEATURE-054
created: 2026-02-04
---

# Performance optimizations — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Reuse a single static, cached set of serialization settings across services | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D2 | Increase the WebSocket transfer buffer from 4KB to 64KB | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D3 | Rework message-history trimming so it stays fast as conversations grow | approved | shipped in 2.4.1 | 2026-02-04 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-04 — feature created; decisions seeded from the shipped 2.4.1 changelog entry covering serialization, transport, and history-trimming efficiency.
