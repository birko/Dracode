---
id: FEATURE-018
created: 2026-05-31
---

# REST + SSE facade for non-streaming clients — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Expose a versioned HTTP interface for projects, specs, features, tasks, plans, runs, agents, and cost reports | proposed | — | — | — | — |
| D2 | Publish an auto-generated, browsable API description and docs page | proposed | — | — | — | — |
| D3 | Offer a lightweight server-sent stream for run progress instead of a full live connection | proposed | — | — | — | — |
| D4 | Build the HTTP layer as a thin wrapper over existing services with no duplicated logic | proposed | — | — | — | — |
| D5 | Require authentication on all HTTP endpoints, with the local trusted mode exempt | proposed | — | — | — | — |
| D6 | Treat the first version as a stable contract; route unstable bits through an experimental path | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-016 behaviour, resource table, and risks.
