---
id: TASK-231
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-28
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-046]
pr: null
github-issue: null
jira-key: null
---

# A finished acceptance fix task counts as the failed task, so its dependents run on a failed dependency

## Context

Found 2026-11-28 while building TASK-223, by reading the code (not yet seen in a run).

- When the acceptance check fails a task for good (`Drake.FailAcceptanceAsync`: "Handle it automatically" on an
  escalation, or no decision store), it files a fix task whose text ends `Original task: {task.Task}`, so it carries the
  failed task's own tag, e.g. `[api-1]`.
- `Drake.GetUnassignedTasks` decides that a dependency `api-1` is met when any Done task's text contains `[api-1]`
  (`doneTasks.Any(d => d.Contains("[api-1]"))`, also across the project's other task files through
  `LoadDoneTasksFromProject`), and checks that before checking for a failed one.
- So once the fix task is Done, every task waiting on `api-1` counts it as done and starts, while `api-1` itself is still
  Failed. Nothing reruns `api-1` (the TASK-223 fix tasks rerun theirs; this one does not).
- The fix tasks TASK-223 added leave the tag out for this reason (`Drake.FileFixTaskAsync`).

## Acceptance criteria

- [x] A Done acceptance fix task never satisfies a dependency on the task it fixes; a test pins it, red today
- [x] Decide and state what a finished acceptance fix does to the failed task: rerun it as TASK-223's fix tasks do (the
      recommendation: then the acceptance fix is just a fix task of kind `acceptance`), or leave it failed
- [x] Dependency matching does not take a tag quoted inside another task's text as that task (a tag counts only at the
      start of a task's text), so no other quoted text can do this either

## Human test plan

- [x] In a harness or on a throwaway server: a task whose acceptance check fails for good, with a task depending on it;
      finish the fix task and see the dependent stay blocked (or run only after the failed task reran and passed)

## Implementation plan

Decided with the owner 2026-11-28: a finished acceptance fix reruns the failed task (the recommendation).

1. Red: `The_acceptance_fix_is_a_fix_task_for_the_failed_one_and_finishing_it_reruns_that_task` (gate tests) and
   `A_tag_quoted_inside_another_tasks_text_does_not_meet_a_dependency` (fix-task tests); plus a green guard that a
   task's own tag, in any case and after leading spaces, still meets its dependency.
2. `TaskTags.Of` / `TaskTags.Is` (Models/Tasks): a task's tag is the one its text starts with. Every dependency check uses
   it: Drake's ready-task pick (done and failed), its dependency-context lookup and dependency walk, and
   `DrakeExecutionService`'s ready check.
3. `Drake.FailAcceptanceAsync` files its fix through `FileFixTaskAsync` (kind `acceptance`, Critical for a missing
   export, else High) instead of its own text, which quoted "Original task: [tag] …". `ReopenFixedTasks` also clears
   `AcceptanceAttempts`.
4. CLAUDE.md (acceptance check, Fix tasks, a task's tag).

## Notes
- 2026-11-28: done. Full suite 774 passed.
- Human test, a scratch harness (a real Drake with the acceptance check on and no decision store, a real workspace file):
  `[api-1]` promised `Exports: refresh`, `[api-2]` depended on it. Three finishes with `refresh` missing: reopened twice,
  then Failed with one fix task (Critical, kind `acceptance`, "…the acceptance check found unmet: Export refresh is not
  declared in src/auth.ts."). Before the fix was done only the fix was ready, api-2 BlockedByFailure. Fix marked Done:
  api-1 Unassigned with attempts 0 and the only ready task; api-2 waiting, not started. api-1 rerun with `refresh`
  exported: Done, and only then api-2 ready.
- Also found while here: a reopen note's "[intent] missing: …" (TASK-218's findings) could have met a dependency on a
  task tagged `intent` the same way; the start-only rule covers it.
