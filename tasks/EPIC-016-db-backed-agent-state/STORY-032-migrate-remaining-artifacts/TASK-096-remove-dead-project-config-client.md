---
id: TASK-096
parent: STORY-032
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-04
depends-on: []
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Remove the dead project-config-client.js (calls /api/project-configs routes nothing serves)

## Context

Found while planning TASK-094. `DraCode.KoboldLair.Client/wwwroot/js/project-config-client.js` (165 lines, class
`ProjectConfigClient`) calls eight `/api/project-configs…` routes (list, defaults, per-project get/put/delete, per-agent
get/put). No server code maps any of them, and no `.js`/`.ts`/`.html` file in the repo loads or imports the script —
it is leftover from the old vanilla UI, written against the file-backed `ProjectConfigurationService` that TASK-094
removes. Per-project agent settings are reached today through `ProjectConfigCommandHandler` → `ProjectService`.

## Acceptance criteria

- [x] `project-config-client.js` and its companion `wwwroot/js/PROJECT-CONFIG-API.md` (documents the same unserved routes) are deleted, after re-confirming nothing references it (scripts, bundler config, HTML, docs that point users at it)
- [x] `rg "/api/project-configs"` has no hit outside history (CHANGELOG, archived docs)
- [x] Client build (`npm run build`) and the generated UI smoke stay green

## Out of scope

- Adding REST routes for project configuration — the WebSocket command path already covers it
- Removing `ProjectConfigurationService` itself — TASK-094

## Human test plan

N/A — covered by automated tests (the file is unreferenced; the build and the UI smoke prove nothing loaded it)

## Implementation plan

Delete `wwwroot/js/` (it held only these two files) after re-checking references; rebuild the client; run the UI smoke against a Testing-environment server.

## Progress log

- 2026-10-06 — references re-checked with `git grep` (only the two files referenced each other; the build, the csproj and the HTML never load `wwwroot/js`); `wwwroot/js/` deleted. `npm run build` ✓ Built; UI smoke (server in Testing, client host) 3 passed after installing Playwright's Chromium. `npm run type-check` reports 2 errors in the shared Birko.Web.Components library (`b-kanban.ts:590`, `b-select.ts:615`, NodeListOf iteration) — identical on `main`, outside this repo.
