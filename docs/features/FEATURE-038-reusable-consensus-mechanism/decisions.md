---
id: FEATURE-038
created: 2026-05-31
---

# Reusable ConsensusService abstraction — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Provide one shared consensus service that accepts candidate producers, a scoring function, an agreement rule, and a config block (on/off flag, N, seed) | proposed | — | — | — | — |
| D2 | Return a structured result — chosen candidate, per-candidate scores, agreement level, rationale — suitable for persisting as a reasoning record | proposed | — | — | — | — |
| D3 | Support the existing decision rules: majority agreement, highest-score-among-agreeing, and synthesis-on-divergence | proposed | — | — | — | — |
| D4 | Offer a deterministic test mode (seeded) where all runs return the same answer so test fixtures stay stable | proposed | — | — | — | — |
| D5 | Be feature-flag aware — when a consumer's flag is off, run the single-candidate path with zero added cost | proposed | — | — | — | — |
| D6 | Handle a failing candidate by dropping it from the pool, not crashing; if all fail, surface a clear error instead of a silent empty consensus | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-035 behaviour bullets.
