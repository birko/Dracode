---
id: TASK-158
parent: STORY-047
feature: FEATURE-026
status: done
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

- [x] The endpoint returns the numbers above
- [x] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [x] The Stats tab shows them with `b-stat` tiles and charts; empty data renders cleanly
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Cross-project numbers (TASK-148)

## Human test plan

- [x] For a finished local project, the tab's numbers match its tasks and usage records

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Endpoint** `GET /api/v1/projects/{id}/stats` (`Api/StatsEndpoints.cs`), owner-scoped (another owner's or an unknown
  project is 404). `Services/ProjectStatsService` takes a list of projects — the cross-project stats (TASK-148) can
  reuse it — and returns `tasks` (total, every status's count, by agent type with done/failed, success rate =
  done ÷ (done + failed), average duration and how many were timed), `cost` (total USD, tokens, calls, by provider and
  model, by UTC day) and `budget` (`CostTracking.Budget.ProjectBudgetUsd`, spent, percent, warning threshold; null with
  no budget). A Done task's duration runs from its plan's first step start (else the plan's creation) to the plan's
  completion, from `KoboldPlanService.LoadPlanAsync` (SQLite, else the plan file). `SqlUsageRepository` gains
  `GetProjectRecordsAsync`.
- **Stats tab** (`views/stats-tab.ts`, after Git): `b-stat` tiles (tasks, done, success rate with how many finished,
  average task with how many were timed, spend, tokens with the calls), the budget line with a `b-progress` (warning
  past the threshold, danger past 100%), and four `b-chart` bar charts — tasks by status (only statuses that have
  tasks), by agent type (done, failed, the rest stacked), cost by day, cost by provider and model. When calls were
  recorded at no cost (no prices set) the two cost charts show tokens instead and a note names `CostTracking.Pricing`.
  Empty data shows dashes and a line in place of each chart. Loaded on open and on Refresh.
- **Tests:** .NET `StatsEndpointsTests` (the numbers from tasks, plans and usage records of one project and not
  another's, the budget; an empty project as zeros and nulls; another owner's 404, admin may). e2e `stats.spec.ts`, 5
  tests (all red on the old workspace): tiles, budget and four rendered charts; empty data; the no-price note with
  tokens charted and a warning budget; Refresh; phone width. Suites .NET 529/529, e2e 132/132.
- **Human test** (throwaway Testing server on a scratch copy): `waver`'s tab read 8 tasks, 8 done, 100% success,
  coding 6 / javascript 1 / test 1, 98 calls, 592 498 tokens, $0 with the pricing note, no budget — the same as its
  task rows and usage records read straight from the database; the average task, 10 s over 8 timed, matches its eight
  plan files (9.6 s). Light and dark at 1400px and 375px: no horizontal scroll, no console errors. The test led to two
  changes: flat $0 cost charts now chart tokens, and the status chart leaves out empty statuses (seven labels overlapped
  at 375px).
