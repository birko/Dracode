---
id: TASK-200
parent: EPIC-016
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Stores work on any database: SQLite for tests, PostgreSQL for production

## Context

Split from TASK-123 on 2026-11-25. The owner wants PostgreSQL in production, with SQLite kept for tests, so every store
has to work whichever database is configured.

- **Today the stores are SQLite or nothing.** DraCode builds `AsyncSqLiteModelRepository` /
  `SqliteDatabaseSettings.For` directly in 38 places (the `Sql*Repository` classes, the event store, the OAuth stores).
  `Program.cs` registers ten of them as `null!` when `KoboldLair:Data:DefaultBackend` is not SQLite: usage, budgets,
  layouts, users, tasks, plans, pending decisions, the event store, spec events, Dragon history. Every consumer treats
  them as optional, so on another backend these features quietly go away. A new non-nullable consumer would get a
  `NullReferenceException` far from the cause. This was TASK-123's `null!` finding.
- **Birko.Data has the providers.** `Birko.Data.SQL.PostgreSQL` (and MySQL, MSSql) offer the same repository and store
  API as SQLite, including `AggregateAsync` (SQL `GROUP BY`).
- **SQLite-only rules exist.** WAL mode (TASK-177, `SqliteJournalModeTests`), one connector per file, and "a backup takes
  all three files" are SQLite's. They must stay true on SQLite and not be assumed elsewhere.
- **Test hosts choose the backend late.** 14 test hosts switch to the JSON backend through `ConfigureAppConfiguration`,
  which `Program.cs` sees only after `Build()`. Any registration-time decision has to account for that (TASK-163's
  comment on the service-key registry).

## Acceptance criteria

- [ ] One database setting picks the provider and connection (SQLite file, or a PostgreSQL connection string); every
      store is built from it, with no store built against SQLite directly
- [ ] No store is registered as `null!`: each is registered for every supported backend
- [ ] The test suite runs on SQLite, and a test proves the stores open and round-trip on PostgreSQL (or the provider
      seam, if a PostgreSQL server is not available to the tests — decided in the grill)
- [ ] CLAUDE.md says how to configure each database, and keeps the SQLite-only rules marked as SQLite's

## Out of scope

- Moving file-based working state into the database (the rest of EPIC-016)

## Open questions (grill first)

- Keep the JSON backend at all, or make a database required?
- How existing SQLite data moves to PostgreSQL (a one-off export/import, or a migration command)?
- Schema creation and changes on PostgreSQL (`CreateSchemaAsync` as today, or migrations)?
- How the tests cover PostgreSQL: a container, a shared test server, or only the provider seam?
- The OAuth stores (`AddOAuthServerStores`) and the event store: same setting, same database?

## Human test plan

- [ ] Start a throwaway server on PostgreSQL: projects, Dragon history, layouts, budgets and the cost report all work,
      and survive a restart
- [ ] The same on SQLite: nothing changed

## Implementation plan

_Populated by `/tasks plan TASK-200` — after the grill._
