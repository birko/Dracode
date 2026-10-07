---
id: TASK-036
parent: STORY-017
feature: FEATURE-019
status: done
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

> **Rescoped 2026-10-07, at pick, before the work (user decision):** remove immediately — no `[Obsolete]` step and no
> one-release window. `Authentication:Jwt:Users` was empty in every config, so no one could sign in through `/auth/login`.
> `/auth/refresh` and `/auth/logout` are **not** legacy: GitHub sign-in (TASK-033) renews through them, and TASK-056 uses them.

- [x] The legacy pieces are removed: `WebSocketAuthenticationService` + `WebSocketAuthenticationConfiguration` registration
      (`Authentication:Enabled|Tokens|TokenBindings`), `POST /auth/login`, `Authentication:Jwt:Users` / `JwtUser`, the
      static-user `KoboldLairRoleProvider` and the static-user lookups in `KoboldLairPermissionChecker` (now a static class
      of permission names + the role → permission map), `IPasswordHasher`
- [x] Docs updated to point at GitHub / OAuth device-code sign-in instead of `/auth/login` — `DraCode.KoboldLair.Server/README.md`
      Authentication section rewritten (the shared-token / IP-binding text was stale since TASK-034)
- [x] After removal: no references remain; build + tests green — `dotnet build DraCode.slnx` clean, suite 252/252

## Out of scope

- The OAuth server / federation (TASK-030 / TASK-033) — this only retires the old path

## Human test plan

N/A — no deprecation window (decision above). That `/auth/login` is gone and that a GitHub-issued refresh token still
renews are asserted by `JwtMiddlewareTests` (`The_static_password_login_is_gone` — proven to fail with the route back;
`Refresh_reissues_a_working_token_and_rotates_the_refresh_token`); the WebSocket/JWT path by `DragonWebSocketAuthTests`.

## Implementation plan

Done 2026-10-07.

1. `Program.cs`: drop the `WebSocketAuthenticationService` / config registration, `IPasswordHasher`, `IRoleProvider`, and
   the now-unused `using`s.
2. `AuthEndpoints`: only `/auth/refresh` + `/auth/logout`. `JwtAuthenticationConfiguration`: no `Users` / `JwtUser`.
   `KoboldLairRoleProvider` deleted; `KoboldLairPermissionChecker` → static class.
3. `appsettings.json`: legacy keys removed. Tests no longer seed static users; the password-login test is replaced by a
   "route is gone" test and a refresh round-trip test.
4. Server README: Authentication section describes JWT + GitHub + OAuth (Birko.Security*).

## Close notes

- Closed 2026-10-07. `RefreshTokenStore` (behind `/auth/refresh`) is DraCode's own in-memory store, so GitHub sessions do
  not survive a server restart; whether to move refresh onto the Birko OAuth server is TASK-108.
