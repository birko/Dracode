---
id: TASK-231
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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

- [ ] A Done acceptance fix task never satisfies a dependency on the task it fixes; a test pins it, red today
- [ ] Decide and state what a finished acceptance fix does to the failed task: rerun it as TASK-223's fix tasks do (the
      recommendation: then the acceptance fix is just a fix task of kind `acceptance`), or leave it failed
- [ ] Dependency matching does not take a tag quoted inside another task's text as that task (a tag counts only at the
      start of a task's text), so no other quoted text can do this either

## Human test plan

- [ ] In a harness or on a throwaway server: a task whose acceptance check fails for good, with a task depending on it;
      finish the fix task and see the dependent stay blocked (or run only after the failed task reran and passed)

## Implementation plan

_Populated by `/tasks plan TASK-231` — leave empty until then._

## Notes
