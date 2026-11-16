---
id: TASK-148
parent: STORY-045
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-145]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Cross-project charts and numbers on the homepage

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

`/wyvern get_metrics` has cost per provider and project, budget status and escalation counts, but it is not
owner-scoped. This task adds `GET /api/v1/stats` over the caller's projects: tasks by status, success rate, and cost
per day for the last 30 days (from the usage records cost tracking keeps). The homepage shows number tiles (active
projects, tasks done this week, success rate, spend this month) and charts (cost over time, tasks by status per
project). The chart component is the plan's choice: a Birko one if it exists, otherwise a pinned library.

Decisions: FEATURE-026 D18.

## Acceptance criteria

- [ ] `GET /api/v1/stats` returns the aggregates above for the caller's projects
- [ ] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [ ] The homepage shows the tiles with `b-stat` and the two charts; empty data renders as empty charts, not errors
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Per-project numbers (TASK-158)
- Setting budgets (TASK-163)

## Human test plan

- [ ] With usage recorded locally, the tiles and charts match the numbers in the database
- [ ] Charts read right in light and dark theme

## Implementation plan

_Populated by `/tasks plan TASK-148` — leave empty until then._
