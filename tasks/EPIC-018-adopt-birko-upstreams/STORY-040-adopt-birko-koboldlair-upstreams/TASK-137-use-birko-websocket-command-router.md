---
id: TASK-137
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-539 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the Birko command router for Dragon commands; keep the handlers

## Context

`Services/WebSocketCommandHandler.cs` (161) + `Models/WebSocket/WebSocketCommand.cs` (11). Its error path drops the request id, so a correlating client waits out its timeout.

Framework side: Birko TASK-539 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Command handlers registered on the framework router
- [ ] Local router and command model deleted
- [ ] Error replies carry the request id (pairs with TASK-116 on the client)

## Out of scope

- The framework change itself (Birko TASK-539)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
