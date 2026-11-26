---
id: TASK-228
parent: EPIC-016
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-26
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-200]
blocks: [TASK-229]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Stores work on any database (3/4): OAuth stores, service keys and the event store on the store factory

## Context

The third slice of TASK-200 (split 2026-11-26). Decided in the grill: the OAuth and event stores use the same database
and setting as every other store. Today `SqliteOAuthStores`, `ServiceKeyRegistry`, `OAuthClientNames` and the
specification event store (Birko.Data.EventSourcing.SQL, with `LegacyDomainEventMigration`) are built against SQLite.

Event sourcing on NoSQL backends (MongoDB at least) does not exist in Birko; filed as Birko TASK-995 (P2, todo) by `birko-framework-44` on
2026-11-26 (the owner's call). Until it lands, the event store works on SQL backends only.

## Acceptance criteria

- [ ] The OAuth stores, the service-key registry and OAuth client names are built from the store factory
- [ ] The specification event store comes from the configured backend: SQL backends through Birko's SQL event store,
      NoSQL ones through Birko's NoSQL event store once it exists (stated as unavailable until then)
- [ ] `LegacyDomainEventMigration` runs only on SQLite, where the old table can exist
- [ ] The concurrency test (two writers on one specification land on distinct versions) passes

## Human test plan

- [ ] Sign in with an API key and change a specification on a throwaway server: both work as before on SQLite

## Implementation plan

_Populated by `/tasks plan TASK-228` — leave empty until then._
