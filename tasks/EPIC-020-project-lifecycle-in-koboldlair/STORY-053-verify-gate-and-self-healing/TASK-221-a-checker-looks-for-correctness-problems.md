---
id: TASK-221
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-217]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# A checker looks for correctness problems

## Context

FEATURE-078 D8. A correctness review of the task's changes.

Decisions: FEATURE-078 D8.

## Acceptance criteria

- [x] The checker reports likely bugs in the change with the line and the failure it would cause

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A change with an obvious bug is held with a finding

## Implementation plan

1. Move the intent checker's file gathering into a shared `Services/Checks/TaskMaterial.cs` (`TouchedFilesAsync`,
   `AppendFilesAsync`, same caps and workspace guard), and add `DiffAsync`: `git diff HEAD -- .` in the workspace,
   capped at 24k chars, null outside git or with no edits. `IntentChecker` uses it unchanged.
2. `CorrectnessChecker : ITaskChecker` (name "correctness", cost 20, so it runs after the intent check; no model →
   throws, so the task goes to a person). Material: the diff, then the touched files.
3. `CorrectnessCheckerAgent : CheckerAgent`: process traces each changed block's inputs and states and what it calls;
   rules: a `bug` names file, line, input and failure; only the change; not style, performance, missing tests or missing
   features; no finding without a statable failure; nothing the files cannot show.
4. `Program.cs`: registered after the intent checker, the Kobold model tracked as `correctness-check` (one
   `checkModel(trackedAs)` helper for both).

## Notes

- Red first: 5 tests in `CorrectnessCheckerTests` (the diff and touched files in git; files outside git; a bug finding
  with file and line in the shared shape; cost after intent and a clean pass; no model throws). The intent checker's
  tests pass unchanged after the move to `TaskMaterial`. Full suite: 752 passed.
- Human test (2026-11-27), scratch harness with a real Drake, both checkers on the live model (vLLM `finstat`), a git
  workspace whose uncommitted edit adds `average()` and a command line that crashes with no arguments (the task says
  nothing about empty input). The intent check passed; the correctness check held the task: reopened (attempt 1) with
  "[correctness] crash: average([]) divides by zero. Running `python stats.py` with no arguments … raises
  ZeroDivisionError" and evidence `stats.py:6` and `stats.py:9`. The same change with a usage exit got 0 findings.
- The model named its finding's kind "crash" rather than "bug": the kind is the checker's own word (`TaskFinding`), and
  nothing reads it beyond showing it.
- Not seen on a live server run; the registration is the same as the intent checker's.
