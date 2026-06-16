---
id: FEATURE-004
created: 2026-05-31
---

# Monitoring & observability infrastructure — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Expose a Prometheus scrape endpoint covering agent counts, queue depth, provider rates, token usage and completion stats, plus a Grafana dashboard template | proposed | — | — | — | TASK-009 |
| D2 | Ship a separate, real-time read-only monitoring web app showing the live agent hierarchy, project progress, error stream and provider status | proposed | — | — | — | TASK-010 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from EPIC-005 success criteria and STORY-002 behaviour (metrics endpoint + monitoring dashboard).
