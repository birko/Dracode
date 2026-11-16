---
id: TASK-154
parent: STORY-047
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-153]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Board: priority lanes in To do, drag to reprioritise

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

The To do column is split into priority lanes (Critical, High, Normal, Low). Dragging a card to another lane sets its
priority through the existing call (`POST /api/v1/tasks/{id}/priority`). The server's execution order stays
priority → dependencies → complexity; no server change. Reprioritising is a desktop action (D21).

Decisions: FEATURE-026 D16.

## Acceptance criteria

- [ ] To do shows the four lanes; dragging a card to another lane changes its priority and it stays there after a refresh
- [ ] A keyboard way to move a card to a lane exists
- [ ] A failed priority call puts the card back and shows an error
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Reordering inside a lane (not decided)

## Human test plan

- [ ] Drag a Normal task to Critical: it is picked up before the other Normal tasks

## Implementation plan

_Populated by `/tasks plan TASK-154` — leave empty until then._
