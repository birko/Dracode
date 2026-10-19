---
id: TASK-119
parent: STORY-041
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
findings: [SEC-2]
---

# The client host's WebSocket proxy skips TLS validation, and `/api/config` hands the auth token to any caller

## Context

`DraCode.KoboldLair.Client/Program.cs`:

- `/dragon` and `/wyvern` proxies set `RemoteCertificateValidationCallback = (…) => true` (L66, L119) — unconditionally,
  so any man-in-the-middle is accepted
- the configured `AuthToken` is concatenated into the upstream query string without URL-encoding (L54, L107)
- the upstream close status is not forwarded to the browser
- `/api/config` returns `authToken` to anyone who can reach the client host

Found in the 2026-10-08 KoboldLair review. The generic proxy is framework Birko TASK-542; this task fixes the live defects
now, without waiting for it (adoption is [[TASK-140]]).

## Acceptance criteria

- [x] Certificate validation on; an opt-out only via an explicit, dev-only config switch that logs a warning
- [x] The token is URL-encoded (or sent as a header)
- [x] `/api/config` no longer returns the token — the browser gets one through sign-in ([[TASK-114]])
- [x] Close status and description forwarded both ways

## Human test plan

Point the client at a server with a self-signed cert and validation on: the proxy refuses. `GET /api/config` shows no token.

## Implementation plan

Done 2026-10-19.

1. `DraCode.KoboldLair.Client/ServerWebSocketProxy.cs` replaces the two copied proxy blocks in `Program.cs`. It validates
   the server certificate; `KoboldLair:AllowInvalidServerCertificate` turns that off only in Development and logs a
   warning at startup. It forwards the caller's own `?token=` with `Uri.EscapeDataString`. It relays each side's close
   status and description to the other, then waits up to 5 s for the close reply before cancelling. A failed upstream
   connect returns 502 before the browser socket is accepted.
2. `AuthToken` dropped fully (decided with the user): `/api/config` returns no token, the proxies never attach one of
   their own (that fallback was the same leak through another door), and the setting is gone from `appsettings.json`
   and the client README. With JWT on, the browser sets its token in the web client's settings until sign-in
   ([[TASK-114]] / [[TASK-056]]). The legacy served page loses its token; it is being rewritten ([[TASK-072]]).
3. Tests: `ServerWebSocketProxyTests` (8) — URL encoding, no host token, the certificate opt-out matrix, and close
   status relayed both ways over real loopback WebSockets (red against a relay that sends a fixed close). The test
   project compiles the proxy file in as source rather than referencing the client app: a project reference would add
   a second top-level `Program` and copy the client's `appsettings.json` over the server's in the test output.

## Close notes

- Closed 2026-10-19. Suite 265/265.
- Human test plan run: the server on `https://127.0.0.1:57999` with a throwaway self-signed certificate; the client
  host in Production refused it (502, "The SSL connection could not be established"), in Development with the opt-out
  it connected (101). `GET /api/config` returns only `serverUrl` and the endpoint paths.
- Found while running it: the server did not start with JWT off (the default) since `e9c9944` — Birko TASK-161 makes
  `AddBirkoSecurity` require the tenant header guard, which was added only when JWT is on. Fixed in its own commit, with
  `HostStartupTests`; other host tests remove the hosted services, where that check runs, so they never saw it.
- [[TASK-140]] (Birko WebSocket proxy) replaces `ServerWebSocketProxy` with the framework's; check it keeps these
  behaviours.
