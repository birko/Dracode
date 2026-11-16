---
id: TASK-149
parent: STORY-045
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-151]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Home Dragon on the projects homepage

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

With no project selected, the homepage docks a "home Dragon" — the same dock component as the workspace — for
starting new projects and asking across projects. Dragon history is stored per project; check what the server keeps
for a session with no project. If it keeps nothing, this task adds per-user storage for the home conversation.

Decisions: FEATURE-026 D12.

## Acceptance criteria

- [ ] The homepage shows the Dragon dock with its own conversation, not any project's
- [ ] A project created from the home Dragon appears on the homepage; opening it switches Dragon to that project's history
- [ ] The home conversation is still there after a reload
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The dock component itself (TASK-151)

## Human test plan

- [ ] Ask the home Dragon to start a project, finish the spec, open the new project: Dragon now shows the project's conversation; go home: the home conversation is back

## Implementation plan

_Populated by `/tasks plan TASK-149` — leave empty until then._
