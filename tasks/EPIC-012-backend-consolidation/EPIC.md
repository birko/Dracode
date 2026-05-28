---
id: EPIC-012
status: planned
created: 2026-05-28
owner: ai
affects: [EPIC-003, EPIC-007]
---

# Backend consolidation — unify on KoboldLair.Server

## Area of concern

DraCode today has two parallel server/client stacks:

- **Old (predecessor):** `DraCode.WebSocket` exposes a single `DraCode.Agent` over WS (`connect/send/reset/disconnect`). `DraCode.Web` is its browser client. No project, no spec, no pipeline.
- **New (current):** `DraCode.KoboldLair.Server` runs the Dragon → Wyrm → Wyvern → Drake → Kobold pipeline. `DraCode.KoboldLair.Client` is its browser UI.

The two share `DraCode.Agent` as a library but never merged. The old stack is unmaintained dead weight: it doesn't see projects, doesn't speak to KoboldLair, but still compiles and ships. Meanwhile the new stack is WebSocket-only — fine for the browser UI, awkward for CLI / Discord / scripted callers that want one-shot REST calls.

This epic collapses everything to **one backend**: `DraCode.KoboldLair.Server` becomes the single source of truth, gains a `/kobold` endpoint that re-exposes the "single agent over the wire" use case the old `DraCode.WebSocket` covered, gains a REST/SSE facade for non-streaming clients, and learns to run as a per-user daemon. OAuth/OIDC adds real identity for any future multi-tenant or remote-daemon scenario.

## Success criteria

- `DraCode.WebSocket` and `DraCode.Web` removed from solution; no remaining references
- `KoboldLair.Server` exposes `/dragon`, `/wyvern`, and new `/kobold` WebSocket endpoints
- `/kobold` supports both ad-hoc (`do <prompt>` against cwd, worktree-always) and project-scoped (`run <projectId>/<taskId>`) modes
- REST facade under `/api/v1/...` with OpenAPI spec for projects, specs, features, tasks, plans, runs, agents, cost reports
- SSE streaming on `/api/v1/runs/{id}/events`
- OAuth/OIDC authentication (GitHub IdP as first impl) for remote daemons + Web UI; local daemons bypass auth (trust OS user)
- `KoboldLair.Server` supports `--daemon` mode with `~/.koboldlair/daemon.json` lock file and lazy spawn from clients

## Dependencies

EPIC-013 (multi-platform clients) depends on this epic.

## Open questions (decide during story planning)

- Whether ad-hoc `/kobold` runs create a hidden project record in `projects.json` or stay completely off-registry
- Exact REST resource shapes (will mirror existing service interfaces in `DraCode.KoboldLair.Server/Services/`)
- Migration plan for existing `projects.json` records into the new `ownerId`-aware schema
