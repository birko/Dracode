---
id: TASK-181
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-21
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-031]
pr: null
github-issue: null
jira-key: null
---

# DragonRequestQueue.Dispose throws when it stops its workers

## Context

Found 2026-11-21 while writing TASK-151's `DragonConversationTests`. `DragonRequestQueue.Dispose()` cancels its
shutdown token and then waits on the processing task, which ends cancelled, so `Dispose` throws
`AggregateException(TaskCanceledException)` (`DragonRequestQueue.cs`, the `Wait` in `Dispose`). The tests swallow it
(`QueueScope`); in the server it surfaces when `DragonService` is disposed at shutdown, as noise or as a failed
graceful shutdown.

## Acceptance criteria

- [ ] Disposing a queue with idle workers, and one with a request running, completes without throwing (a test that
      fails today); a running request's reply is not sent after disposal
- [ ] `DragonConversationTests.QueueScope` no longer needs to swallow the exception

## Human test plan

- [ ] Stop a throwaway server with Ctrl+C after a Dragon conversation: no exception from the queue in the log

## Implementation plan

_Populated by `/tasks plan TASK-181` — leave empty until then._
