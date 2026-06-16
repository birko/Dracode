---
id: STORY-017
parent: EPIC-012
status: in-progress
created: 2026-05-28
---

# OAuth/OIDC authentication and per-caller identity

## User story

As an operator wanting to expose KoboldLair to multiple clients (CLI on my laptop, Discord bot, CI runner, future teammates), I want real per-caller identity so I can audit who did what and revoke individual callers without breaking others. Local-only single-user installs should keep working with zero config.

## Existing building blocks (use these — do not re-implement)

Birko.Framework already ships the identity stack; this story is **wiring + DraCode-specific glue**, not a greenfield OAuth build:

| Project | Role here |
|---|---|
| **`Birko.Security.OAuth.Server`** | DraCode acts as its **own authorization server (IdP)**. OAuth 2.1, pure handler library (no ASP.NET dep). Supports `authorization_code`+PKCE, `client_credentials`, `refresh_token` (rotation), and `device_code` (RFC 8628). Dynamic client registration (RFC 7591). |
| **`Birko.Communication.OAuth` + `.Providers`** | **Federation client** — `GitHubOAuthProvider` already implemented for "login with GitHub". |
| **`Birko.Security.Jwt`** | `JwtTokenProvider` — the concrete `ITokenProvider` the OAuth server signs tokens with. |
| **`Birko.Security.AspNetCore`** | JWT validation middleware + DI. `AddBirkoJwtBearer` / `AddBirkoSecurity` wire `AddAuthentication`+`AddJwtBearer`+`AddAuthorization`, plus `ICurrentUser` (claims → `ownerId`) and `IPermissionChecker`+`PermissionEndpointFilter` (claims-based scopes → service-account enforcement). **Reads `?token=` from the query string out of the box** (built for SSE + WebSocket). The server currently has **no** auth middleware enabled — this just needs turning on. |
| **`Birko.Data.SQL.SqLite`** | Persistence — `AsyncSQLiteStore<T>` implements `IAsyncStore<T>`, which is exactly the contract every OAuth-server store interface extends. |

The current `DraCode.KoboldLair.Server/Auth/AuthEndpoints.cs` (static username/password JWT via `JwtAuthenticationConfiguration.Users`) is a **stopgap to retire** once the OAuth server is wired.

## Grant-type → client mapping

| Client | Grant | Notes |
|---|---|---|
| CLI (`koboldlair login`) | **`device_code`** (RFC 8628) | No localhost redirect server needed; ideal for headless/terminal. Supersedes the "open browser + capture redirect" flow. |
| Web UI | `authorization_code` + PKCE | Public client. |
| Discord bot / CI | **`client_credentials`** | This *is* the "service-account JWT with scopes" — operator registers a confidential client per bot. |
| Token refresh | `refresh_token` | Rotation on by default; plaintext cached in `~/.koboldlair/auth.json`. |

GitHub OAuth (via `GitHubOAuthProvider`) is the **upstream IdP for human login** — DraCode federates to GitHub, then mints its own tokens for its clients.

## Behaviour

- DraCode hosts the OAuth server endpoints (`/authorize`, `/token`, `/device_authorization`, client registration) by mapping minimal-API routes onto the `OAuthServer` handler instance.
- JWT validation middleware sits in front of `/api/v1/...` (STORY-016) and `/dragon`, `/wyvern`, `/kobold`.
- Human login federates to GitHub; the stable GitHub identity (`sub`) maps to a DraCode `User` record. Projects gain an `ownerId` foreign key.
- Service accounts (Discord, CI) are registered as confidential OAuth clients; their `client_credentials` tokens carry `{ sub: "service:discord-bot", scopes: [...] }`.
- **Local daemon mode bypasses auth entirely**: when `KoboldLair.Server` starts with `--daemon` and binds to `127.0.0.1`, it trusts the OS user and skips JWT validation. Any non-loopback bind requires auth.
- CLI login: `koboldlair login` runs the device-code flow, prints the user-code + verification URL, polls `/token`, caches the result in `~/.koboldlair/auth.json`.
- Web UI: existing login UI replaced by the GitHub OAuth redirect.

## Persistence (corrected — no bespoke SQL project)

The OAuth-server store interfaces are each `IAsyncStore<T>` plus default-method named lookups (e.g. `IOAuthClientStore.GetByClientIdAsync` falls through to `ReadAsync(filter)`). The models are `AbstractModel` descendants. So production persistence is **five trivial subclass declarations** over the existing SQLite store — the SQL mirror of the test project's `InMemoryStore<T>` subclasses:

```csharp
public class SqlOAuthClientStore        : AsyncSQLiteStore<OAuthClient>,        IOAuthClientStore { }
public class SqlAuthorizationCodeStore  : AsyncSQLiteStore<AuthorizationCode>,  IAuthorizationCodeStore { }
public class SqlRefreshTokenStore       : AsyncSQLiteStore<RefreshTokenRecord>, IRefreshTokenStore { }
public class SqlDeviceCodeStore         : AsyncSQLiteStore<DeviceCodeRecord>,   IDeviceCodeStore { }
public class SqlConsentStore            : AsyncSQLiteStore<ConsentRecord>,      IConsentStore { }
```

Each gets `SetSettings(new PasswordSettings(dbDir, dbFile))` (same DB file pattern as `SqlPlanRepository`) and `InitAsync()` on startup to create tables. No `*Entity`/`EntityMapper` layer is needed — the OAuth models persist directly (unlike KoboldLair domain models). No new Birko subproject.

## DraCode-specific data changes

- New tables (auto-created via `InitAsync`): OAuth client/code/refresh/device/consent stores (above), plus a DraCode `users` table.
- `projects` gains `ownerId`; existing `projects.json` rows migrate to a system `ownerId = "legacy"` user. Future creates require a real owner.
- `WebSocketAuthenticationConfiguration` (shared-token system) and `AuthEndpoints.cs` (static user JWT) marked deprecated; remain functional one release as a migration fallback, then removed.

## Suggested task breakdown

1. **Host the OAuth server** — register `OAuthServer` (singleton) with `JwtTokenProvider`; map `/authorize`, `/token`, `/device_authorization`, client-registration minimal-API endpoints onto its handlers.
2. **Wire the 5 SQLite stores** — the subclasses above + DI registration + `InitAsync` on startup.
3. **Enable JWT validation middleware** — call `AddBirkoSecurity(...)` + `app.UseAuthentication()/UseAuthorization()`; protect `/api/v1`, `/dragon`, `/wyvern`, `/kobold`; honour the daemon loopback bypass. Query-string (`?token=`) auth for SSE/WS is already handled by the package. Map service-account `scopes` to endpoint protection via `PermissionEndpointFilter`.
4. **GitHub federation** — wire `GitHubOAuthProvider`; map upstream GitHub `sub` → DraCode `User`.
5. **`User` entity + `projects.ownerId`** + `projects.json` "legacy" migration + per-user scoping default.
6. **Service-account registration** — operator UX to create confidential clients (Web UI + `koboldlair keys create --service`).
7. **Deprecate** `WebSocketAuthenticationConfiguration` + static `AuthEndpoints`.

## Risks

- GitHub IdP unreachable → human auth fails. The local-daemon bypass mitigates the common single-user case; document the failure mode.
- JWT signing-key rotation requires server restart; document the operator playbook.
- Per-user project scoping: `GET /api/v1/projects` defaults to caller's own; admins opt into `?scope=all`.

## Downstream dependency

STORY-016 (REST/SSE facade) requires this story's validation middleware for its "all endpoints require auth" guarantee. Recommend adding `depends-on: [STORY-017]` to STORY-016 (or shipping STORY-016 behind the daemon bypass first).
