---
id: TASK-158
parent: STORY-047
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Stats tab: per-project numbers

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

No per-project REST exists; `/wyvern get_metrics` and `get_project_agents` are not owner-scoped. This task adds
`GET /api/v1/projects/{id}/stats`: tasks by status and agent type, success rate, average task duration, cost by
provider and model over time, and spend against the project budget. Its shape is compatible with the cross-project
`GET /api/v1/stats`, so whichever lands second reuses the other's service; neither waits for the other.

Decisions: FEATURE-026 D18.

## Acceptance criteria

- [ ] The endpoint returns the numbers above
- [ ] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [ ] The Stats tab shows them with `b-stat` tiles and charts; empty data renders cleanly
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Cross-project numbers (TASK-148)

## Human test plan

- [ ] For a finished local project, the tab's numbers match its tasks and usage records

## Implementation plan

_Populated by `/tasks plan TASK-158` — leave empty until then._
