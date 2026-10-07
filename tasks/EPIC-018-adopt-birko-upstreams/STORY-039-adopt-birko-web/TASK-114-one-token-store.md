---
id: TASK-114
parent: STORY-039
feature: FEATURE-026
status: todo
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# One token store: the Shell auth store feeds WebSocket, REST and SSE; config.ts stops holding a token

## Context

The client keeps two token stores: `koboldlair-config` (`DraCode.KoboldLair.Client/src/services/config.ts`) and `koboldlair_auth` (the Shell auth store
wrapped in `DraCode.KoboldLair.Client/src/auth-store.ts`). The WebSocket client reads the first, and nothing reads the second yet. The REST `ApiClient`
(Birko.Web.Core, with `onRefreshToken` / `onUnauthorized`) and `SseClient` should read the same token as the WebSocket.

## Acceptance criteria

- [ ] The Shell auth store is the only place the token lives; `WsClient.getToken`, `ApiClient` and `SseClient` read it
- [ ] `config.ts` keeps server URL and settings only; an existing `koboldlair-config` token is migrated once, then removed
- [ ] Tests: setting the token in the auth store reaches all three clients; signing out clears it

## Out of scope

- OAuth redirect parsing and refresh scheduling (TASK-056, built on Birko TASK-524)

## Human test plan

N/A — covered by tests.

## Implementation plan
