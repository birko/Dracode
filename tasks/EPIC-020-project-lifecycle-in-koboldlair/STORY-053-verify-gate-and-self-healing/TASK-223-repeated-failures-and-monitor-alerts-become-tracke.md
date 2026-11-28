---
id: TASK-223
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Repeated failures and monitor alerts become tracked fix tasks, worked by impact

## Context

FEATURE-078 D14. Repeated failures and monitor alerts are retried today; they are filed as tracked fix tasks and
worked through in order of impact.

Decisions: FEATURE-078 D14.

## Acceptance criteria

- [x] A failure that repeats, or a monitor alert, files a fix task naming what failed
- [x] Fix tasks are picked by impact before other work of the same priority
- [x] The same failure does not file a second open fix task

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A task that fails repeatedly on a throwaway server leaves one fix task behind

## Implementation plan

Decided with the owner 2026-11-28 (all four recommended answers):
- **Which failures:** recovery gave up — a permanent error, or the retries used up. Acceptance failures keep their own
  fix task (TASK-025/222); a failing fix task gets none.
- **Monitor alerts:** the first alert of a kind on a task takes its automatic route as before; the same kind again files
  a fix task as well.
- **Impact:** how many tasks wait on the failed task, directly or through others; within a priority, fix tasks first,
  highest impact first.
- **After the fix:** the failed task runs again, its retries cleared.

Steps:
1. Red: `FixTaskTests` (11 cases) — filing and de-duplication, ordering, reopening, `GaveUpReason`, `RepeatedKind`, and a
   repeated alert routed to the Drake holding the task. Failed to compile against the old code.
2. `TaskRecord.FixesTaskId` / `FixKind` (and on `TaskEntity` and the mapper; the tasks table gains the columns at
   startup through the existing column repair).
3. `Drake.FileFixTaskAsync` (by task or id), `ReopenFixedTasks` at the start of `GetUnassignedTasks`, `FixImpact` in
   `SortTasksByPriority`. A task blocked by failed dependencies is unblocked once none of them is failed any more
   (before: only once all were done, so a dependency sent back to run left it blocked).
4. `FailureRecoveryJob.GaveUpReason`, called for each task the job skips; `ReasoningMonitorService.RepeatedKind`, passed to
   `RouteToHoldingDrakeAsync`, which files the fix through the Drake holding the task before routing the alert.
5. CLAUDE.md (Failure Recovery, ReasoningMonitorService).

## Notes

- 2026-11-28: done. Full suite 771 passed.
- Human test, a scratch harness (the real `FailureRecoveryJob`, `DrakeFactory`, `ProjectService` and task file; no
  server, since forcing a real Kobold to fail six times through the backoff would take an hour): `[api-1]` failed with a
  transient error and `[api-2]` waited on it. Each job cycle retried it (each run's failure applied by hand, the backoff
  wait skipped); on the sixth cycle recovery gave up and filed one fix task, "Fix what made task api-1 (…) fail
  (failure): it failed 6 times; the last error: Network timeout…". Three more cycles filed nothing. The fix was in the
  task file on disk and the only ready task. Marked Done: api-1 went back to Unassigned with 0 retries, api-2 from
  BlockedByFailure to Unassigned.
- Found, not fixed (the owner kept acceptance failures as they are): the acceptance check's own fix task (TASK-025)
  quotes "Original task: [token] …", and dependencies are matched by "[token]" in done tasks' text, so once that fix is
  Done the failed task's dependents count it as done. The new fix tasks strip the token for this reason. Filed as
  TASK-231 (P2, STORY-042).
