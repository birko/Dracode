---
id: TASK-166
parent: STORY-042
feature: null
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: []
blocks: []
findings: [FIELD-021]
pr: null
github-issue: null
jira-key: null
---

# A task file read back turns Failed and BlockedByFailure tasks into Unassigned, so they run again

## Context

Found while doing TASK-019 (2026-11-16). `TaskTracker` writes each task's status into its markdown task file as an
emoji and a word (`GenerateMarkdown`: `🔴 failed`, `🟠 blockedbyfailure`). Reading the file back
(`LoadFromMarkdown` → `ParseStatus`, `DraCode.KoboldLair/Models/Tasks/TaskTracker.cs` ~632) maps only `unassigned`,
`notinitialized`, `working`, `done`, `awaitinghumandecision` and `error`; every other word falls to the default,
`TaskStatus.Unassigned`. So a Failed task, or one blocked by a failed dependency, comes back from its file as
Unassigned, and Drake offers it as new work.

Drake reloads its tasks from the task file before every execution cycle (`ReloadTasksFromFileAsync`, called by
`DrakeExecutionService`), so this is not only a cold-start case. Whether something else (the SQL task table, the WAL)
puts the status back before the work picker runs has not been checked; that is the first thing to establish, since
it decides whether the defect is visible.

## Acceptance criteria

- [x] A Failed task and a BlockedByFailure task keep their status through `GenerateMarkdown` → `LoadFromMarkdown`
- [x] A test pins both, red on the current code
- [x] Every value of `TaskStatus` round-trips through the markdown file (one test over the enum, so a status added later cannot slip again)
- [x] The Context's open question is answered in the task: whether a Failed task was actually re-run, and by which path

## Out of scope

- The `error` word, which reads as Done by design (a task that finished with an error message)

## Human test plan

N/A — the round trip is a pure read/write of a file, fully asserted by the tests above.

## Implementation plan

1. Answer the open question from the loaders (below).
2. `ParseStatus` reads any status word through `Enum.TryParse` (the writer's words are the enum names in lower case);
   `error` still reads as Done.
3. `GenerateMarkdown` writes `error` only for a Done task with an error message; every other status keeps its word.
4. `TaskMarkdownRoundTripTests`: Failed and BlockedByFailure, and a theory over every `TaskStatus` value, with and
   without an error message.

## Notes

- **The open question:** the markdown is read only when a task file has no JSON sidecar. `TaskTracker.LoadFromFile` /
  `LoadFromFileAsync` — used by `DrakeFactory` (Drake's load and reload), the Warden tools (retry, batch, progress,
  task details) and `JsonToSqlMigration` — prefer `<area>-tasks.json` and parse the markdown only without it; Drake
  writes both on every save. So a Failed task was re-run only in a project whose task file had no sidecar (one from
  before the JSON format, or whose sidecar was lost): its next load read it Unassigned and the work picker offered it.
  No run of that kind was seen; the defect was latent on current projects.
- **Also fixed:** the writer put `error` (read back as Done) for any task with an error message except Failed and
  parked ones, so a BlockedByFailure, Unassigned or Working task with a message came back finished. Now only a Done
  task is written as `error`.
- **Fix** (`TaskTracker.ParseStatus`, `GenerateMarkdown`) and **tests** (`TaskMarkdownRoundTripTests`, 16 cases, 8 red
  on the old code: Failed and BlockedByFailure without a message, and every non-Done status with one). Suites .NET
  558/558, e2e 137/137 (twice). Human test N/A, as the task says.
