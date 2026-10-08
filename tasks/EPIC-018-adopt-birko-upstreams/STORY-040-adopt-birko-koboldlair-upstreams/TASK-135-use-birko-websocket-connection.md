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

# Receive whole messages with Birko's ReceiveMessageAsync; delete WebSocketSender and the hand-written receive loops

## Context

Live defects until this lands: `DragonService` / `WyrmService` ignore `EndOfMessage` (64 KB buffer — a larger message
parses as partial JSON); `KoboldEndpointService` reads its first message uncapped; `WebSocketSender` reports undelivered
messages as sent.

Birko TASK-537 (rescoped 2026-10-08) ships `ReceiveMessageAsync` (`Birko.Communication.WebSocket.Messaging`): one whole
message, reassembled across frames, capped, with close and over-cap reported as outcomes. It does **not** ship a send
queue, because it measured that concurrent `SendAsync` on one socket is serialized by .NET 10's `ManagedWebSocket`
(3 × 200 concurrent 100 KB sends: 0 threw, 0 corrupt). So `WyrmService` sending from a `Task.Run` while its receive
loop also sends is **not** a defect, contrary to what this task first said, and `WebSocketSender` can simply go.

Framework side: Birko TASK-537 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Dragon, Wyrm and the Kobold endpoint read with `ReceiveMessageAsync` and a declared cap; over-cap closes the socket instead of parsing a fragment
- [ ] `WebSocketSender` deleted; sends go straight to the socket, and a failed send is logged or surfaced, never reported as delivered
- [ ] The hand-written receive loops deleted
- [ ] Test: a message larger than 64 KB arrives whole; one over the cap closes with `MessageTooBig`

## Out of scope

- The framework change itself (Birko TASK-537)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
