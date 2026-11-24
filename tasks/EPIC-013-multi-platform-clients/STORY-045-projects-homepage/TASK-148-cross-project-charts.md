---
id: TASK-148
parent: STORY-045
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-145]
blocks: []
findings: []
pr: 55
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

- [x] `GET /api/v1/stats` returns the aggregates above for the caller's projects
- [x] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [x] The homepage shows the tiles with `b-stat` and the two charts; empty data renders as empty charts, not errors
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Per-project numbers (TASK-158)
- Setting budgets (TASK-163)

## Human test plan

- [x] With usage recorded locally, the tiles and charts match the numbers in the database
- [x] Charts read right in light and dark theme

## Implementation plan

1. Red first: four server tests in `StatsEndpointsTests` and five e2e tests in `home-stats.spec.ts`.
   - Server tests:
     - The overview over the caller's projects: active projects, tasks by status and per project, done and failed
       this week, spend this month, 30 days of cost.
     - An owner with nothing gets zeros and 30 empty days.
     - `scope=all` widens to every project for an admin only.
     - The monthly budget is reported when one is set.
   - The e2e tests cover:
     - the tiles and both charts;
     - nothing yet: zeros and empty charts, no errors;
     - calls recorded at no cost: tokens are charted instead;
     - numbers that fail to load leave the cards in place;
     - no horizontal scroll at 375 px.
2. `ProjectStatsService.OverviewAsync(projects, now)` reads the task store and usage records; it reads no plans, so
   it stays cheap across many projects. `GET /api/v1/stats` passes it `ApiOwnership.VisibleProjects`, the rule
   `/needs-you` and `/notifications` use. No other owner's data can reach it, so there is no id to answer 404 for.
3. The homepage (`home-view.ts`):
   - The four `b-stat` tiles go under the title, as in mockup C1: active projects, tasks done this week, success rate,
     spent this month.
   - Two `b-chart` charts go below the cards:
     - Cost over the last 30 days. It shows tokens when calls were recorded at $0, as the Stats tab does.
     - Tasks by status per project, stacked bars in status groups.
   - The numbers load after the cards, so a failure only says so in place of the tiles.

## Notes

- Definitions:
  - **Active:** the project is Running and not Verified, Completed or Failed. A Paused project is not active.
  - **This week:** the last 7 days, counted by the time a task's status last changed (`UpdatedAt`).
  - **This month:** the calendar month in UTC.
  - **Cost by day:** every one of the last 30 days, zeros included, so the time axis is always 30 days wide.
- Human test on a throwaway server, a copy of TASK-196's `greeter175` with today's LAN-model usage:
  - `GET /api/v1/stats` matched the database exactly:
    - 1 project, 0 active (Completed).
    - 4 tasks Done, all this week, 100% success.
    - 137 calls and 1,710,160 tokens this month at $0: 123 calls and 1,543,000 tokens on 23 Nov, 14 calls and
      167,160 tokens on 24 Nov.
  - The page showed those numbers, charted tokens with the pricing note, and logged no console errors.
  - Light and dark theme both read right, and at 390 px the tiles wrap and the charts stack with no horizontal
    scroll.
- `b-chart` has no value formatter, so the token axis reads `2000000` rather than `2,000,000`. The Stats tab shows the
  same. Fixing it is a Birko.Web change, not filed.
- Full .NET suite: 621 passed. Full e2e suite: 159 passed. `tsc --noEmit` reports the same 32 errors before and
  after, all in Birko.Web's own sources.
