---
id: TASK-196
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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

- [ ] Retrying a task of a project that is Completed (or otherwise past InProgress) gets it run: the project returns
      to InProgress, or the retry is refused with a reason the person can act on — decide which before building
- [ ] A test that fails today
- [ ] The full suite passes

## Out of scope

- Other ways a finished project's task can change (priority changes, edits).

## Human test plan

- [ ] On a throwaway server, retry a failed task of a Completed project from the Board: it runs, or the refusal says why

## Implementation plan

_Populated by `/tasks plan TASK-196` — leave empty until then._
