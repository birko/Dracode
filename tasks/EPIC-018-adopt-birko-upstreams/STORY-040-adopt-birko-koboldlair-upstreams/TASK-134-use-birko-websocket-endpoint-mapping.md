---
id: TASK-134
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

# Map the WebSocket endpoints with Birko's mapping and RequireAuthorization; delete the three hand-written blocks

## Context

**Unblocked:** Birko TASK-536 landed (2026-10-08): `requireAuthentication: true` now fails closed; for JWT use `requireAuthentication: false` + `.RequireAuthorization()`.

`Program.cs` L1071–1140: three copy-pasted `app.Map` blocks (401 before accept, 400 if not a WebSocket request) and a hand-written claims parser `ResolveCaller`. They exist because Birko's `requireAuthentication` flag failed open.

Framework side: Birko TASK-536 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [x] `/dragon`, `/wyvern`, `/kobold` use `MapWebSocketEndpoint` + `.RequireAuthorization()`
- [x] Caller resolved through `ICurrentUser`; `ResolveCaller` deleted
- [x] `DragonWebSocketAuthTests` green, including an anonymous-upgrade rejection

## Out of scope

- The framework change itself (Birko TASK-536)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan

1. Map `/wyvern`, `/dragon`, `/kobold` with `MapWebSocketEndpoint(..., requireAuthentication: false)`; chain
   `.RequireAuthorization()` only when JWT is on (UseAuthorization is only in the pipeline then — authorization
   metadata without it fails every request).
2. Replace `ResolveCaller` with `Auth/WebSocketCaller.From(ICurrentUser)` returning the existing `KoboldCaller`.
3. Tests: caller unit tests; host tests for anonymous rejection on all three, authorization metadata on/off,
   anonymous upgrade with JWT off.

## Implementation notes

- `Program.cs`: the three `app.Map` blocks, `jwtCaptured` and `ResolveCaller` are gone. The endpoints are an array of
  `MapWebSocketEndpoint` results, and `RequireAuthorization()` is applied to each when `jwtRuntimeEnabled`.
- `WebSocketCaller.From` reads the subject with `GetClaim(ClaimTypes.NameIdentifier)`, **not** `ICurrentUser.UserGuid`:
  GitHub subjects are `github:{id}` and service accounts `service:<name>`, which `UserGuid` turns into null. Admin is
  `ApiOwnership.IsAdmin` (the same rule as `/api/v1`); name and email keep the old fallbacks; an unauthenticated
  caller (JWT off) is still the local owner and admin.
- Sibling: the `/api/v1` surface has that very `UserGuid` defect at its own call sites (`ApiOwnership.CanAccess`,
  project listing/creation, runs, `/whoami`). Filed as TASK-141, not fixed here.
- Behaviour change (accepted by the owner): a plain (non-WebSocket) request now gets 404 instead of 400, since
  Birko's middleware passes it down the pipeline. Unauthenticated requests still get 401 first. No client depends on
  the 400.
- `docs/Dragon-Requirements-Agent.md` endpoint example updated.

## Close notes

- Regression proof: `With_jwt_every_websocket_endpoint_requires_authorization` (3 cases) failed on the old code
  (plain `app.Map`, no metadata) and passes now. Mutation `Sub = user.UserGuid?.ToString()` fails 3 of the
  `WebSocketCallerTests`.
- New tests: `WebSocketCallerTests` (8), `DragonWebSocketAuthTests` +9 (`/kobold` anonymous rejected and JWT
  accepted, metadata with/without JWT, anonymous upgrade with JWT off, plain request 401).
- Full suite 313/313.
- Human test plan: N/A (covered by tests); not run against a live server, per the local-settings gotcha.
