---
id: TASK-081
parent: null
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Decide whether DraCode.KoboldLair needs its own Microsoft.Data.Sqlite reference

## Context

Spawned from TASK-076's Out of scope at its sign-off. `DraCode.KoboldLair` declares
`Microsoft.Data.Sqlite` (now `10.*`) but no `.cs` file in that project references the package; it
also reaches it through its `ProjectReference` to `DraCode.Birko`. TASK-076 raised the version
instead of deleting the reference, because a SQLite provider's native assets (`SQLitePCLRaw`) are
not visible to a grep for type names. This task settles whether the explicit reference is needed.

## Acceptance criteria

- [ ] Remove the reference on a branch and confirm `dotnet build` + the full test suite pass
- [ ] Confirm the native SQLite assets still land in the Server's output and the Server starts and opens its SQLite database
- [ ] Either the reference is deleted, or it stays with a one-line reason next to it

## Out of scope

- Any other package-version alignment (TASK-076 covered that)

## Human test plan

N/A — the build, the test suite and a Server start against SQLite are all mechanical checks.

## Implementation plan

_Populated by `/tasks plan TASK-081` — leave empty until then._