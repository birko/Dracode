---
id: TASK-119
parent: STORY-041
feature: null
status: todo
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

- [ ] Certificate validation on; an opt-out only via an explicit, dev-only config switch that logs a warning
- [ ] The token is URL-encoded (or sent as a header)
- [ ] `/api/config` no longer returns the token — the browser gets one through sign-in ([[TASK-114]])
- [ ] Close status and description forwarded both ways

## Human test plan

Point the client at a server with a self-signed cert and validation on: the proxy refuses. `GET /api/config` shows no token.

## Implementation plan
