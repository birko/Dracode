---
id: TASK-153
parent: STORY-047
feature: FEATURE-026
status: todo
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: [TASK-154, TASK-164]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Board tab: status kanban with Retry

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

The Board tab shows the project's tasks (`GET /api/v1/projects/{id}/tasks`) in fixed status columns: To do,
In progress, Blocked·Failed, Done. The agents move the cards; the user does not drag between status columns (D16).
No push of task changes reaches a browser today, so the board refreshes while visible (the plan picks polling or
another way). Failed cards have Retry (`POST /api/v1/tasks/{id}/retry`). When Dragon is expanded the board turns
into a compact list (D13).

Decisions: FEATURE-026 D16.

## Acceptance criteria

- [ ] Four status columns; each card shows title, agent type, priority and, on a failed card, the start of its error
- [ ] Retry on a failed card resets it and the card moves to To do
- [ ] The board refreshes on its own while visible
- [ ] With Dragon expanded, the board shows as a compact list
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Priority lanes and reprioritising (TASK-154)
- Further statuses (D16: later, if missing)

## Human test plan

- [ ] Watch a running local project: cards move between columns as the agents work
- [ ] Retry a failed task: it leaves Blocked·Failed and runs again

## Implementation plan

_Populated by `/tasks plan TASK-153` — leave empty until then._
