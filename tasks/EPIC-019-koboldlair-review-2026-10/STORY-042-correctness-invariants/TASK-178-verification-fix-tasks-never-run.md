---
id: TASK-178
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
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

- [x] A fix task written by `WyvernVerificationService` is loaded by Drake and summoned, shown by a test
- [x] A verification run that fails again does not add a second fix task for a check type whose fix task is still open
- [x] A Drake whose task file loads zero tasks does not overwrite a non-empty file (or the database's tasks) with an
      empty list, shown by a test
- [x] The full suite passes

## Out of scope

- What the toolchain checks themselves run, and whether they should run on a project with no build at all.

## Human test plan

- [x] On a throwaway server, let a finished project fail verification; confirm its fix tasks run once, and that the
      log does not repeat "created fix tasks" every cycle.

## Notes

- `DraCode.KoboldLair/Services/VerificationFixTasks.cs`: `AddAsync` writes the fix tasks through `TaskTracker` (JSON plus
  markdown, GUID ids, the check's priority), as Wyvern writes any area, and skips a check type whose fix task
  (`Fix <type> failure:`) is not Done. `WyvernVerificationService.CreateFixTasksAsync` calls it and registers the
  `verification-fixes` area as before.
- `Drake.WriteTaskFileAsync`: every save (debounced, immediate, flush, WAL recovery) goes through it, and it does not
  write an empty tracker over a file `TaskTracker.FileHoldsTasks` says holds tasks (a non-empty or unparseable JSON, or a
  markdown table with rows and no JSON); the WAL is kept then. Drake never removes tasks, so that save could only
  destroy ones it failed to load. Database rows are never deleted by a save (only project deletion deletes them).
- `VerificationFixTasksTests` (6): written fix tasks load in a Drake with ids and priority and a Kobold is summoned; a
  failing re-run after a Drake reload/save adds no second fix for an open check; a Done fix gets a new one; a Drake
  that loads nothing from the old four-column markdown, or from unparseable JSON, leaves the file as it was; an empty
  file is still rewritten. With the guard off, the two guard tests fail.
- `SqliteJournalModeTests` now run in a collection of their own (`DisableParallelization`): since Birko TASK-941 the
  rollback test failed in 2 of 3 full runs, because another class's `ClearAllPools()` dropped the stale handle and a
  fresh one waited out the busy timeout on the write lock. Full suite 480/480, three runs in a row.
- Human test, 2026-11-19, throwaway server on scratch `projects025` with the LAN `finstat` model: `waver` failed
  verification, three fix tasks were created once (syntax, test, lint), Drake `waver:verification-fixes` loaded all
  three, added them to the database, and each was summoned and finished. "Created fix task" was logged once per failing
  verification, not every cycle. The next verification failed again for reasons outside this task (checks run in the
  project folder, not `workspace/`, and POSIX commands run through PowerShell), so a second round of three was created
  for the Done fixes; filed as TASK-179, server stopped after two rounds.
