---
id: FEATURE-018
generated: 2026-06-17
---

# REST + SSE facade for non-streaming clients — Status

> Auto-generated rollup. PM/stocktaker audience, no code jargon.

**Phase:** building

## Decisions at a glance

| State | Count |
|-------|-------|
| ✅ approved | 6 |
| ✏️ changed | 0 |
| ⏸️ deferred | 0 |
| ❌ removed | 0 |
| 💭 proposed (undecided) | 0 |

## Build progress

0 / 5 tasks done.

| Task | Status |
|------|--------|
| The HTTP skeleton + auto-generated API description and docs page | ⬜ todo |
| Read/write endpoints for projects, specs, features, tasks, plans | ⬜ todo |
| Endpoints to start runs and check their status | ⬜ todo |
| Lightweight server-sent stream of run progress | ⬜ todo |
| Endpoints for active agents and cost reports | ⬜ todo |

## What can be tested now

Nothing yet — decisions are approved but no task has been built.

## Prototype
Skipped — this is a backend HTTP surface; the auto-generated API docs page and the test suite are the proof.

## Next step
Begin the build, starting with the HTTP skeleton and API docs page. The
"require sign-in on every endpoint" decision is enforced by FEATURE-019's validation
step (local trusted mode exempt), so it depends on that work landing.
