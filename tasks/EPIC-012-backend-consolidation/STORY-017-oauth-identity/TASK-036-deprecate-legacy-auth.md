---
id: TASK-036
parent: STORY-017
feature: FEATURE-019
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-032, TASK-033]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Deprecate and remove legacy auth

## Context

The old shared-token `WebSocketAuthenticationConfiguration` and the static username/password `AuthEndpoints.cs` (`/auth/login|refresh|logout` over `JwtAuthenticationConfiguration.Users`) are superseded by the OAuth server + GitHub federation. They remain functional one release as a migration fallback, then are removed.

> **Scope update (2026-06-20, via TASK-034 grill):** TASK-034 already switches the `/dragon` + `/wyvern` WebSocket endpoints **off** the legacy `WebSocketAuthenticationService` and onto JWT bearer (so Dragon sessions carry a real `sub`). So this task's WS portion narrows to **deleting** the now-dead `WebSocketAuthenticationService` + `Authentication` (non-JWT) config + IP-binding machinery (FEATURE-069). Also: since we're rebuilding from scratch with **no legacy consumers**, the "one-release deprecation window" below is likely unnecessary — removal can be immediate. Revisit the AC when this task is picked.

## Acceptance criteria

- [ ] `WebSocketAuthenticationConfiguration` and static `AuthEndpoints` marked `[Obsolete]` with a migration message
- [ ] Docs updated to point at OAuth/device-code login instead of `/auth/login`
- [ ] Removal tracked behind a one-release window (note the target release in the PR)
- [ ] After removal: no references remain; build + tests green

## Out of scope

- The OAuth server / federation (TASK-030 / TASK-033) — this only retires the old path

## Human test plan

- [ ] During the deprecation window, confirm an existing static-user login still works; after removal, confirm only OAuth/device-code login is accepted

## Implementation plan

_Populated by `/tasks plan TASK-036` — leave empty until then._
