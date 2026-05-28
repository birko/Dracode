---
id: EPIC-013
status: planned
created: 2026-05-28
owner: ai
affects: [EPIC-003, EPIC-007]
depends-on: [EPIC-012]
---

# Multi-platform clients on the unified backend

## Area of concern

With EPIC-012 collapsing DraCode to a single backend (`KoboldLair.Server` with WS + REST/SSE), we can finally build a coherent family of clients that all speak the same protocol. The pre-existing planned epic EPIC-007 (CLI, VSCode, Python SDK) and EPIC-003 (Web UI polish) are superseded by this epic — both depended on a stable single backend, which is now true after EPIC-012.

## Success criteria

- `DraCode.KoboldLair.Cli` ships as a single-file binary for Windows / macOS / Linux with full Dragon, Wyvern, and Kobold verbs
- Discord bot operates as a service account against the REST facade
- VSCode extension hosts a Dragon side panel + inline diff view for Kobold runs
- Python SDK published to PyPI under `koboldlair`, used from notebooks
- `DraCode.KoboldLair.Client` (web) reaches the long-deferred polish items (drag-drop tabs, workspace layouts), plus new affordances exposed by the rework (daemon status, project switcher, OAuth login)
- Every client uses the same auth surface (OAuth for humans, service JWT for bots)

## Dependencies

Hard depends on EPIC-012. Story B5 (Web client polish) is independent enough that it could ship earlier in parallel with EPIC-012's backend work, but for ordering simplicity it lives here.

## Story priority order

1. STORY-019 (CLI) — unblocks the most users
2. STORY-023 (Web polish) — can run in parallel
3. STORY-021 (VSCode) — second-most demanded client surface
4. STORY-022 (Python SDK) — scripting & data-science use cases
5. STORY-020 (Discord bot) — nice-to-have, depends on service-account JWT being solid
