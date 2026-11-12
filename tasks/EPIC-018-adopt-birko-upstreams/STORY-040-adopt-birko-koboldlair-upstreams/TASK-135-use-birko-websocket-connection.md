---
id: TASK-135
parent: STORY-040
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: null
pr: null
github-issue: null
jira-key: null
---

# Receive whole messages with Birko's ReceiveMessageAsync; delete WebSocketSender and the hand-written receive loops

## Context

**Unblocked:** Birko TASK-537 landed (2026-10-08): capped `ReceiveMessageAsync`; no send queue (concurrent sends are already serialized).

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

- [x] Dragon, Wyrm and the Kobold endpoint read with `ReceiveMessageAsync` and a declared cap; over-cap closes the socket instead of parsing a fragment
- [x] `WebSocketSender` deleted; sends go straight to the socket, and a failed send is logged or surfaced, never reported as delivered
- [x] The hand-written receive loops deleted
- [x] Test: a message larger than 64 KB arrives whole; one over the cap closes with `MessageTooBig`

## Out of scope

- The framework change itself (Birko TASK-537)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan

1. `WebSocketLimits.MaxMessageBytes` (Birko's default, 4 MiB) as the one declared cap.
2. Dragon and Wyrm receive with `ReceiveMessageAsync`; the Kobold endpoint reads its initial payload and its
   watch-for-close loop the same way.
3. Delete `WebSocketSender`, `DragonSession.Sender`, `DragonRequest.Sender`; sends go through
   `WebSocketSend.TrySendTextAsync`, which returns false (and logs) instead of reporting an undelivered message sent.
4. Host tests on all three endpoints, red first.

## Implementation notes

- **Cap:** 4 MiB on all three endpoints. Before, Dragon and Wyrm parsed whatever one `ReceiveAsync` returned (the
  test host delivers 16 KB frames, so a 100 KB ping parsed as a fragment and answered `error`); Kobold reassembled
  without a limit. Over the cap, `ReceiveMessageAsync` closes with MessageTooBig and the handler logs a warning and
  ends.
- **Sends:** `WebSocketSend.TrySendTextAsync` (no Birko send helper exists) sends straight to the socket and returns
  false when the socket is not open or the send throws. Callers that act on delivery now check it:
  - Dragon pending notifications: marked read by id, only those actually sent. Before, `MarkAsRead(project)` cleared
    the whole list after the loop, so notifications whose send failed were lost, and so was any that arrived during the
    loop.
  - `DragonRequestQueue.SendResponseAsync`: the post-response callback (spec detection) only runs when the response
    was delivered, as its doc already said.
  - `/kobold`: a failed send cancels the relay, as a close does (before, `WebSocketSender` reported it sent and the
    pump kept going).
  - Everything else (streaming chunks, thinking updates, status) was fire-and-forget before and still is; a failure is
    logged at Debug.
- Ordering of Dragon's fire-and-forget sends now relies on the socket's own send serialization (Birko TASK-537's
  measurement) instead of the queue; each caller reaches `SendAsync` synchronously in call order.
- Wyrm's and `WebSocketCommandHandler`'s sends already went straight to the socket and throw on failure (caught and
  logged by the loop); left as they were.
- The Kobold endpoint no longer sends "missing 'mode'" to a socket the client already closed: a close or an over-cap
  initial payload ends the handler.

## Close notes

- `WebSocketMessageSizeTests` (6): ping >64 KB on `/wyvern` and `/dragon` answers `pong` (the Dragon case opens a real
  session with a dummy ollama provider); `/kobold` initial payload >64 KB is parsed whole; a message over the cap on
  each endpoint closes with MessageTooBig and gets no reply. On the old code 5 of 6 failed: the two >64 KB pings
  (fragment parsed) and all three over-cap cases (parsed and answered). The Kobold >64 KB case passed on the old code,
  which already reassembled; it stays as the guard for the new path.
- Not covered by a test: notifications marked read only when sent, the skipped post-response callback, the `/kobold`
  relay stopping on a failed send (all need a socket that fails mid-send inside a full Dragon/Kobold run).
- Full suite 319/319.
- Human test plan: N/A; not run against a live server (local-settings gotcha).
