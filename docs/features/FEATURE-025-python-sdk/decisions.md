---
id: FEATURE-025
created: 2026-05-31
---

# Python SDK — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Ship a `koboldlair-python` package published to PyPI as `koboldlair` | proposed | Standard installable client for the Python ecosystem | — | — | — |
| D2 | Provide both sync `Client` and `AsyncClient` surfaces | proposed | Covers both scripting and async notebook/data-science workflows | — | — | — |
| D3 | Support project create, wait-for-status, list tasks, run tasks, and live event streaming | proposed | Mirrors the core KoboldLair workflow programmatically | — | — | — |
| D4 | Generate Pydantic models from the server OpenAPI spec and track a schema hash to fail fast on drift | proposed | Keeps the SDK in sync with the server and catches contract mismatches early | — | — | — |
| D5 | `from_local_daemon()` auto-discovers a local daemon like the CLI does | proposed | Frictionless local use without manually supplying a URL/token | — | — | — |
| D6 | Async streaming over SSE with a sync-polling fallback for restrictive networks | proposed | Robust streaming even behind corporate proxies | — | — | — |
| D7 | Publish via GitHub Actions; SDK version matches the server API version | proposed | Automated releases with clear API-version alignment | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-022 behaviour (package, sync/async surface, models, daemon discovery, streaming, distribution).
