---
id: TASK-163
parent: STORY-049
feature: FEATURE-026
status: todo
priority: P3
assignee: agent
created: 2026-11-16
depends-on: [TASK-161]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Settings: budgets

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Budgets (daily, monthly, per project, warning threshold) come from `appsettings` (`KoboldLair:CostTracking:Budget`)
and can only be read (inside `get_metrics`). This task adds admin-only `GET`/`PUT /api/v1/budgets`, stored so a change
survives a restart and overrides the file (the plan picks the store), and a Budgets tab showing each budget with this
period's spend.

Decisions: FEATURE-026 D20.

## Acceptance criteria

- [ ] An admin reads and changes the budgets; the change holds after a restart; a non-admin gets 403
- [ ] The tab shows each budget beside current spend, warning colour past the threshold
- [ ] Budget enforcement uses the changed values
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Per-project budget overrides (not decided)

## Human test plan

- [ ] Lower the daily budget below today's spend: the next LLM call is blocked and the tab shows it over budget

## Implementation plan

_Populated by `/tasks plan TASK-163` — leave empty until then._
