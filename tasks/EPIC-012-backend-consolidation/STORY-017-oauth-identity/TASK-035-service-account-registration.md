---
id: TASK-035
parent: STORY-017
feature: FEATURE-019
status: review
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-030, TASK-032]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Service-account client registration

## Context

Non-human callers (Discord bot, CI) authenticate via `client_credentials` confidential clients with scoped tokens (`{ sub: "service:discord-bot", scopes: ["projects:write","runs:write"] }`). Operators register these clients via the Web UI and/or `koboldlair keys create --service "discord-bot"`.

## Acceptance criteria

- [x] Operator can register a confidential OAuth client — server `POST /register` (admin-gated, existing); Web UI form is TASK-056 and CLI `keys create` is EPIC-013, both thin callers (flagged out of scope)
- [x] Issued client_credentials token carries `sub: "service:<name>"` + requested `scopes` — `ServiceAccountTokenIssuer` (D14); tested (decodes `sub == service:discord-bot`)
- [x] Scopes enforced at endpoints (via TASK-032's `PermissionEndpointFilter`) — tested: in-scope 200 (`whoami`/`view_own`) vs out-of-scope 403 (`/register`/`manage_users`)
- [x] Client secret shown once on creation, stored hashed (server's SHA-256 hashing) — existing `/register`; tested secret returned once
- [x] Revocation: operator can disable a client without affecting others — `POST /register/{clientId}/disable` (per-client `IsEnabled=false`); tested disable → `/token` `invalid_client`
- [x] Tests: scoped token allows in-scope calls, rejects out-of-scope (403) — `ServiceAccountTests` (5 tests); full suite 78/78 green

## Out of scope

- The Discord bot itself (EPIC-013 / STORY-020)
- Human OAuth (TASK-033)

## Human test plan

**Automated** (covered by `ServiceAccountTests`; full suite 78/78 green) — the enforcement *mechanism* is exercised end-to-end over HTTP: register → `client_credentials` → in-scope 200 / out-of-scope 403 → disable → `invalid_client`.

**Deferred — the literal scenario needs surfaces from other tasks (CLI `keys create` = EPIC-013; `/api/v1/runs` + `DELETE /api/v1/projects` = STORY-016 TASK-043/044). Task stays in `review` until they exist:**
- [ ] Create a service client via CLI, use its token to `POST /api/v1/runs` (in-scope, succeeds) and `DELETE /api/v1/projects/{id}` (out-of-scope, 403); disable the client → token rejected

## Implementation plan

> ⚠ **Acceptance criteria questions (flagged, not edited):**
> 1. **`sub: "service:<name>"` is not achievable through the stock framework path.**
>    `TokenEndpointHandler.HandleClientCredentialsAsync` hardcodes `subject = client.ClientId`
>    (a random id). Stamping `service:<name>` needs either a consumer-side token issuer (Option A)
>    or a framework change (Option B). **Owner decision required.**
> 2. **The example scopes `projects:write`/`runs:write` don't exist as an enforcement vocabulary.**
>    `PermissionEndpointFilter` checks the comma-joined `scope` claim against permission *constants*
>    (`manage_projects`, `execute_agents`, `view_own`, …). There is no `projects:write` anywhere.
>    Also: the framework's client-credentials path emits `scope` **space-joined**, but
>    `ClaimsCurrentUser` splits **only on commas** — so a stock multi-scope service token would not
>    enforce. **Owner decision required** (use permission constants, or add a scope→permission map).

### What already exists (so several criteria are partly met)
- **Registration:** `POST /register` (`OAuthEndpoints.HandleRegisterAsync` → `server.ClientRegistration.RegisterAsync`)
  creates a confidential client, generates a 32-byte secret, **hashes it SHA-256**, persists via
  `IOAuthClientStore`, returns the plaintext **once**. Already gated `.RequirePermission(ManageUsers)`.
  → criteria "register confidential client" + "secret shown once / SHA-256 hashed" are essentially done.
- **Revocation flag:** `OAuthClient.IsEnabled` + `OAuthClientEntity.IsEnabled` round-trip in `SqlOAuthClientStore`;
  `AuthenticateClientAsync` already rejects `!IsEnabled`. **Missing:** an operator endpoint to flip it.
- **Enforcement:** `PermissionClaim = "scope"`; `ClaimsCurrentUser` comma-splits it; `PermissionEndpointFilter`
  checks `Permissions.Contains(required)` — same mechanic the human path (TASK-033/034) uses.

### Architectural decision (the crux) — reuse `/register`, but own service-token issuance
Reuse `RegisterAsync` for registration. For the token, the stock client-credentials grant can't produce
`service:<name>` or a comma-joined permission scope, so:
- **Option A (recommended): consumer-side service-token issuer.** Branch `POST /token` on
  `grant_type=client_credentials` to a new `ServiceAccountTokenIssuer` that validates the client
  (secret/`IsEnabled`/grant/scope-narrow, mirroring `AuthenticateClientAsync`), then mints via the existing
  `ITokenProvider` with `sub = "service:" + client.Name`, `scope = string.Join(",", grantedScopes)`. Framework
  untouched; token validates under the existing JWT middleware. Trade-off: duplicates a little client-auth logic.
- **Option B: framework change** — injectable subject/scope strategy on `TokenEndpointHandler`. Cleaner but forks
  `Birko.Security.OAuth.Server` and risks the other three grants. Defer to a framework task.

### Ordered steps (assuming Option A + permission-constant scopes)
1. **Service name:** reuse `OAuthClient.Name` as `<name>` (already round-trips; no schema change). Convention:
   a client is a service account when its grant type is `client_credentials`; `sub = "service:" + Name`.
2. **`ServiceAccountTokenIssuer`** (new, `Auth/`): validate client (load via `IOAuthClientStore.GetByClientIdAsync`;
   reject null/`!IsEnabled`/no `client_credentials` grant/secret mismatch), narrow requested scopes against
   `client.AllowedScopes`, mint via `ITokenProvider` with `sub="service:"+Name` + comma-joined permission scopes.
3. **Wire into `OAuthEndpoints.HandleTokenAsync`:** branch `client_credentials` → issuer; other grants still go to
   `server.Token.HandleAsync` (public `/token` contract unchanged).
4. **Scope vocabulary:** register service clients with permission-constant `AllowedScopes`
   (`manage_projects`, …) — document that the criteria's `projects:write` is illustrative. (Alt: scope→permission map.)
5. **Revocation endpoint** (new in `OAuthEndpoints`, `.RequirePermission(ManageUsers)`): load client, set
   `IsEnabled=false`, `UpdateAsync`. Per-row, doesn't affect others; token path already rejects disabled clients.
6. **Tests** (`Tests/Auth/`, `WebApplicationFactory<Program>` + in-memory stores): register → secret-once;
   `client_credentials` → `sub="service:<name>"` + comma-joined scope; **in-scope 200 / out-of-scope 403** (reuse
   the `whoami` + `.RequirePermission` style); disable → `/token` returns `invalid_client`.

### Cross-task scope tangles (do NOT absorb silently)
- **CLI `koboldlair keys create`** belongs to the CLI client (EPIC-013 / STORY-019, TASK-050+) — **not in this repo yet.**
  In-scope deliverable here = the **server-side registration + service-token + revocation API**; the CLI is a thin caller.
- **"Web UI form"** is TASK-056 territory (same as TASK-033's button). Server API is the real work.

### Risks
- **`ClientSecretHasher` is `internal`** to the framework — the consumer issuer can't call `Verify`/`Hash`; re-implement
  the identical SHA-256-hex (matches "server's SHA-256 hashing") or verify only via the stock path. Confirm visibility.
- **Duplicated client-auth** (Option A) can drift from `AuthenticateClientAsync` — add a parity test (wrong secret →
  `invalid_client`, disabled → rejected).
- **Revocation latency** — disabling doesn't invalidate already-issued JWTs (no introspection); only blocks new token
  requests until expiry (`AccessTokenLifetimeSeconds`, default 3600). The human test plan's "disable → token rejected"
  holds for *new* requests; document.
- **Comma vs. space join** — the issuer MUST comma-join; routing service tokens through stock `server.Token.HandleAsync`
  would silently break multi-scope enforcement.

### Critical files
- `DraCode.KoboldLair.Server/Auth/OAuthEndpoints.cs` — branch `/token` for `client_credentials`; add revocation endpoint
- **New:** `DraCode.KoboldLair.Server/Auth/ServiceAccountTokenIssuer.cs`
- `DraCode.KoboldLair.Server/Auth/KoboldLairPermissionChecker.cs` — scope vocabulary / optional scope→permission map
- `DraCode.KoboldLair.Server/Program.cs` — DI for the issuer (reuse `ITokenProvider`); representative protected endpoints
- `DraCode.KoboldLair.Tests/Auth/` — in-scope/out-of-scope, secret-once, disable→reject (mirror `JwtMiddlewareTests`/`OAuthServerTests`)
- `Birko.Security.OAuth.Server/Endpoints/Token/TokenEndpointHandler.cs` — reference for client-auth + scope-narrow parity (the `subject = client.ClientId` line that necessitates Option A)
