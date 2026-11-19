---
id: TASK-174
parent: STORY-040
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: verify
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P1
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-17
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-024]
pr: null
github-issue: null
jira-key: null
---

# With JWT on, a real server refuses every `?token=` WebSocket connect

## Context

Found 2026-11-17 while running TASK-024's human test plan against a real server process (not a test host): the
`DraCode.KoboldLair.Server` built from `task/TASK-025` (`c3d2c8f`), run with `dotnet run --no-build` on Kestrel at
`http://127.0.0.1:57399`, `ASPNETCORE_ENVIRONMENT=Testing`, `Authentication:Jwt:Enabled=true` with a throwaway secret,
a scratch `ProjectsPath`. `main` has the same code.

- `POST /api/v1/projects` with the token in an `Authorization: Bearer` header: **201**.
- `ws://…/dragon?token=<same token>`: **401** on the upgrade.
- The same `/dragon` upgrade with the token in an `Authorization` header instead: accepted, and the whole decision
  round trip worked.

**Mechanism.** Birko's bearer setup (`Birko.Security.AspNetCore/Authentication/JwtBearerExtensions.cs`,
`OnMessageReceived` → `AcceptsQueryToken`) reads `?token=` only when the request has no `Authorization` header and
`http.WebSockets.IsWebSocketRequest` is true (or it asks for `text/event-stream`, or its path is in
`QueryTokenPathPrefixes`). `IsWebSocketRequest` comes from the feature that `UseWebSockets` installs, and
`DraCode.KoboldLair.Server/Program.cs` calls `app.UseAuthentication()` (line 1021 on `main`, 1041 on the TASK-025
stack) **before** `app.UseWebSockets(webSocketOptions)` (line 1128 on `main`, 1148 on the stack). On Kestrel the flag is therefore false while authentication runs,
the query token is ignored, and `RequireAuthorization` on `/dragon`, `/kobold` and `/wyvern` (TASK-134) refuses the
upgrade. A browser cannot set headers on a WebSocket, so the web client and `ServerWebSocketProxy` (which forwards
`?token=`) can never connect with JWT on.

**Why the tests miss it.** `DragonWebSocketAuthTests`, `DragonDecisionSocketTests` and the rest connect through
`TestServer.CreateWebSocketClient()`, which supplies the WebSocket feature on the request itself, so
`IsWebSocketRequest` is true whatever the middleware order.

Introduced or exposed by TASK-134 (`3c02a37`), which moved the endpoints onto Birko's `MapWebSocketEndpoint` with
`RequireAuthorization`. With JWT off (the default config) nothing is checked, which is presumably why nobody hit it.

## Acceptance criteria

- [x] A test connects to `/dragon` with `?token=` on a **real Kestrel host** (a server bound to a loopback port, not
      `TestServer`) with JWT on, and is accepted; it fails on the current middleware order
- [x] The same test shows `/kobold` accepts `?token=` the same way, and an upgrade with no token or a bad one is still
      refused with 401
- [x] `/api/v1` keeps refusing a `?token=` query on a plain (non-WebSocket, non-SSE) request, as Birko intends
- [x] The full suite passes

## Out of scope

- The decision-card behaviour itself (TASK-024), and its human test plan, which reruns through the query path once
  this lands.
- Changing Birko's `AcceptsQueryToken` rules.

## Human test plan

N/A — fully covered by automated tests: the acceptance test runs on a real Kestrel socket, which is the gap the
TestServer tests left. TASK-024's human test plan exercises the same connect by hand afterwards.

## Implementation plan

1. Red: `WebSocketQueryTokenKestrelTests` starts one real Kestrel host (free loopback port, JWT on) and connects with
   `ClientWebSocket`. On the old order both `?token=` connects came back 401; the no-token, bad-token and plain
   `/api/v1?token=` cases already held.
2. Green: `Program.cs` calls `UseWebSockets` right after `UseCors`, before `UseAuthentication`, so Birko's
   `AcceptsQueryToken` sees `IsWebSocketRequest` on Kestrel.

## Outcome (2026-11-19)

- New tests 7/7; with `DragonWebSocketAuthTests` and `JwtMiddlewareTests` 28/28; full suite 365/365 on `main` + this change.
- The test host binds `127.0.0.1:0`, not Kestrel's default 5000, so it cannot collide with another local server.
