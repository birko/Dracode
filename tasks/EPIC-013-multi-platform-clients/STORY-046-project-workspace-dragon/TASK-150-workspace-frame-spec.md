---
id: TASK-150
parent: STORY-046
feature: FEATURE-026
status: todo
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-145]
blocks: [TASK-146, TASK-151, TASK-153, TASK-155, TASK-156, TASK-158]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Project workspace frame with the read-only spec tab

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

A project opens at `#/projects/{id}/{tab}`: the project's tabs in the centre — Board, Spec, Runs, Git, Stats (D11).
A tab is shown once its screen exists, so this task ships the frame with the Spec tab: the specification
(`GET /api/v1/projects/{id}/specification`, Markdown) and its features (`GET .../features`), read only. Spec edits go
through Dragon (D19), and the tab says so. The homepage's project cards become links to the workspace.

Decisions: FEATURE-026 D11, FEATURE-026 D19.

## Acceptance criteria

- [ ] A project card opens the workspace; the URL carries the project and the tab
- [ ] The Spec tab renders the specification's Markdown (sanitised) and lists the features with their status; there are no edit controls
- [ ] An unknown or not-owned project shows the not-found view
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Dragon docked beside the tabs (TASK-151)
- The other tabs (STORY-047)

## Human test plan

- [ ] Open a local project with a long spec: headings, lists and code blocks render; the features show with their status

## Implementation plan

_Populated by `/tasks plan TASK-150` — leave empty until then._
