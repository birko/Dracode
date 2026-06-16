---
id: FEATURE-040
created: 2026-05-31
---

# Retrofit voting integrations onto the shared mechanism — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Re-express all three voting integrations (Wyvern breakdown, WyrmAgent selection, KoboldPlanner plan) as consumers of the shared consensus service | proposed | — | — | — | — |
| D2 | Preserve behaviour and feature flags exactly — no regression in agreement rules or cost profile; this is a refactor, not a behaviour change | proposed | — | — | — | — |
| D3 | Document the applicable sequencing path: post-ship migration if the integrations land first, or no-op verification if the shared service lands first | proposed | — | — | — | — |
| D4 | Acceptance: no bespoke parallel-vote-and-score loops remain outside the shared service, and each retrofitted integration's fixtures exercise the deterministic test mode | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-037 behaviour bullets. Depends on FEATURE-038 (shared consensus mechanism).
