---
id: TASK-030
parent: STORY-017
feature: null
status: done
priority: P1
assignee: ai
created: 2026-06-11
depends-on: []
blocks: [TASK-031, TASK-032, TASK-033, TASK-035]
pr: c712e1b
github-issue: null
jira-key: null
---

# Host Birko OAuth server endpoints in KoboldLair.Server

## Context

DraCode becomes its own OAuth 2.1 authorization server (IdP) by hosting `Birko.Security.OAuth.Server` — a pure, ASP.NET-agnostic handler library. Register `OAuthServer` (singleton) with `JwtTokenProvider` (`Birko.Security.Jwt`) and map minimal-API routes onto its handlers. See STORY-017 for grant-type → client mapping (device_code for CLI, client_credentials for service accounts, authorization_code+PKCE for web).

## Acceptance criteria

- [x] `OAuthServer` registered in DI (singleton factory) with the JWT `ITokenProvider` + `OAuthServerSettings` (`Program.cs`)
- [x] Minimal-API routes mapped onto handlers (`Auth/OAuthEndpoints.cs`): `/token`, `/device_authorization`, `/register` (gated), `/device/approve`, `/authorize` (JSON result, no HTML consent UI)
- [x] `/token` dispatches all four grant types; `OAuthServerException` mapped to RFC 6749 §5.2 JSON (`{error, error_description, error_uri}`, 400)
- [x] Unit tests cover each grant type (success + invalid-grant) using in-memory stores — **8 tests green** (`Auth/OAuthServerTests.cs`): client_credentials ±, authorization_code valid/reused, refresh rotation+revoke, device pending→approve→issue / unknown, unsupported grant
- [x] Stores injected via interface as **singletons** (`Auth/InMemoryOAuthStores.cs`; TASK-031 swaps SQLite)
- [x] OAuth-issued tokens carry `iss`/`aud` signed via the existing `JwtAuthenticationConfiguration` (shared Secret/Issuer/Audience) — validate under TASK-032 by construction
- [x] `/register` mapped only when `AllowDynamicRegistration: true` (default false, independent of `Enabled`); `/device/approve` unguarded with the `// TODO(TASK-032)` marker

## Out of scope

- SQLite store implementations (TASK-031)
- Validation middleware on protected routes (TASK-032)
- GitHub federation (TASK-033)

## Human test plan

- [x] `curl` the device-code flow end to end against a local instance (set `Authentication:OAuth:Enabled=true` + `AllowDynamicRegistration=true` + `KOBOLDLAIR_JWT_SECRET`): **verified 2026-06-12** — register(public)→device_authorization→pending(400)→approve→token(200, signed JWT, scope read); bad client→400 invalid_client; snake_case JSON + RFC error mapping confirmed. `POST /register` a device client → `POST /device_authorization` → `POST /device/approve` `{user_code,user_id,approved:true}` → poll `POST /token` (`grant_type=urn:ietf:params:oauth:grant-type:device_code`) → receive `access_token`. **This is the only verification of the HTTP layer** (snake_case JSON, form parsing, error mapping) — the 8 automated tests cover the handlers, not `OAuthEndpoints.cs`. Optionally automate later with `WebApplicationFactory<Program>`.

## Implementation plan

> ✅ **Resolved (2 scope decisions, 2026-06-12):**
> 1. **`/authorize` is mapped but not end-to-end.** No consent UI / logged-in browser session exists yet, so the route derives `userId` from the JWT principal (if present) and returns **JSON** describing the consent/redirect outcome — it does not render an HTML consent page. The full web `authorization_code`+PKCE flow (consent UI) is **deferred to a later web-UI task** (STORY-023 / EPIC-013 territory). Acceptable: the route + handler wiring is exercised; the browser round-trip is out of scope here.
> 2. **Add `POST /device/approve`, unguarded for now (grilled).** Maps the handler's `ApproveAsync` so the device-code flow completes and the human-test curl flow is runnable. ⚠ The JWT validation middleware is TASK-032 — at TASK-030 the pipeline has no `UseAuthentication`, so the route **cannot** be guarded nor derive a principal `userId` yet. Resolution **(b)**: `/device/approve` takes an explicit `userId` **body field** and is unguarded (acceptable — the whole OAuth surface is `Enabled:false` by default, local-dev only), with a `// TODO(TASK-032): require auth + derive userId from the authenticated principal` marker. Hardening tracked in TASK-032.

### Architecture
`Birko.Security.OAuth.Server` is a **shared MSBuild project** (`.shproj`+`.projitems`), consumed via `<Import Label="Shared">` like `Birko.Security.Jwt` already is — **not yet imported** by the server. `OAuthServer` is a plain composition root (constructor: `OAuthServerSettings, ITokenProvider, TokenOptions, IOAuthClientStore, IAuthorizationCodeStore, IRefreshTokenStore, IDeviceCodeStore, IConsentStore, deviceVerificationUri, IDateTimeProvider?`), exposing pure handlers `.Token/.Authorize/.DeviceAuthorization/.ClientRegistration`.

### Steps
1. **Reference Birko shared projects** — add `<Import ..\..\Birko.Security.OAuth.Server\Birko.Security.OAuth.Server.projitems Label="Shared">` to `DraCode.KoboldLair.Server.csproj` (next to existing Birko imports). Resolve transitive `.projitems` (`Birko.Data.Stores`, `Birko.Data.Core`, `Birko.Time.Abstractions`, `Birko.Configuration`) — **build before coding** (most likely failure point).
2. **`OAuthServerConfiguration` POCO + appsettings** — disabled-by-default. **Issuer / Audience / Secret are NOT redefined here — the OAuth server reuses the existing `JwtAuthenticationConfiguration`'s `Secret` + `Issuer` (`"KoboldLair"`) + `Audience` (`"KoboldLair"`)** (grilled — single source of truth: OAuth stamps `iss`/`aud` and signs with these, and TASK-032's `AddBirkoJwtBearer` validates against the *same* JWT config, so OAuth-minted tokens validate by construction; any divergence 401s every service-account token). The `Authentication:OAuth` section (sibling of `:Jwt`) therefore carries **only** OAuth-specific settings: `Enabled:false`, `DeviceVerificationUri`, the lifetime ints, `RotateRefreshTokens`, `RequirePkceForPublicClients`, and **`AllowDynamicRegistration:false`** (separate flag, independent of `Enabled` — gates whether `/register` is mapped at all; see step 4). Build `TokenOptions`/`OAuthServerSettings` by combining the JWT config (secret/issuer/audience) with the OAuth section (lifetimes/etc.). Add to base + Production + local example, all disabled.
3. **DI registration in `Program.cs`** (gated by `if (oauthCfg.Enabled)`):
   - Register the **five store interfaces as singletons, one shared instance each** (grilled — load-bearing: the device-code flow creates a record in one request and reads/mutates it across later `/token` + `/device/approve` requests; a scoped/transient or duplicate registration gives each request a fresh empty in-memory store and *nothing resolves*). The singleton `OAuthServer` captures these same singletons; do NOT also register them with a different lifetime for any TASK-032 consumer — resolve the same singleton. Register by interface so TASK-031 swaps SQLite in cleanly (mirror the `IProjectRepository` SQLite-or-fallback lambda pattern). For now each returns an **in-memory** impl.
   - ⚠ The ready-made `InMemoryStore` lives in the *Birko test* project (not shippable) → add a small server-side `Auth/InMemoryOAuthStores.cs` (generic `IAsyncStore<T>` + five shims) so prod compiles standalone; TASK-031 just changes the lambda bodies to SQLite.
   - Register `OAuthServer` singleton via factory resolving the five stores + existing `ITokenProvider` + `TokenOptions`/`OAuthServerSettings` from config + the existing `SystemDateTimeProvider` clock.
4. **`Auth/OAuthEndpoints.cs`** (NEW, mirror `AuthEndpoints.cs`) — `MapOAuthEndpoints(this WebApplication)`:
   - `POST /token` — `ReadFormAsync` (+ HTTP Basic fallback for client creds) → `TokenRequest` → `server.Token.HandleAsync`; catch `OAuthServerException` → `Results.Json(TokenErrorResponse.From(ex), 400)`.
   - `POST /device_authorization` → `DeviceAuthorizationRequest`.
   - `POST /register` (RFC 7591) → `ClientRegistrationRequest` — **only mapped when `AllowDynamicRegistration: true`** (separate flag, default `false`, *independent* of `OAuth:Enabled`; grilled — open dynamic registration lets anyone mint a confidential client, so enabling OAuth must NOT expose it). Even when mapped, admin-auth gating still lands in TASK-032. Clients are otherwise created by seeding the store / future admin UI.
   - `GET /authorize` → `AuthorizeRequest` (userId from principal; JSON result — see ⚠).
   - (per resolved #2) `POST /device/approve` — body `{ user_code, user_id, approved }` → `DeviceAuthorization.ApproveAsync`; **unguarded** with a `// TODO(TASK-032): require auth + principal-derived userId` marker.
   - Wire in `Program.cs` after the JWT block, guarded by `oauthCfg.Enabled`. No route collisions with `/`, `/auth/*`.
5. **JSON casing (load-bearing)** — Birko DTOs are PascalCase; minimal-API default emits camelCase, not RFC snake_case. Build response objects with **explicit snake_case** (`new { access_token = …, token_type = …, expires_in = … }`) per endpoint, per the OAuth.Server CLAUDE.md wire example. Don't rely on global JSON options.

### Tests (`DraCode.KoboldLair.Tests`, xUnit + FluentAssertions — already configured)
- Test `OAuthServer` handlers wired with in-memory stores directly (AC says "using the in-memory stores") + a **fake `IDateTimeProvider`** for expiry/`slow_down`. To reuse stores, either reference the server-side in-memory stores (factor into `DraCode.KoboldLair` library) or import the OAuth.Server `.projitems` + local in-memory store in the test project.
- Per grant type, success + invalid-grant: `client_credentials` (bad secret → `invalid_client`), `authorization_code`+PKCE (reused/expired/bad verifier → `invalid_grant`), `refresh_token` (rotation; replay revoked → `invalid_grant`), `device_code` (`authorization_pending` → approve → success; unknown → `invalid_grant`).
- Optional: `WebApplicationFactory<Program>` against `/token` with `Enabled:true` to assert snake_case JSON + 400 mapping.

### Risks
- Shared-project transitive imports (step 1) — resolve first.
- JSON casing leak — mitigated by explicit snake_case.
- In-memory store source (Birko test project, not shippable) — add server/library impl.
- `/authorize` + device-approve have no UI surface — see ⚠.
- Single signing key reuse — if separate keys chosen, TASK-032 needs multi-key validation.

### Verification
With `Authentication:OAuth:Enabled=true` + `KOBOLDLAIR_JWT_SECRET` set:
1. `dotnet build DraCode.KoboldLair.Server` + `dotnet test DraCode.KoboldLair.Tests` (all grant-type cases green).
2. `POST /register` a confidential client → `curl -X POST /token -d "grant_type=client_credentials&client_id=…&client_secret=…"` → `200` snake_case `access_token`; bad secret → `400 {"error":"invalid_client"}`.
3. With `Enabled=false` (default): OAuth routes 404, app starts unchanged.

### Critical files
`DraCode.KoboldLair.Server/Program.cs` · `DraCode.KoboldLair.Server/Auth/OAuthEndpoints.cs` (new) · `Auth/InMemoryOAuthStores.cs` (new) · `DraCode.KoboldLair.Server.csproj` · `appsettings.json` · (ref) `Birko.Security.OAuth.Server/Endpoints/Token/TokenEndpointHandler.cs`
