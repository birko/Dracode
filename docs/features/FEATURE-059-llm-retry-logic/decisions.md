---
id: FEATURE-059
created: 2026-02-03
---

# LLM retry logic with backoff — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Automatically retry recoverable provider failures (rate limits, 5xx, timeouts, network) | approved | shipped in 2.4.0 | 2026-02-03 | team | — |
| D2 | Use exponential backoff with optional jitter, tunable via a retry policy | approved | shipped in 2.4.0 | 2026-02-03 | team | — |
| D3 | Honour the provider's Retry-After signal when present | approved | shipped in 2.4.0 | 2026-02-03 | team | — |
| D4 | Apply the same retry protection uniformly across all ten providers | approved | shipped in 2.4.0 | 2026-02-03 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-03 — feature created; decisions seeded from the shipped 2.4.0 changelog entry.
