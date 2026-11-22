---
id: TASK-146
parent: STORY-023
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: [TASK-147]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Header project picker inside a project

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Inside a project, a picker in the header switches to another of the caller's projects (`GET /api/v1/projects`) and
keeps the tab the user is on. There is no left project list (D10). This is the changed form of the old "project
switcher" (D4); preserving chat scroll position across switches was not discussed and is not part of it.

Decisions: FEATURE-026 D4, FEATURE-026 D10.

## Acceptance criteria

- [x] Inside a project the header shows the current project's name as a picker listing the caller's projects, each with its status
- [x] Typing filters the list; choosing a project opens it on the same tab
- [x] On the homepage the picker is not shown
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The "N other projects need you" pill (TASK-147)
- Keeping chat scroll position across switches (not decided)

## Human test plan

- [x] Open project A on a tab, pick project B in the header: B opens on the same tab; the URL names B

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Picker** (`components/project-picker.ts`, in the header's `project-picker` slot): Birko's `b-select` in
  `searchable` mode (bare, no clear button), its options the caller's projects from `GET /api/v1/projects?scope=all`
  (as the homepage: an admin sees all) grouped under their status, the value the current project. Typing filters,
  case- and accent-insensitive (`pritahy` finds "Príťahy"); choosing one sets `#/projects/{id}/{same tab}`; Escape
  leaves it as it was. It follows `hashchange`: rendered only on a project route, reloading the list when the route
  names a project it does not have yet. Status is shown as the group heading, so the box itself holds just the name.
- **Birko.Web v0.4.23 / v0.5.0 arrived meanwhile** in the local checkout: the shell now escapes the user name and
  initials (Birko TASK-965) and offers Profile only on request (TASK-966). DraCode's own escaping in `app-shell.ts`
  would have shown names escaped twice (the TASK-056 e2e test caught it), so it is removed, with the `getUserInitials`
  and `getUserMenuItems` overrides the shell's defaults now cover.
- **Tests:** e2e `project-picker.spec.ts`, 6 tests (all red on the old header): name and grouped list, filtering with
  and without accents and opening on the same tab, Board to Board, Escape, none on the homepage, phone width. Suite
  100/100.
- **Human test** (throwaway Testing server on a scratch copy, JWT on): on `waver2`'s Spec tab the picker read
  "waver2" and listed Completed: waver, InProgress: waver2; picking waver opened `#/projects/{waver}/spec`, title and
  picker "waver", Spec tab shown. No picker on the homepage. Light and dark at 1400px, 375px with the list open: no
  horizontal scroll (picker ~150px on a phone), no console errors.
