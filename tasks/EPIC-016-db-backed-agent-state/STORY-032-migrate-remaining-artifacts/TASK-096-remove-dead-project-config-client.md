---
id: TASK-096
parent: STORY-032
feature: null
status: todo
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

- [ ] `project-config-client.js` and its companion `wwwroot/js/PROJECT-CONFIG-API.md` (documents the same unserved routes) are deleted, after re-confirming nothing references it (scripts, bundler config, HTML, docs that point users at it)
- [ ] `rg "/api/project-configs"` has no hit outside history (CHANGELOG, archived docs)
- [ ] Client build (`npm run build`) and the generated UI smoke stay green

## Out of scope

- Adding REST routes for project configuration — the WebSocket command path already covers it
- Removing `ProjectConfigurationService` itself — TASK-094

## Human test plan

N/A — covered by automated tests (the file is unreferenced; the build and the UI smoke prove nothing loaded it)

## Implementation plan

_Populated by `/tasks plan TASK-096` — leave empty until then._
