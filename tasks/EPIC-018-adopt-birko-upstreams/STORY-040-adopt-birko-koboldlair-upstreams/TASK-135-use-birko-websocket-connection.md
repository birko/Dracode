---
id: TASK-135
parent: STORY-040
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-537 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the Birko WebSocket connection; delete WebSocketSender and the hand-written receive loops

## Context

Live defects until this lands: `WyrmService` sends from a `Task.Run` while its receive loop also sends (concurrent `SendAsync`); `DragonService` / `WyrmService` ignore `EndOfMessage` (64 KB buffer — large messages parse as partial JSON); `KoboldEndpointService` reads its first message uncapped; `WebSocketSender` reports undelivered messages as sent. If Birko TASK-537 slips, patch the concurrent send and `EndOfMessage` locally first.

Framework side: Birko TASK-537 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] All three services send and receive through the framework connection
- [ ] `WebSocketSender` and the receive loops deleted
- [ ] Test: a message larger than 64 KB arrives whole

## Out of scope

- The framework change itself (Birko TASK-537)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
