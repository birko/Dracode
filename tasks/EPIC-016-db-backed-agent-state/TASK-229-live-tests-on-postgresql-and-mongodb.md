---
id: TASK-229
parent: EPIC-016
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-26
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-200, TASK-227, TASK-228]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Stores work on any database (4/4): live tests on PostgreSQL and MongoDB, and the human test

## Context

The last slice of TASK-200 (split 2026-11-26). Decided in the grill: PostgreSQL tests follow Birko's live-test convention
(`BIRKO_PG_HOST`, user, password, port; skipped when unset; `BIRKO_REQUIRE_LIVE` makes them required), against a local
container in Docker Desktop. The owner chose Birko's unified factory, so MongoDB is covered the same way. This slice is
also Birko TASK-988's and TASK-990's human test (`birko-framework-44` waits for pass/fail).

## Acceptance criteria

- [x] Live tests prove every store opens, writes and reads back on PostgreSQL when `BIRKO_PG_HOST` is set, and on
      MongoDB when its host variable is set; skipped otherwise; the full suite still passes on SQLite
- [x] CLAUDE.md says how to configure each database, keeping the SQLite-only rules (WAL, the three-file backup) marked
      as SQLite's
- [x] Birko gets pass/fail for TASK-988 and TASK-990, naming the backends run

## Human test plan

- [x] A throwaway server on a local PostgreSQL: projects, Dragon history, layouts, budgets and the cost report work and
      survive a restart; the same on SQLite; the same on MongoDB if one can be started (specification history only once
      Birko's NoSQL event store exists)

## Implementation plan

1. `EveryStoreOnEveryDatabaseTests`: one theory over SqLite, InMemory, PostgreSQL, MongoDB. Every store (projects, tasks,
   decisions, plans, Dragon history, layouts, budgets, usage and its aggregation, provider config, users, service keys,
   the specification event store) writes through one factory and is read back through a second on the same database.
   PostgreSQL and MongoDB follow Birko's convention (`BIRKO_PG_HOST`/`_PORT`/`_USER`/`_PASSWORD`,
   `BIRKO_MONGO_HOST`/`_PORT`; skipped unset, failed under `BIRKO_REQUIRE_LIVE`), each on a fresh database dropped after.
2. CLAUDE.md § Which database: how to configure each database (JSON for all three), what each creates itself, the live
   test and its variables; the WAL section is titled SQLite only.
3. Human test on a throwaway server per database, then pass/fail to `birko-framework-44`.

## Notes

- 2026-11-28: done. Full suite 760 passed. The live test with `BIRKO_REQUIRE_LIVE=1` against Birko's containers
  (`birko-live-pg` 5432, `birko-live-mongo-standalone` 27018) passed 4/4 on the first run; without the variables the live
  cases skip, and require-live without hosts fails them, as the convention says. No leftover databases.
- Human test, a throwaway server per database (Testing; run from a scratch folder with `ASPNETCORE_CONTENTROOT` set to
  the server project, so the real `user-settings.json` was not read; the LAN vLLM model for Dragon, its key read from
  the environment): a project, a layout, budgets and a Dragon home message written; after a restart each read back. All
  pass on SQLite, PostgreSQL and MongoDB. On PostgreSQL Dragon's model calls left 3 `usage_records` rows; the budgets
  page's spend read 0 because no pricing is configured. Specification history was not exercised through the server (no
  REST route records it); the live test covers it on every database.
- Birko told PASS for TASK-988 and TASK-990 on SQLite, PostgreSQL 16, MongoDB 7 and InMemory, with one observation: on
  MongoDB the collections take the entity class names (`ProjectEntity`), not the SQL table names.
- The OAuth stores still live in the SQLite file under `ProjectsPath` on every database (TASK-230).
