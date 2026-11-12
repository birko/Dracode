---
id: TASK-142
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P1
assignee: ai
created: 2026-11-12
depends-on: [TASK-072, TASK-114]
blocks: []
blocked: null
pr: null
github-issue: null
jira-key: null
---

# Wire the web client's views to the server; they show mock data

## Context

Found while doing TASK-072. Once the TypeScript shell is served, every view in
`DraCode.KoboldLair.Client/src/views/` shows hard-coded sample data or a simulation:

- `dragon-view.ts` — `connectToDragon()` / `handleSend()` simulate the Dragon (`// TODO: Connect to actual WebSocket
  Dragon agent`); nothing reaches `/dragon`
- `dashboard-view.ts`, `projects-view.ts`, `hierarchy-view.ts`, `metrics-view.ts`, `compare-view.ts`,
  `impact-view.ts`, `project-config-view.ts`, `settings-view.ts` — mock data, `// TODO: Replace with actual API call`
- `services/api-client.ts` (the `/wyvern` command client) exists but no view calls it

The legacy page TASK-072 archived (`wwwroot/archive/legacy-index.html`) at least opened a live `/wyvern` connection, so
mounting the shell traded that for a complete but mock UI (owner's call, 2026-11-12: proceed and file this).

## Acceptance criteria

- [ ] Dragon view talks to `/dragon` (session, `client_ready`, messages, streaming, `session_resumed` replay)
- [ ] Dashboard, projects, hierarchy, settings/providers and project-config read real data (`/wyvern` commands or
      `/api/v1`), with loading and error states; no mock data left in `src/views/`
- [ ] Metrics / compare / impact either read real data or are hidden until they can
- [ ] The connection state in the shell status bar reflects the real connection
- [ ] E2E: authored flows against a disposable backend (see CLAUDE.md § Testing, test data isolation)

## Out of scope

- Sign-in (TASK-056), the token store (TASK-114)

## Human test plan

- [ ] With a local server on a throwaway projects path, chat with Dragon in the browser and see the project list update

## Implementation plan
