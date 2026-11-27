---
id: TASK-227
parent: EPIC-016
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
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
pr: 83
github-issue: null
jira-key: null
---

# Stores work on any database (2/4): drop the JSON backend and the `null!` stores

## Context

The second slice of TASK-200 (split 2026-11-26). Decided in TASK-200's grill: the JSON backend is dropped and a database
is required. `Program.cs` registers ten stores as `null!` when `KoboldLair:Data:DefaultBackend` is not SQLite (TASK-123's
finding, moved to TASK-200); `StorageBackend` is checked in 19 places; 14 test hosts run on `JsonFile`.

## Acceptance criteria

- [x] No store is registered as `null!`: every store is registered from the store factory (TASK-200) on any backend
- [x] The JSON backend is gone (`StorageBackend.JsonFile`, the JSON `ProjectRepository` path, the `DefaultBackend`
      checks); a configuration still naming it fails at startup with a message saying what to set instead
- [x] The 14 test hosts on `JsonFile` run on a temporary SQLite file; the full suite passes
- [x] CLAUDE.md no longer describes a JSON backend

## Human test plan

- [x] A throwaway server with no database setting starts on SQLite as before; one still naming `JsonFile` refuses to
      start with a clear message

## Implementation plan

1. Red: `HostStartupTests` — with no database setting every store is registered on SQLite (the database file exists);
   a host still naming `JsonFile` refuses to start, naming `JsonFile` and `KoboldLair:Data:Provider`.
2. `RepositoryFactory`: drop `StorageBackend`; `DataStorageConfig.DefaultBackend` becomes a string kept only to refuse
   anything but `SqLite` (`EnsureSupported`), called by `StoreFactoryFor` and validated on start.
3. `Program.cs`: register every store unconditionally from the factory (no `null!`), remove the JSON
   `ProjectRepository` registration and the dead circuit-breaker block; OAuth stores, service keys and the event store
   always on the SQLite file (TASK-228 moves them); the `projects.json` import stays, unconditional.
4. Tests: drop `DefaultBackend` from every test host (14 on `JsonFile`, the rest `SqLite`), each already on its own temp
   `ProjectsPath`; remove the layouts-without-SQLite test; drop `DefaultBackend` from appsettings.
5. CLAUDE.md: no JSON backend, the refusal, parking and layouts no longer "SQLite only".

## Notes

- The JSON `ProjectRepository` class stays: tests use it as a light `IProjectRepository`, and the server no longer
  registers it. `JsonToSqlMigration` (moving an old `projects.json` into the database) stays and now runs on every
  backend.
- `DefaultBackend: SqLite` is still accepted (it names the default), so an `appsettings.local.json` that sets it keeps
  working; any other value stops the server before it listens.
- Null checks for the stores (endpoints answering 409, parking refused) are left in the services: the server never hits
  them now, but services built without a store in tests still do.
- Full suite 706 passed. Human test on the throwaway `projects193`: started with no database setting ("Project repository
  on SqLite"; projects, stats, needs-you, layout PUT/GET all 200/204); with `KoboldLair__Data__DefaultBackend=JsonFile` it
  stopped before listening with the message naming `KoboldLair:Data:Provider`.
