---
id: TASK-046
parent: STORY-016
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-042]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# agents/active + cost-report endpoints

## Context

Read-only operational endpoints over existing services: `GET /api/v1/agents/active` (running Drakes/Kobolds, from the agent-status machinery) and `GET /api/v1/cost-report?period=daily|monthly` (over the existing cost-tracking `SqlUsageRepository` / `view_cost_report` logic).

## Acceptance criteria

- [ ] `GET /api/v1/agents/active` returns currently running Drakes/Kobolds (wraps existing agent-status service)
- [ ] `GET /api/v1/cost-report?period=daily|monthly` returns usage/cost (wraps existing cost-tracking)
- [ ] Both behind auth; scoping applied where ownership is relevant
- [ ] Tests: shapes match the existing tool outputs

## Out of scope

- New cost-tracking logic (reuse existing)

## Human test plan

- [ ] N/A — fully covered by automated tests

## Implementation plan

_Populated by `/tasks plan TASK-046` — leave empty until then._
