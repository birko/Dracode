---
id: TASK-166
parent: STORY-042
feature: null
status: todo
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

- [ ] A Failed task and a BlockedByFailure task keep their status through `GenerateMarkdown` → `LoadFromMarkdown`
- [ ] A test pins both, red on the current code
- [ ] Every value of `TaskStatus` round-trips through the markdown file (one test over the enum, so a status added later cannot slip again)
- [ ] The Context's open question is answered in the task: whether a Failed task was actually re-run, and by which path

## Out of scope

- The `error` word, which reads as Done by design (a task that finished with an error message)

## Human test plan

N/A — the round trip is a pure read/write of a file, fully asserted by the tests above.

## Implementation plan

_Populated by `/tasks plan TASK-166` — leave empty until then._
