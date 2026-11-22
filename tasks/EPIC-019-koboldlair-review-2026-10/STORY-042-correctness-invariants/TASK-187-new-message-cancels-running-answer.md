---
id: TASK-187
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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
pr: null
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

- [ ] A message sent while Dragon is answering is answered after the running reply, with both messages in Dragon's
      context — queued per session rather than cancelling — proved by a test that fails today
- [ ] Whatever the server does, the dock shows it: the second message is visibly waiting, never silently dropped (e2e)
- [ ] An explicit cancel, if one is kept, is its own message type and is not triggered by sending a message

## Out of scope

- Concurrency limits across sessions (`maxConcurrentRequests`)

## Human test plan

- [ ] On a throwaway server: ask Dragon something that delegates to Sage, send a follow-up before it answers — both
      are answered, the second taking the first into account

## Implementation plan

_Populated by `/tasks plan TASK-187` — leave empty until then._
