---
id: FEATURE-020
generated: 2026-06-17
---

# Daemon mode for KoboldLair.Server — Status

> Auto-generated rollup. PM/stocktaker audience, no code jargon.

**Phase:** building

## Decisions at a glance

| State | Count |
|-------|-------|
| ✅ approved | 7 |
| ✏️ changed | 0 |
| ⏸️ deferred | 0 |
| ❌ removed | 0 |
| 💭 proposed (undecided) | 0 |

## Build progress

0 / 3 tasks done.

| Task | Status |
|------|--------|
| Background daemon with start / stop / status commands (+ graceful drain) | ⬜ todo |
| State file in the user's home (port, paths) + single-instance lock + stale-state recovery | ⬜ todo |
| Client auto-discovery, spawn-on-demand, and direct-address bypass | ⬜ todo |

## What can be tested now

Nothing yet — decisions are approved but no task has been built.

## Prototype
Skipped — this is server process/lifecycle plumbing; the test suite and running the daemon are the proof.

## Next step
Begin the build, starting with the daemon lifecycle commands and the home-folder state file.
