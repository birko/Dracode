---
id: TASK-196
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-24
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-042]
pr: null
github-issue: null
jira-key: null
---

# A retried task of a Completed project is never run

## Context

Found 2026-11-24 while setting up TASK-182's human test (throwaway copy of the `greeter175` project). The project
was Completed. One of its tasks was set Failed and retried through `POST /api/v1/tasks/{id}/retry`, which answered
200 and reset the task to Unassigned. Nothing ever ran it: `DrakeExecutionService` only picks projects that are
Analyzed or InProgress, so a Completed project gets no Drake. The task sat Unassigned with no message until the
project was set back to InProgress by hand.

The same applies to Warden's `retry_failed_task` and the Board's Retry, which go through
`DrakeFactory.RetryFailedTaskAsync` too. The person is told the task was retried, and then it never runs.

## Acceptance criteria

- [x] Retrying a task of a project that is Completed (or otherwise past InProgress) gets it run: the project returns
      to InProgress (decided with the owner 2026-11-24: reopen, not refuse)
- [x] A test that fails today
- [x] The full suite passes

## Out of scope

- Other ways a finished project's task can change (priority changes, edits).

## Human test plan

- [x] On a throwaway server, retry a failed task of a Completed project from the Board: it runs, or the refusal says why

## Implementation plan

Decided with the owner 2026-11-24: a retry reopens the project rather than being refused.

1. Red first: four new tests in `TaskRetryTests`.
   - A retry in a project that is AwaitingVerification, Verified, Completed or Failed reopens it to InProgress and
     reports what it was.
   - An InProgress project is left alone.
   - A retry that changes nothing (the task is not Failed) does not reopen the project.
2. `DrakeFactory.ReopenFinishedProject(projectId)` sets such a project to InProgress, logs it, and returns the status
   it was reopened from. `RetryFailedTaskAsync` calls it after a retry and returns a `TaskRetry` record
   (`Outcome`, `Status`, `ReopenedFrom`). The record still deconstructs into (outcome, status), so callers are
   unchanged.
3. The callers say so:
   - The REST retry answers with `projectReopenedFrom`.
   - Warden's single retry and `retry_all` add a line saying the project is back In progress and will be verified
     again. `retry_all` resets tasks itself, so it calls `ReopenFinishedProject` directly.

## Notes

- When the retried task finishes, the usual path runs: AwaitingVerification, then verification, then Completed.
- Not covered: a retry while verification is running (AwaitingVerification). The verification service may still
  set the project Completed while the retried task runs. That is the same as any status written by two services.
- Human test on a throwaway server (copy of the `greeter175` project, LAN model, client host on 57088):
  - Playwright clicked Retry on the failed `qa-1` card on the Board. The server answered 200 with
    `projectReopenedFrom: "Completed"`, the card showed no error, and the project read InProgress.
  - Drake ran the task to Done in 27 s. The project went to AwaitingVerification, passed verification, and was
    Completed again.
  - In the same run, TASK-182's late-revision drop fired live: "Dropped escalation … the task finished while its
    plan was being revised".
- Full .NET suite: 617 passed. The e2e suite was not run: the Board's handling did not change, and the reply only
  gained a field.
