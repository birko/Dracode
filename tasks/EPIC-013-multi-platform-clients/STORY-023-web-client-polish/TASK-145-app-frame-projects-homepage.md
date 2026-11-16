---
id: TASK-145
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P1
assignee: agent
created: 2026-11-16
depends-on: []
blocks: [TASK-147, TASK-148, TASK-150, TASK-159, TASK-161, TASK-164, TASK-056]
findings: []
pr: null
github-issue: null
jira-key: null
---

# App frame and projects homepage

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Today the served page mounts `<kobold-lair-shell>` (a `BAppShell` with a ribbon), and the views in
`DraCode.KoboldLair.Client/src/views/` are mocks shaped for the ribbon (dashboard, projects, project-config, hierarchy,
metrics, compare, impact). Kept as plumbing (D9): the router and module manifests (`routes.ts`, `module-store.ts`), the
Shell auth store, `KoboldComponent`, the build and the e2e harness.

This task replaces the ribbon frame with the decided one — a header (product name, then slots for the project picker,
the notification bell and the user menu) over the route outlet — and makes the projects homepage the home route, so the
new frame lands with a working screen. `GET /api/v1/projects` returns id, name, owner, status, execution state and
created date; the homepage needs progress, so each listed project gains its task counts (total, done, in progress,
failed) from the task repository.

The settings and Dragon views stay reachable at their current routes until their rebuilt replacements land, so no
working screen disappears in between. Project cards are not links yet: the workspace they open is its own task.

Decisions: FEATURE-026 D9, FEATURE-026 D10, FEATURE-026 D22.

## Acceptance criteria

- [ ] The header replaces the ribbon; `#/` is the projects homepage
- [ ] The homepage shows the caller's projects as cards: name, status, execution state, a progress bar from the task counts; an admin sees every project
- [ ] `GET /api/v1/projects` returns task counts per project; server tests cover the counts and keep the owner scoping
- [ ] An empty homepage says how to start a project (Dragon)
- [ ] The ribbon views are deleted with their manifests, routes and e2e expectations: dashboard, projects, project-config, hierarchy, metrics, compare, impact; settings and Dragon still open at their routes
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The header's project picker, bell and user menu contents (their own tasks)
- What needs you and the charts (STORY-045)
- Sign-in (TASK-056)

## Human test plan

- [ ] With a few projects in the local test DB, the homepage shows a card per project whose progress matches its tasks
- [ ] Narrow the window below ~900px: cards stack in one column, the header still fits, no horizontal scroll
- [ ] Light and dark theme both read right

## Implementation plan

_Populated by `/tasks plan TASK-145` — leave empty until then._
