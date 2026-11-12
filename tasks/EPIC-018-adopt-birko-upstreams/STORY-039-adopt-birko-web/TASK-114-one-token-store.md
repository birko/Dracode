---
id: TASK-114
parent: STORY-039
feature: FEATURE-026
status: done
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

- [x] The Shell auth store is the only place the token lives; `WsClient.getToken`, `ApiClient` and `SseClient` read it
- [x] `config.ts` keeps server URL and settings only; an existing `koboldlair-config` token is migrated once, then removed
- [x] Tests: setting the token in the auth store reaches all three clients; signing out clears it

## Out of scope

- OAuth redirect parsing and refresh scheduling (TASK-056, built on Birko TASK-524)

## Human test plan

N/A — covered by tests.

## Implementation plan

1. `auth-store.ts`: `getToken()`; a one-time migration of the legacy token keys.
2. `config.ts` loses `authToken`; the `/wyvern` client reads `getToken`; `services/http.ts` creates the REST and SSE
   clients on the same getter.
3. The server selector stops keeping a token per server.
4. Playwright `tokens.spec.ts` through a `window.koboldLair` handle.

## Implementation notes

- **One store:** `auth-store.ts` exports `getToken()` (the Shell auth store's `token`). Readers:
  - `services/api-client.ts` (the `/wyvern` command client, Birko `WsClient`): `getToken` — before, it closed over a
    config snapshot taken at `connect()`, so a token change never reached a reconnect;
  - `services/http.ts` (new): `createRestClient()` — Birko `ApiClient` on `{server}/api/v1`, `getToken`,
    `onUnauthorized` signs out; `createSseClient(path)` — Birko `SseClient`, `getToken`. Nothing calls them yet;
    TASK-142 will.
- **Config:** `authToken`, `setAuthToken()` and reading `authToken` from `/api/config` are gone (the host stopped
  sending it in TASK-119). `config.ts` also drops an `authToken` it finds in storage, so a later save cannot write
  one back whichever module loads first.
- **Migration** (`migrateLegacyTokens`, on load): a `koboldlair-config.authToken` moves into the auth store unless it
  already holds one, and is deleted; per-server `token`s in `koboldlair-servers` are deleted. After the first load
  nothing is left, so signing out stays signed out.
- **Server selector** (exported, not mounted anywhere): no token field, no per-server token, no `setAuthToken` on
  switching or deleting a server. My call: a token now comes from sign-in (TASK-056), not from pasting one per
  server, and keeping the per-server copy would be the second store this task removes.
- **Debug/e2e handle:** `window.koboldLair = { auth: {store, setAuth, clearAuth, getToken}, clients:
  {createWyvernClient, createRestClient, createSseClient} }`, beside the existing `koboldLairShell` /
  `koboldLairRouter`.

## Close notes

- `tests/ui-e2e/tokens.spec.ts` (3): a token set in the auth store reaches the WebSocket URL (`?token=`), the REST
  `Authorization: Bearer` header and the SSE URL; after sign-out none of the three sends a token and
  `koboldlair_auth` is gone; a `koboldlair-config` token is migrated into the auth store, removed from config and from
  the per-server list, and does not come back after sign-out and reload. Mutations: the WebSocket client ignoring the
  auth store fails test 1; no migration fails test 3.
- On the way: Playwright reports no `websocket` event for a refused connection, and `routeWebSocket` stalled the
  page's `evaluate`, so the spec records WebSocket URLs from the constructor.
- E2E 29/29 against the client host; `tsc` clean in `src/`.
- Human test plan: N/A per the task.
