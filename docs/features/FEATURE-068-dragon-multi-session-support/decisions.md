---
id: FEATURE-068
created: 2026-01-31
---

# Dragon multi-session support — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Track multiple concurrent conversations per connection | approved | shipped in 2.2.0 | 2026-01-31 | team | — |
| D2 | Keep conversations alive ~10 minutes across disconnects with replay on reconnect | approved | shipped in 2.2.0 | 2026-01-31 | team | — |
| D3 | Retain up to 100 messages per conversation for replay | approved | shipped in 2.2.0 | 2026-01-31 | team | — |
| D4 | Allow provider reload mid-conversation and run a 60-second cleanup timer | approved | shipped in 2.2.0 | 2026-01-31 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-01-31 — feature created; decisions seeded from shipped 2.2.0 changelog entry.
