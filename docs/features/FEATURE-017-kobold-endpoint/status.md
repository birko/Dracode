---
id: FEATURE-017
generated: 2026-06-17
---

# /kobold WebSocket endpoint — ad-hoc + project-scoped Kobold execution — Status

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
| Live progress feed for a single run | ⬜ todo |
| The worker endpoint + its opening-message protocol | ⬜ todo |
| Ad-hoc mode (point it at a folder) | ⬜ todo |
| Project mode (point it at an existing task) | ⬜ todo |
| Allow the worker to work in folders outside a project | ⬜ todo |

## What can be tested now

Nothing yet — decisions are approved but no task has been built.

## Prototype
Skipped — this is a backend worker endpoint; the test suite and a live run are the proof.

## Next step
Begin the build, starting with the live progress feed and the endpoint protocol.
Sign-in for this endpoint is handled by FEATURE-019 (its validation step guards `/kobold`).
