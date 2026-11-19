---
id: TASK-178
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-19
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-028]
pr: null
github-issue: null
jira-key: null
---

# Verification fix tasks are never run, and new ones are written every cycle

## Context

Found 2026-11-19 while running TASK-025's human test plan against a real, throwaway server (Testing environment,
scratch DB, LAN `finstat` model). A finished project (`waver`) failed `WyvernVerificationService`'s toolchain checks.
For the next hour and a half the log repeated, about once a minute: "Created fix task verify-syntax-… / verify-test-… /
verify-lint-…", "Verification failed for waver, created fix tasks", "All tasks completed - project awaiting
verification!". No fix task ever ran. `verification-fixes-tasks.json` held `[]` and the `.md` an empty table.

**Mechanism.** `WyvernVerificationService.CreateFixTasksAsync` writes only `verification-fixes-tasks.md`, as a
four-column table whose header is `| ID | Task | Agent | Status |`. `TaskTracker.LoadFromMarkdown` starts a table only
at a line beginning `| Task |`, so it reads zero tasks. The Drake created for the area (`waver:verification-fixes`)
then sees no work, saves its empty tracker (`[]` in the `.json`, an empty table in the `.md`), and the project goes
back to AwaitingVerification. The next verification run finds no earlier `verify-<type>-` ids in the rewritten file, so
its duplicate guard passes and the same fix tasks are written again.

**Possibly the same loss elsewhere.** In the same run a Wyvern task file (`module-implementation-tasks.json` of project
`waver2`) also became `[]` after a restart, while the database row still held the task, so Drake saw nothing to do and
the REST API showed it Unassigned. The file had been edited by hand (an `Exports:` clause added to the task text, as the
test needed) before that restart, so the edit may be the cause; not shown either way. A tracker that loads zero tasks
from a file that had some, and then saves, destroys them in both cases.

## Acceptance criteria

- [ ] A fix task written by `WyvernVerificationService` is loaded by Drake and summoned, shown by a test
- [ ] A verification run that fails again does not add a second fix task for a check type whose fix task is still open
- [ ] A Drake whose task file loads zero tasks does not overwrite a non-empty file (or the database's tasks) with an
      empty list, shown by a test
- [ ] The full suite passes

## Out of scope

- What the toolchain checks themselves run, and whether they should run on a project with no build at all.

## Human test plan

- [ ] On a throwaway server, let a finished project fail verification; confirm its fix tasks run once, and that the
      log does not repeat "created fix tasks" every cycle.

## Implementation plan

_Populated by `/tasks plan TASK-178` — leave empty until then._
