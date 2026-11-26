---
id: TASK-229
parent: EPIC-016
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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

- [ ] Live tests prove every store opens, writes and reads back on PostgreSQL when `BIRKO_PG_HOST` is set, and on
      MongoDB when its host variable is set; skipped otherwise; the full suite still passes on SQLite
- [ ] CLAUDE.md says how to configure each database, keeping the SQLite-only rules (WAL, the three-file backup) marked
      as SQLite's
- [ ] Birko gets pass/fail for TASK-988 and TASK-990, naming the backends run

## Human test plan

- [ ] A throwaway server on a local PostgreSQL: projects, Dragon history, layouts, budgets and the cost report work and
      survive a restart; the same on SQLite; the same on MongoDB if one can be started (specification history only once
      Birko's NoSQL event store exists)

## Implementation plan

_Populated by `/tasks plan TASK-229` — leave empty until then._
