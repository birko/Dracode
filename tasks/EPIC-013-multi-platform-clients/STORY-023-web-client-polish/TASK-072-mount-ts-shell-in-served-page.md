---
id: TASK-072
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P1
assignee: ai
created: 2026-06-22
depends-on: []
blocks: [TASK-056]
pr: null
github-issue: null
jira-key: null
---

# Mount the TypeScript shell in the served page

## Context

The served `DraCode.KoboldLair.Client/wwwroot/index.html` is a self-contained **legacy
vanilla-JS app** (its own router + mock data); it does not load the built TypeScript shell
(`wwwroot/dist/app.js`) and does not mount `#app-shell` / `#route-outlet`. As a result the
TypeScript app in `src/` — where all current feature work lives (dragon view, settings, and the
TASK-056 OAuth login + API-keys UI) — **is not served by anything**. `app-shell.ts:11-13` notes
this explicitly. Discovered during the TASK-056 plan (2026-06-22); likely related to the active
`Birko.Web.Shell` investigation (commit `54412d9`).

Until the shell is mounted, TASK-056 (and any other `src/` feature) ships dark — built but never
rendered. This task makes the served page boot the TS shell so those features become reachable.

## Acceptance criteria

- [ ] The served page loads the built shell bundle (`dist/app.js`) and mounts the shell root
      (`#app-shell` + `#route-outlet`, per `app-shell-init.ts` / `app-shell.ts`)
- [ ] The TS router drives navigation (hash routes resolve to the `src/views/*` views), replacing
      the legacy vanilla-JS router/mock data on the served page
- [ ] No regression to the build (`npm run build` produces the bundle the page references) and the
      app reaches an authenticated/landing state without console errors
- [ ] Decide the fate of the legacy vanilla-JS `index.html` content (archived or removed), consistent
      with the `wwwroot/archive/old-vanilla-ui/` precedent

## Out of scope

- The OAuth login button + API-keys UI itself (TASK-056)
- Any server-side change

## Human test plan

- [ ] Load the served app in a browser → the TypeScript shell renders (not the legacy mock UI); hash
      navigation works; no console errors

## Implementation plan

_Populated by `/tasks plan TASK-072` — leave empty until then. Coordinate with the active
Birko.Web.Shell work (commit `54412d9`) before implementing to avoid conflicts._
