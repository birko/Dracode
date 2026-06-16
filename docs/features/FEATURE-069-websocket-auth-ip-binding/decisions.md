---
id: FEATURE-069
created: 2026-01-15
---

# WebSocket authentication with IP binding — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Support token-based authentication for WebSocket connections | approved | shipped in 2.0.5 | 2026-01-15 | team | — |
| D2 | Allow binding a token to a specific client IP so stolen tokens can't be reused | approved | shipped in 2.0.5 | 2026-01-15 | team | — |
| D3 | Detect the real client IP through proxies (X-Forwarded-For / X-Real-IP) | approved | shipped in 2.0.5 | 2026-01-15 | team | — |
| D4 | Keep authentication disabled by default and log failed attempts | approved | shipped in 2.0.5 | 2026-01-15 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-01-15 — feature created; decisions seeded from the shipped 2.0.5 changelog entry.
