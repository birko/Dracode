---
id: FEATURE-009
created: 2026-05-31
---

# Evaluate collapsing the three Drake background services — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Measure each service's real-work-vs-empty-tick ratio over 24h before changing anything | proposed | Need baseline overhead data to justify any merge or event-trigger change | — | — | — |
| D2 | Merge the stuck-worker watcher and the stalled-reasoning watcher into a single liveness service (lower risk first) | proposed | Their watching responsibilities overlap conceptually | — | — | — |
| D3 | Explore making the work-starter react to project-ready events instead of polling on a fixed timer | proposed | Reduces idle background activity and makes the system more responsive | — | — | — |
| D4 | Isolate each responsibility internally within any merged service to avoid a single large failure point | proposed | Each service has its own retry/recovery logic; merging must not create one shared failure domain | — | — | — |
| D5 | Document whether the current timer intervals are deliberate or arbitrary before changing them | proposed | Avoids silently breaking timing assumptions baked into the current cadence | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-007 hypotheses, test plan, risk, and open question.
