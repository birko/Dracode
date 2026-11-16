---
id: TASK-116
parent: STORY-039
feature: FEATURE-026
status: cancelled
priority: P3
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Adopt WsClient.request and the runtime-config loader from Birko.Web.Core; delete the local copies

> **Unblocked 2026-10-14:** Birko TASK-523 and TASK-526 shipped in **Birko.Web v0.1.8** — `WsClient.request()` / `requestMessage()` (correlation on the envelope's top-level `id`, which is how `WebSocketCommandHandler` replies; the current `api-client.ts` looks for the id inside `data`, so its replies may never match) and `RuntimeConfig` (`secretKeys: ['authToken']` keeps the token out of localStorage). Core README § WsClient, § Runtime configuration.

## Context

`DraCode.KoboldLair.Client/src/services/api-client.ts` correlates WebSocket requests and replies itself (about 90 generic lines) and checks "connected"
as `ws !== null`; `DraCode.KoboldLair.Client/src/services/config.ts` layers defaults < `/api/config` < local storage itself. Birko TASK-523 / TASK-526 move
both into the framework. The header's connection state (TASK-055) should then come from `WsClient.connected` / the framework
connection-state manager, not `window.koboldLairWebSocket`.

## Acceptance criteria

- [ ] `api-client.ts` keeps only its typed command methods on top of `WsClient.request`
- [ ] `config.ts` uses the framework runtime-config loader
- [ ] Connection state comes from `WsClient`; `window.koboldLairWebSocket` is no longer read
- [ ] Existing client tests stay green

## Out of scope

- The framework changes themselves (Birko TASK-523 / TASK-526)

## Human test plan

N/A — covered by tests.

## Implementation plan

## Cancelled

Cancelled 2026-11-16, superseded. The rebuilt web UI talks only to `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so it cannot back screens in a signed-in, per-user UI (FEATURE-026 D8, decided by the owner 2026-11-16). That leaves `api-client.ts` without a screen to serve. The runtime-config half can be taken up when `config.ts` is next touched.
