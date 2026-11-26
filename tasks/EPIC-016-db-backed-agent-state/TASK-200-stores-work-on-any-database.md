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
depends-on: [Birko.Framework/TASK-988]
blocks: [TASK-227, TASK-228, TASK-229]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Stores work on any database (1/4): one database setting, every repository built from Birko's store factory

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

Split on 2026-11-26 (the owner): this task is the first of four slices; TASK-227, TASK-228 and TASK-229 are the rest.

- [ ] One database setting (`KoboldLair:Data:Provider` and `KoboldLair:Data:ConnectionString`) builds one Birko
      `IStoreFactory` (TASK-990's unified factory); SQLite, with today's file under `ProjectsPath`, when none is given
- [ ] Every `Sql*Repository` (projects, tasks, users, plans, pending decisions, Dragon history, usage, budgets, layouts,
      provider config) gets its store from that factory; none builds a SQLite store or settings itself
- [ ] What only an SQL database has is used behind a capability check: column repair (`EnsureColumns`) only on SQL
      backends, aggregation through `IAsyncAggregatableStore`; on SQLite nothing changes (WAL, `SqliteJournalModeTests`)
- [ ] The full suite passes on SQLite as today

## Out of scope

- Dropping the JSON backend and the `null!` stores (TASK-227)
- The OAuth stores, the service-key registry and the event store (TASK-228)
- PostgreSQL and MongoDB live tests and the human test (TASK-229)
- Moving file-based working state into the database (the rest of EPIC-016)

## Decisions (grill, 2026-11-25, owner)

- **JSON backend → dropped.** A database is required; the 14 test hosts on `JsonFile` move to a temporary SQLite file.
- **Existing data → no migration.** SQLite is only for testing; production starts fresh on PostgreSQL.
- **Factory → Birko's unified `IStoreFactory` (TASK-990), not the SQL-only one (TASK-988)** (owner, 2026-11-26): SQL-only
  features sit behind capability checks; the old SQLite event-table migration stays SQLite-only; event sourcing on
  NoSQL backends is requested from Birko (TASK-228 waits on it).
- **Store creation → a provider-neutral store factory in Birko** (one interface, the provider picked from configuration);
  DraCode's stores depend only on it. Filed as Birko TASK-988 (2026-11-25, not started; its own open questions: where it lives, connection string or `SqlSettings`, the four per-provider factories); this task waits on it.
- **Schema → Birko's `CreateSchemaAsync` and `EnsureColumns`,** already provider-neutral (in the shared SQL connector),
  on both databases. No migrations framework until a change needs more than added columns.
- **PostgreSQL tests → Birko's live-test convention:** they run when `BIRKO_PG_HOST` (user, password, port) is set,
  skip otherwise, and `BIRKO_REQUIRE_LIVE` makes them required. Everything else runs on SQLite.
- **PostgreSQL for the human test and the live tests → a local container in Docker Desktop.**
- **OAuth and event stores → the same database and setting** as every other store.
- **Settings → `KoboldLair:Data:Provider`** (`SqLite` | `PostgreSQL`) **and `KoboldLair:Data:ConnectionString`.** SQLite
  defaults to today's file under `ProjectsPath` when no connection string is given.
- CLAUDE.md keeps the SQLite-only rules (WAL, the three-file backup) under a SQLite heading, with PostgreSQL's setup
  beside it.

## Human test plan

- [ ] Start a throwaway server on PostgreSQL: projects, Dragon history, layouts, budgets and the cost report all work,
      and survive a restart
- [ ] The same on SQLite: nothing changed

## Implementation plan

_Populated by `/tasks plan TASK-200` — once Birko's provider-neutral store factory is filed and its shape is known._
