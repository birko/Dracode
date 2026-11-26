---
id: TASK-227
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

# Stores work on any database (2/4): drop the JSON backend and the `null!` stores

## Context

The second slice of TASK-200 (split 2026-11-26). Decided in TASK-200's grill: the JSON backend is dropped and a database
is required. `Program.cs` registers ten stores as `null!` when `KoboldLair:Data:DefaultBackend` is not SQLite (TASK-123's
finding, moved to TASK-200); `StorageBackend` is checked in 19 places; 14 test hosts run on `JsonFile`.

## Acceptance criteria

- [ ] No store is registered as `null!`: every store is registered from the store factory (TASK-200) on any backend
- [ ] The JSON backend is gone (`StorageBackend.JsonFile`, the JSON `ProjectRepository` path, the `DefaultBackend`
      checks); a configuration still naming it fails at startup with a message saying what to set instead
- [ ] The 14 test hosts on `JsonFile` run on a temporary SQLite file; the full suite passes
- [ ] CLAUDE.md no longer describes a JSON backend

## Human test plan

- [ ] A throwaway server with no database setting starts on SQLite as before; one still naming `JsonFile` refuses to
      start with a clear message

## Implementation plan

_Populated by `/tasks plan TASK-227` — leave empty until then._
