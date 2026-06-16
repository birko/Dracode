---
id: FEATURE-072
created: 2026-01-10
---

# WebSocket multi-agent system — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Host multiple agents over a single WebSocket connection, each addressed by agentId | approved | shipped in 2.0 | 2026-01-10 | team | — |
| D2 | Keep provider config server-side with environment-variable expansion | approved | shipped in 2.0 | 2026-01-10 | team | — |
| D3 | Provide a command set: list / connect / disconnect / reset / send | approved | shipped in 2.0 | 2026-01-10 | team | — |
| D4 | Maintain independent history per agent and allow side-by-side response comparison | approved | shipped in 2.0 | 2026-01-10 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-01-10 — feature created; decisions seeded from the shipped 2.0 changelog entry (introduced DraCode.WebSocket, DraCode.Web, DraCode.AppHost).
