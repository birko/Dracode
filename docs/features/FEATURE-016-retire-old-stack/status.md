---
id: FEATURE-016
generated: 2026-06-17
---

# Retire DraCode.WebSocket and DraCode.Web — Status

> Auto-generated rollup. PM/stocktaker audience, no code jargon.

**Phase:** done

## Decisions at a glance

| State | Count |
|-------|-------|
| ✅ approved | 4 |
| ✏️ changed | 0 |
| ⏸️ deferred | 0 |
| ❌ removed | 0 |
| 💭 proposed (undecided) | 0 |

## Build progress

2 / 2 tasks done. TASK-029 (mechanical removal of DraCode.WebSocket + DraCode.Web) done — verified via the Aspire dashboard (only the two KoboldLair resources remain). TASK-070 (retired the stale FULL_PROJECT_SPECIFICATION.md regeneration spec) done.

## What can be tested now

Shipped & verified: `dotnet build ./DraCode.slnx` succeeds, and `dotnet run --project DraCode.AppHost` shows only the two KoboldLair resources (no websocket/web group).

## Prototype
N/A — pure removal, no user-facing surface.

## Next step
None — feature complete and signed off.
