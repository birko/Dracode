---
id: TASK-163
parent: STORY-049
feature: FEATURE-026
status: done
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

- [x] An admin reads and changes the budgets; the change holds after a restart; a non-admin gets 403
- [x] The tab shows each budget beside current spend, warning colour past the threshold
- [x] Budget enforcement uses the changed values
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Per-project budget overrides (not decided)

## Human test plan

- [x] Lower the daily budget below today's spend: the next LLM call is blocked and the tab shows it over budget

## Implementation plan

1. Red first:
   - `BudgetEndpointsTests` (9 tests):
     - an admin reads appsettings' budgets with today's and this month's spend and the top project;
     - a change is enforced at once by `CostTrackingService` and still holds after a restart over appsettings;
     - without `manage_config`, reading and changing are 403;
     - out-of-range values are 400;
     - the capabilities offer budgets to admins only.
   - `tests/ui-e2e/budgets.spec.ts` (6 tests).
2. Store: `budget_settings` (`BudgetSettingsEntity`, `SqlBudgetRepository`), one row on the shared SQLite database.
   It is covered by `SqliteJournalModeTests` like every `Sql*Repository`.
3. `BudgetSettingsService` writes the stored values over `KoboldLair:CostTracking:Budget`'s live object at startup and
   after each saved change. `CostTrackingService` checks that same object before every LLM call, so a change is
   enforced from the next call, and the stats read the same values.
   - The service is built before `CostTrackingService` and right after `Build()`, so the stored values are in force
     from the start.
   - Spend uses the windows enforcement uses: today and this month in UTC, and a project's whole life.
4. `GET`/`PUT /api/v1/budgets` (`manage_config`). The PUT takes all four values: 0 means none, and the threshold is
   1–100. A server without SQLite answers 409. `canManageBudgets` is added to the Settings capabilities.
5. Settings → Budgets tab (`budgets-tab.ts`, route `#/settings/budgets`, admins only):
   - Each budget is shown beside its spend, in the warning colour from the threshold and the danger colour, marked
     "Over budget", from 100%.
   - A `b-form` with Save, which shows the server's refusal as it is.
   - Without a store, the tab says why there is no form.

## Notes

- The human test found a real defect, fixed here, with a regression test in `Data/DatabasePathTests.cs`.
  - The cause: `DataStorageConfig.ProjectsPath` was set from `KoboldLair:ProjectsPath` only inside the
    `IProjectRepository` factory, so every SQL store built before that factory opened `./projects/<db>` instead.
  - How the new service hit it: it is built right after `Build()`, which builds the usage store early. On the
    throwaway server, usage and budgets went to `DraCode.KoboldLair.Server/projects/koboldlair-test.db`. In production
    `appsettings.local.json` sets only `KoboldLair:ProjectsPath`, so it would have split the real database the same
    way.
  - The fix: a `PostConfigure` sets the path at configuration time. That throwaway database, created by the test run,
    was deleted.
  - It also gives each server test that sets only `KoboldLair:ProjectsPath` its own database. Before, they shared
    `./projects/koboldlair-test.db` in the test output folder.
- Human test on a throwaway server (a copy of `greeter175`, with one $0.50 usage record seeded for today, since the LAN
  model's calls are recorded at $0):
  - Before: "No daily budget. $0.5000 spent today."
  - Saving a $0.25 daily budget in the tab showed "$0.5000 of $0.2500 today (200%)", Over budget, in the danger colour.
  - Dragon's next message was refused: "Budget exceeded (daily): $0,50 / $0,25", in the dock and in the server log.
  - After a restart, the stored row (0.25, `owner-175`) and the tab still said the same.
- Birko's budget message formats dollars with the server's culture ("$0,50" on a Slovak machine). That is in
  Birko.AI.Resilience; not filed.
- Full .NET suite: 632 passed. Full e2e suite: 168 passed. `tsc --noEmit` reports the same 32 errors as before, all in
  Birko.Web's sources.
