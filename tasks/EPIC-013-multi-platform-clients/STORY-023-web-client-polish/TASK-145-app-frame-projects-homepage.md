---
id: TASK-145
parent: STORY-023
feature: FEATURE-026
status: done
priority: P1
assignee: agent
created: 2026-11-16
depends-on: []
blocks: [TASK-147, TASK-148, TASK-150, TASK-159, TASK-161, TASK-164, TASK-056]
findings: []
pr: 13
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

- [x] The header replaces the ribbon; `#/` is the projects homepage
- [x] The homepage shows the caller's projects as cards: name, status, execution state, a progress bar from the task counts; an admin sees every project
- [x] `GET /api/v1/projects` returns task counts per project; server tests cover the counts and keep the owner scoping
- [x] An empty homepage says how to start a project (Dragon)
- [x] The ribbon views are deleted with their manifests, routes and e2e expectations: dashboard, projects, project-config, hierarchy, metrics, compare, impact; settings and Dragon still open at their routes
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The header's project picker, bell and user menu contents (their own tasks)
- What needs you and the charts (STORY-045)
- Sign-in (TASK-056)

## Human test plan

- [x] With a few projects in the local test DB, the homepage shows a card per project whose progress matches its tasks
- [x] Narrow the window below ~900px: cards stack in one column, the header still fits, no horizontal scroll
- [x] Light and dark theme both read right

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Frame:** `KoboldLairAppShell` now extends Birko.Web.Shell's `BCoreAppShell` (header over a content slot) instead of
  `BAppShell` (ribbon + status bar). The header is the product name, then `data-header-slot` elements for the project
  picker, the bell and the user menu. The picker and the bell are empty until TASK-146 and TASK-159; the user menu slot
  holds the shell's own user dropdown (sign out) until TASK-056.
- **Server:** `GET /api/v1/projects` gives each project `tasks: { total, done, inProgress, failed }` from
  `ITaskRepository` (null with no task store). In progress counts `NotInitialized` and `Working`; failed counts
  `Failed` only, not `BlockedByFailure`. Test: `ResourceEndpointsTests.Project_listing_carries_each_projects_task_counts`
  (red before the change); the existing owner-scoping test is unchanged.
- **Homepage** (`kobold-home-view`): asks for `?scope=all`, which the server honours only for an admin. Signed out
  (no token) it asks to sign in and sends nothing, since `/api/v1` needs a signed-in caller; a failed load says so.
- **Deleted:** the dashboard, projects, project-config, hierarchy, metrics, compare and impact views, their module
  manifests, routes and e2e expectations. Dragon (`#/dragon`, `#/dragon/specifications`) and settings (`#/settings`,
  its two tabs) keep their routes. The not-found view links back to `#/`.
- **e2e:** new `home.spec.ts` (14 tests) failed 14/14 on the old build; the suite is 35/35 after.
- **Human test (2026-11-20):** a throwaway server (Testing environment, a copy of TASK-179's scratch projects folder,
  JWT on) served the real client. With an owner's token the homepage showed `waver` (Completed, 8 of 8 tasks done) and
  `waver2` (InProgress, 0 of 3 done, 1 failed), matching the task rows in its database; another user's token got an
  empty list. Checked by screenshots in light and dark at 1280px and 375px: cards stack in one column at 375px, the
  header fits, no horizontal scroll, no console errors.
- The user menu still reads "Guest": the shell takes the name from the auth store, which only sign-in (TASK-056) fills.
