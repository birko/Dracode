---
id: TASK-181
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
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
pr: 65
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

- [x] Disposing a queue with idle workers, and one with a request running, completes without throwing (a test that
      fails today); a running request's reply is not sent after disposal
- [x] `DragonConversationTests.QueueScope` no longer needs to swallow the exception

## Human test plan

- [x] Stop a throwaway server with Ctrl+C after a Dragon conversation: no exception from the queue in the log

## Implementation plan

Picked 2026-11-25:

1. Red first, `DragonRequestQueueTests`:
   - Disposing an idle queue, twice, does not throw.
   - Disposing with an answer running does not throw, returns within 2 s, and nothing more is sent on the socket
     after it.
   - `DragonConversationTests.QueueScope` stops swallowing `AggregateException`.
   - Red: both new tests and the two conversation tests failed with the aggregated `TaskCanceledException`.
2. `DragonRequestQueue`:
   - The workers catch the shutdown's `OperationCanceledException` and end, so the processing task completes rather
     than being cancelled.
   - `Dispose` marks the queue stopping and cancels waiting requests first. After that, every `Send*` returns at once.
   - A worker whose request is running when the queue stops does not wait it out. `ContinueSessionAsync` takes no
     token, so the answer runs to its end, sends nothing, and any exception is observed.
   - The concurrency limiter is disposed only once the workers have ended, since a late `Release` would throw.

## Notes

- .NET suite 649 passed. No client change, so no e2e run.
- **Human test** (throwaway server, `projects193`): a Dragon conversation, then Ctrl+C while a 600-word streamed reply
  was being written. The host waited for that in-flight reply, so the queue was disposed with no answer running
  (that case is covered by the test). The log showed "Graceful shutdown complete", then "DragonRequestQueue
  processing stopped", and no exception.
- The same run was Birko TASK-987's human test: streamed Dragon replies through vLLM now record their usage
  (`stream_options.include_usage`, Birko.Framework main e4a18826), including the reply cut by the shutdown. The
  result went to `birko-framework-44`.
