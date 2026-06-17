---
id: TASK-032
parent: STORY-017
feature: FEATURE-019
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-030]
blocks: [TASK-034, TASK-035, TASK-036, TASK-042, TASK-049]
pr: null
github-issue: null
jira-key: null
---

# Enable JWT validation middleware

## Context

The server currently mints tokens (`AuthEndpoints`) but has **no `UseAuthentication`/`UseAuthorization`** in the pipeline — protected routes aren't enforced. `Birko.Security.AspNetCore` provides `AddBirkoSecurity` / `AddBirkoJwtBearer` which wire the full JWT Bearer pipeline, `ICurrentUser` (claims), `IPermissionChecker` + `PermissionEndpointFilter` (scopes), and **already read `?token=` from the query string** (for SSE/WS). This task turns it on.

## Acceptance criteria

- [ ] `AddBirkoSecurity(...)` registered; `app.UseAuthentication()` + `app.UseAuthorization()` added to the pipeline
- [ ] Protected: `/api/v1/*` (STORY-016), `/dragon`, `/wyvern`, `/kobold`
- [ ] **Daemon loopback bypass**: when bound to `127.0.0.1` in daemon mode, JWT validation is skipped (OS-user trust); any non-loopback bind enforces it
- [ ] Service-account `scopes` enforced at endpoints via `PermissionEndpointFilter`
- [ ] `?token=` auth confirmed working for a WS endpoint (sets up SSE reuse in TASK-045)
- [ ] **Harden the OAuth `/device/approve` route from TASK-030**: require auth and derive `userId` from the authenticated principal instead of the explicit `user_id` body field (TASK-030 shipped it unguarded under the `Enabled:false` default — remove the `// TODO(TASK-032)` marker and drop the body `user_id`). Also gate the OAuth `/register` (RFC 7591) route behind admin auth.
- [ ] Tests: 401 without token, 200 with valid token, bypass under loopback daemon

## Out of scope

- Reconciling the two auth paths on WS endpoints beyond enabling (Birko WS middleware vs JWT bearer) — track in STORY-015 open question
- Removing the legacy auth (TASK-036)

## Human test plan

- [ ] Start non-daemon (non-loopback) → `curl /api/v1/projects` without token returns 401; with a valid token returns 200
- [ ] Start `--daemon` on 127.0.0.1 → same call succeeds with no token

## Implementation plan

_Populated by `/tasks plan TASK-032` — leave empty until then._
