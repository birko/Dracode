---
id: FEATURE-018
created: 2026-05-31
---

# REST + SSE facade for non-streaming clients — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Expose a versioned HTTP interface for projects, specs, features, tasks, plans, runs, agents, and cost reports | approved | `/api/v1` resource + runs + agents/cost endpoints | 2026-06-17 | human | TASK-043, TASK-044, TASK-046 |
| D2 | Publish an auto-generated, browsable API description and docs page | approved | OpenAPI document + docs page shipped with the API skeleton | 2026-06-17 | human | TASK-042 |
| D3 | Offer a lightweight server-sent stream for run progress instead of a full live connection | approved | SSE stream for run progress; reuses the per-run event source from FEATURE-017 | 2026-06-17 | human | TASK-045 |
| D4 | Build the HTTP layer as a thin wrapper over existing services with no duplicated logic | approved | Minimal-API layer delegates to existing services; no business logic duplicated | 2026-06-17 | human | TASK-042 |
| D5 | Require authentication on all HTTP endpoints, with the local trusted mode exempt | approved | Enforced by the JWT middleware from FEATURE-019 (loopback daemon bypass) | 2026-06-17 | human | TASK-032 |
| D6 | Treat the first version as a stable contract; route unstable bits through an experimental path | approved | `/api/v1` stable; experimental routes carry a separate path | 2026-06-17 | human | TASK-042 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-016 behaviour, resource table, and risks.
- 2026-06-17 — decide: D1–D6 proposed → approved (ratifying the STORY-016 design; work not yet started); `→ Tasks` wired to TASK-042…046 and feature back-link added to those tasks. D5 enforced cross-feature by TASK-032 (FEATURE-019). Phase → building (0/5 done).
