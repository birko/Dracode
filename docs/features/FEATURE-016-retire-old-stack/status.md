---
id: FEATURE-016
generated: 2026-05-31
---

# Retire DraCode.WebSocket and DraCode.Web — Status

> Auto-generated rollup. PM/stocktaker audience, no code jargon.

**Phase:** in-progress (decomposed + implementation started)

## Decisions at a glance

| State | Count |
|-------|-------|
| ✅ approved | 0 |
| ✏️ changed | 0 |
| ⏸️ deferred | 0 |
| ❌ removed | 0 |
| 💭 proposed (undecided) | 4 |

## Build progress

0 / 2 tasks done. TASK-029 (mechanical removal of DraCode.WebSocket + DraCode.Web) in progress; TASK-070 (scrub FULL_PROJECT_SPECIFICATION.md) todo.

## What can be tested now
Once TASK-029 lands: `dotnet build ./DraCode.slnx` succeeds, and `dotnet run --project DraCode.AppHost` shows only the two KoboldLair resources (no websocket/web group).

## Prototype
N/A — pure removal, no user-facing surface.

## Next step
Finish TASK-029 (deletion + AppHost/slnx/.vscode unwire + doc scrub), then TASK-070 (regeneration-spec scrub).
