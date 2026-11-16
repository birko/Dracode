---
id: TASK-146
parent: STORY-023
feature: FEATURE-026
status: todo
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

- [ ] Inside a project the header shows the current project's name as a picker listing the caller's projects, each with its status
- [ ] Typing filters the list; choosing a project opens it on the same tab
- [ ] On the homepage the picker is not shown
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The "N other projects need you" pill (TASK-147)
- Keeping chat scroll position across switches (not decided)

## Human test plan

- [ ] Open project A on a tab, pick project B in the header: B opens on the same tab; the URL names B

## Implementation plan

_Populated by `/tasks plan TASK-146` — leave empty until then._
