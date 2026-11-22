---
id: TASK-187
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-034]
pr: 35
github-issue: null
jira-key: null
---

# A message sent while Dragon is still answering cancels that answer, and Dragon replies without it

## Context

Found 2026-11-22 in TASK-149's human test. `DragonRequestQueue.EnqueueAsync` calls `CancelSessionRequestAsync` before
queueing, so a second message cancels the session's running request. The cancelled request's turn never reaches Dragon's
LLM context, and the next reply is written as if the first message had not been sent ("you haven't told me which one
you want"). Work already started by the cancelled request (a Sage delegation) may still finish and write files.

The web dock keeps its input enabled while Dragon is typing (home and project dock alike), so a person can do this by
sending a follow-up before the reply ends; a streamed preamble ("I'll hand this to Sage…") looks like a finished reply.

## Acceptance criteria

- [x] A message sent while Dragon is answering is answered after the running reply, with both messages in Dragon's
      context — queued per session rather than cancelling — proved by a test that fails today
- [x] Whatever the server does, the dock shows it: the second message is visibly waiting, never silently dropped (e2e)
- [x] An explicit cancel, if one is kept, is its own message type and is not triggered by sending a message

## Out of scope

- Concurrency limits across sessions (`maxConcurrentRequests`)

## Human test plan

- [x] On a throwaway server: ask Dragon something that delegates to Sage, send a follow-up before it answers — both
      are answered, the second taking the first into account
  - **Passed 2026-11-22:** see Notes

## Implementation plan

1. `DragonRequestQueue`: a session's requests run one at a time, in order. A request whose session still has one
   waiting or running joins the channel when that one finishes (`DragonRequest.Finished`), so no worker blocks and other
   sessions are not held up. Enqueueing no longer cancels; the cancel command drops only requests not yet started.
2. Client: count the messages in flight from `dragon_status` `complete`/`error`; mark a message sent behind another as
   waiting (`status: 'sending'`) and clear it when its turn comes.
3. Tests: queue tests with a slow fake provider (red today); an e2e for the waiting mark.

## Notes

- **Cause, as found:** `EnqueueAsync` called `CancelSessionRequestAsync` first, but `DragonAgent.ContinueSessionAsync`
  takes no cancellation, so a running request was never stopped — the new one ran beside it on the same Dragon, and
  `ContinueSessionAsync` writes `_conversationHistory` back when it ends, so whichever finished last erased the other's
  turn. A request still waiting in the channel was skipped as cancelled, its message dropped.
- **Server** (`DragonRequestQueue`, `DragonRequest.Finished`): per-session order as planned. `_lastBySession` holds
  each session's newest request; one behind an unfinished request is written to the channel by a continuation of that
  request's `Finished`. `Finish` runs for answered, failed, timed-out (after the Dragon call really ends) and skipped
  requests. The per-session `_cancellationTokens` map is gone; `CancelSessionRequestAsync` (the cancel command)
  cancels the session's requests not yet started.
- **Client** (`DragonConnection`): `_inFlight` / `_waiting`; a message sent while one is in flight is shown with
  b-chat's `sending` mark ("Sending...") and the line "Your message waits until Dragon finishes this reply" (Dragon's
  activity lines replace it while it works). `kobold-dragon-dock` redraws the list when the last message's mark changes
  (b-chat's in-place update redraws only the text).
- **Tests:** `DragonRequestQueueTests` (3, all red on the old queue): two messages answered in order with the first
  exchange in the second call's context and never two calls at once; another session answered while a busy one works;
  the cancel command drops a waiting message and the running one finishes. The tests do not dispose the queue
  (TASK-181). e2e `dragon-dock.spec.ts` +1 (red before): the second message is marked, unmarked when the first reply
  completes, then answered. Suites .NET 538/538, e2e 137/137.
- **Human test** (throwaway Testing server, scratch copy of `projects160`, LAN model): in the home Dragon, "remember
  AMBERFOX, and have Sage load waver" then, 0.7 s later, "what is the codeword?". The second showed "Sending..." while
  Sage worked; the server logged it WAITING; both were answered in order and the second said "AMBERFOX"; the mark and
  typing cleared. No console errors.
