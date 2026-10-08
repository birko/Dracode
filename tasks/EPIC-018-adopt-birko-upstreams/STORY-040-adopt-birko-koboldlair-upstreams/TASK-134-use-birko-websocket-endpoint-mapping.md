---
id: TASK-134
parent: STORY-040
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-536 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Map the WebSocket endpoints with Birko's mapping and RequireAuthorization; delete the three hand-written blocks

## Context

`Program.cs` L1071–1140: three copy-pasted `app.Map` blocks (401 before accept, 400 if not a WebSocket request) and a hand-written claims parser `ResolveCaller`. They exist because Birko's `requireAuthentication` flag failed open.

Framework side: Birko TASK-536 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] `/dragon`, `/wyvern`, `/kobold` use `MapWebSocketEndpoint` + `.RequireAuthorization()`
- [ ] Caller resolved through `ICurrentUser`; `ResolveCaller` deleted
- [ ] `DragonWebSocketAuthTests` green, including an anonymous-upgrade rejection

## Out of scope

- The framework change itself (Birko TASK-536)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
