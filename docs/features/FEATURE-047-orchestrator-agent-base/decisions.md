---
id: FEATURE-047
created: 2026-02-06
---

# OrchestratorAgent base class — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Introduce a shared base for the coordinating agents (Dragon, Wyrm, Wyvern) | approved | shipped in 2.5.0 | 2026-02-06 | team | — |
| D2 | Provide reusable helpers for common guidance and response parsing | approved | shipped in 2.5.0 | 2026-02-06 | team | — |
| D3 | Replace per-agent reasoning-style logic with a single shared depth-guidance helper | approved | shipped in 2.5.0 | 2026-02-06 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-06 — feature created; decisions seeded from the shipped 2.5.0 changelog entry.
