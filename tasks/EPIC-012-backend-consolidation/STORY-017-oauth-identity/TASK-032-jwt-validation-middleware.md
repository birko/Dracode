---
id: TASK-032
parent: STORY-017
feature: FEATURE-019
status: done
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

- [x] `AddBirkoSecurity(...)` registered; `app.UseAuthentication()` + `app.UseAuthorization()` added to the pipeline (auth services registered unconditionally; middleware added when `Authentication:Jwt:Enabled`)
- [x] Protected: `/api/v1/*` (STORY-016), `/dragon`, `/wyvern`, `/kobold` — `/api/v1/whoami` seeds and proves the `/api/v1` surface; `/dragon`+`/wyvern` retain legacy WS auth (reconciliation is out of scope, STORY-015); `/api/v1/*` resource routes and `/kobold` attach `.RequireAuthorization()`/`.RequirePermission()` when their stories land (the pipeline now enforces them by construction)
- [x] **Daemon loopback bypass**: gated by `Authentication:Daemon:LoopbackBypass` + an all-loopback bind check (`DaemonLoopback.ShouldBypass`); injects a synthetic `*`-permission principal. Daemon-mode bind wiring arrives with TASK-047/049; until then the flag + bind inspection drive it
- [x] Service-account `scopes` enforced at endpoints via `PermissionEndpointFilter` (`PermissionClaim = "scope"`; login emits expanded role permissions in the same claim). Known limitation: space-delimited multi-scope OAuth tokens aren't split (single scope works) — follow-up
- [x] `?token=` auth confirmed working (covered by `Query_string_token_authenticates_for_sse_ws_reuse`; sets up SSE reuse in TASK-045)
- [x] **Harden the OAuth `/device/approve` route from TASK-030**: now `.RequireAuthorization()`, derives `userId` from the principal `sub`, body `user_id` dropped, `// TODO(TASK-032)` removed. `/register` (RFC 7591) gated behind `.RequirePermission(ManageUsers)`; `/authorize` tightened to principal-only as a consistency follow-on
- [x] Tests: 401 without token, 200 with valid token, bypass under loopback daemon (+ 403 without permission, `?token=`, login round-trip, and `DaemonLoopback` unit tests) — 10 new tests, all green

## Out of scope

- Reconciling the two auth paths on WS endpoints beyond enabling (Birko WS middleware vs JWT bearer) — track in STORY-015 open question
- Removing the legacy auth (TASK-036)

## Human test plan

The 401 / 200 / `?token=` / 403 / loopback-bypass behaviours are all covered by automated
integration tests (`JwtMiddlewareTests`, `WebApplicationFactory`-hosted) + `DaemonLoopbackTests`
unit tests — 10 tests, green. The literal `--daemon` flag (TASK-047/049) and `/api/v1/projects`
(STORY-016) don't exist yet, so the manual smoke below targets the equivalent existing surface:

- [x] Enforce: server with `Jwt:Enabled=true`, bypass off → `curl /api/v1/whoami` no-token 401, bad-token 401, valid bearer 200 (body shows perms resolved from the scope claim). Verified on real Kestrel 2026-06-18.
- [x] Loopback bypass: `Jwt:Enabled=true` + `Daemon:LoopbackBypass=true` bound to `127.0.0.1` → no-token returns 200 (synthetic `*` principal; startup warning logged). Verified 2026-06-18.
- [x] Safety: bypass on but bound to `0.0.0.0` → no-token returns 401 (non-loopback bind enforces despite the flag). Verified 2026-06-18.
- [ ] (Deferred) Re-run the literal `--daemon` / `/api/v1/projects` smoke once daemon mode (TASK-047/049) and REST resources (STORY-016) land

## Implementation plan

⚠ **Acceptance criteria question:** Three of the protected resources do not exist in the codebase yet — `/api/v1/*` (STORY-016, not done), `/kobold` (STORY-015, not done), and the OAuth-issued/login tokens currently carry **no `permission` claim** (only a comma-joined `roles` claim). Criterion 2 (`Protected: /api/v1/*, /dragon, /wyvern, /kobold`) and criterion 4 (scopes enforced via `PermissionEndpointFilter`) are therefore only partially satisfiable today: `/dragon`+`/wyvern` exist; `/api/v1/*` and `/kobold` can only be "protected by construction" (the middleware is on, routes get added protected later). And `PermissionEndpointFilter` will reject everything unless the token-minting path emits a `permission`/scope claim. The plan below enables the middleware and wires the filter on a representative endpoint, and flags the claim-emission gap explicitly. **Confirm this scoping is acceptable.**

### What exists today (grounded)

- `Server/Program.cs` builds the pipeline with **no `UseAuthentication`/`UseAuthorization`**. It registers JWT plumbing manually (`ITokenProvider`, `IPasswordHasher`, `IRoleProvider`, `IPermissionChecker` → `KoboldLairPermissionChecker`, `RefreshTokenStore`) but never adds the ASP.NET auth scheme. Pipeline tail (~812-862): `MapDefaultEndpoints` → `UseCors` → `MapAuthEndpoints` (if `Jwt.Enabled`) → `MapOAuthEndpoints` (if `OAuth.Enabled`) → `UseWebSockets` → `MapWebSocket("/wyvern"|"/dragon", …, requireAuthentication: true)` → `MapGet("/")` → `app.Run()`.
- `/dragon` & `/wyvern` use the **legacy** `MapWebSocket` extension (`Birko.Communication.WebSocket`), a branched `app.Map(...)` sub-pipeline that auths via `WebSocketAuthenticationService` (legacy `?token=` + IP allowlist) and **never flows through `UseAuthorization`**. This is the dual-auth tension the task marks out of scope (STORY-015 open question).
- `Birko.Security.AspNetCore` is **not yet in the build.** `DraCode.Birko.csproj` imports Birko.Security/.Jwt/.OAuth.Server but not `.AspNetCore`. Its projitems (`C:\Source\Birko.Security.AspNetCore\…projitems`) has no `FrameworkReference` — depends on the host Web SDK. Must import into the **Server** project (`Microsoft.NET.Sdk.Web`), not DraCode.Birko (plain SDK, would fail to resolve `Microsoft.AspNetCore.*`).
- `AddBirkoJwtBearer` already wires `OnMessageReceived` to read `?token=` → WS/SSE query-token criterion is satisfied by the framework for endpoints that flow through the bearer middleware.
- `AddBirkoSecurity` registers `IPermissionChecker`→`ClaimsPermissionChecker` (scoped) & `ICurrentUser`→`ClaimsCurrentUser` (scoped). Server already has `IPermissionChecker`→`KoboldLairPermissionChecker` (singleton) — a real collision to resolve.
- `PermissionEndpointFilter` reads `ICurrentUser.Permissions` (the `permission` claim) + `"*"`. Minting paths emit no such claim (`AuthEndpoints` → `sub`/`name`/`roles`; OAuth → `scope`). **Gap:** without a `permission`/scope mapping the filter forbids everyone.
- **No daemon mode / loopback binding exists yet** (TASK-047/048/049 not done). The loopback bypass must be a **config hook**, not real daemon integration.
- OAuth (`Auth/OAuthEndpoints.cs`): `/device/approve` (line ~108, unguarded, `// ⚠ TODO(TASK-032)` at 105-107, takes `DeviceApproveRequest.UserId` from body); `/register` (line ~37, mapped only when `allowDynamicRegistration`, TODO at 36); `/authorize` (line ~134 already prefers `User.FindFirst("sub")`, falls back to `?user_id=`).
- Tests: `DraCode.KoboldLair.Tests` has **no** `Microsoft.AspNetCore.Mvc.Testing` / `WebApplicationFactory`; existing auth tests are handler-level unit tests. `Program` is top-level statements (no `public partial class Program`).

### Steps

1. **Bring `Birko.Security.AspNetCore` into the build.** Add to `DraCode.KoboldLair.Server.csproj` (alongside the WS/SSE projitems block, ~lines 18-22):
   `<Import Project="$(BirkoSrc)\Birko.Security.AspNetCore\Birko.Security.AspNetCore.projitems" Label="Shared" />`
   Keep it in the web host, **not** DraCode.Birko. Risk: a `FrameworkReference Include="Microsoft.AspNetCore.App"` may be needed if the Web SDK's implicit ref doesn't cover `…Authentication.JwtBearer` — verify by building; add to Server csproj if `AddJwtBearer` fails to resolve.

2. **Register `AddBirkoSecurity` in DI** (Program.cs, after the JWT block ~52-76), reusing the existing `JwtAuthenticationConfiguration` (same Secret/Issuer/Audience so OAuth/login tokens validate by construction). Mirror `Symbio.Api/Program.cs` ~85-91. `AddBirkoJwtBearer` throws on empty Secret → keep the dev-key fallback (matches existing `ITokenProvider` fallback at line 62).
   **Resolve the `IPermissionChecker` collision deliberately:** prefer keeping `ClaimsPermissionChecker` (claims-based, matches the filter + the "scopes" intent) and **remove** the `KoboldLairPermissionChecker` registration. (Role provider left registered is harmless.)

3. **Add the middleware + loopback bypass** (after `UseCors()` ~line 815, before `MapAuthEndpoints`/`MapOAuthEndpoints`). Daemon mode doesn't exist → gate via config: add `Authentication:Daemon:LoopbackBypass` (default false) + bind-address inspection.
   ```csharp
   if (jwtCfg.Enabled) {
       app.UseAuthentication();   // safe to always run — only populates User from a bearer token if present
       var bypass = ResolveLoopbackBypass(app);   // flag AND every bound host is 127.0.0.1/localhost/::1
       if (bypass)
           app.Use(async (ctx, next) => {  // synthetic authenticated principal so RequireAuthorization + PermissionEndpointFilter both pass
               ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
                   [ new Claim(JwtClaimNames.Permission, "*"), new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()) ],
                   authenticationType: "DaemonLoopback"));
               await next();
           });
       app.UseAuthorization();
   }
   ```
   The synthetic-principal form (vs conditionally omitting `UseAuthorization`) keeps one pipeline shape and actually satisfies "validation is skipped" while keeping the permission filter functional. `ResolveLoopbackBypass`: a small static helper (new `Auth/DaemonLoopback.cs`) reading configured URLs (`app.Urls`/`ASPNETCORE_URLS`) — bypass only when *flag on* AND *every bind loopback*; any non-loopback bind enforces. Emit a startup warning when bypass is active.

4. **Protect the endpoints.**
   - `/dragon`, `/wyvern`: legacy branched pipeline — **do not** reconcile the two WS auth stacks (out of scope). Already gated by `requireAuthentication: true`. Document that they retain legacy auth; the bearer `?token=` proof is demonstrated on a non-branched route for TASK-045 SSE reuse.
   - `/api/v1/*` & `/kobold` (not built yet): can't be mapped here; with `UseAuthorization()` present, future routes attach `.RequireAuthorization()`/`.RequirePermission(...)`. Leave a documented seam.
   - Demonstrate `PermissionEndpointFilter`: attach `.RequirePermission(...)` to one real endpoint — this surfaces the claim gap (Step 6).

5. **Harden OAuth routes** (`Auth/OAuthEndpoints.cs`):
   - `/device/approve`: remove the `// ⚠ TODO(TASK-032)`; derive `userId` from the principal (`ICurrentUser.UserId` / `User.FindFirst("sub")`), `Results.Unauthorized()` when unauthenticated, drop `UserId` from `DeviceApproveRequest`, add `.RequireAuthorization()`.
   - `/register`: keep `allowDynamicRegistration` gate **and** add `.RequireAuthorization()` + admin check (`.RequirePermission(...ManageUsers)` or admin scope). Remove the TODO.
   - `/authorize`: consistency follow-on — tighten to `User.FindFirst("sub")`, drop `?user_id=` fallback, add `.RequireAuthorization()`. (Call out as follow-on; not silently expanded scope.)
   - These map only when `OAuth:Enabled`, so inert under default. Note bypass interaction: under loopback bypass the synthetic principal must carry the admin permission for `/register`, or exclude `/register` from bypass.

6. **Close the permission-claim gap** (required for criterion 4 to actually pass). Do **both**: (a) in `AuthEndpoints.HandleLogin`/`HandleRefresh` emit a `permission` claim by expanding the user's roles through `KoboldLairPermissionChecker.RolePermissions` (comma-joined; `ClaimsCurrentUser` splits it); (b) configure `ClaimMappingOptions` so service-account `scope`→permission maps (serves "service-account scopes enforced"). Set via `AddBirkoSecurity` options' `Jwt.Claims` in Step 2.

7. **Tests** (`DraCode.KoboldLair.Tests`): add `Microsoft.AspNetCore.Mvc.Testing`; add `public partial class Program { }` to end of Program.cs so `WebApplicationFactory<Program>` works. New `Auth/JwtMiddlewareTests.cs`: 401 without token; 200 with valid token (mint via `ITokenProvider` or POST `/auth/login`); `?token=` query authenticates (proves `OnMessageReceived`); loopback bypass → 200 with no token; `PermissionEndpointFilter` → 403 without permission, 200 with. **Risk:** the factory boots all hosted services + SQLite migration — neutralize background services via `WithWebHostBuilder`/`ConfigureServices` and point data at temp/in-memory, else tests are slow/flaky.

### Ordering
1 (csproj import) → 2+6 (DI + claim mapping + checker collision) → 3 (pipeline + bypass) → 4+5 (endpoint protection + OAuth hardening) → 7 (test infra + tests).

### Key risks
- **Dual WS auth paths** — enabling bearer does NOT retroactively JWT-protect `/dragon`/`/wyvern` (branched legacy pipeline); they stay legacy-token. Be explicit in the PR.
- **Loopback bypass without daemon mode** — config flag + bind inspection; require *both* flag and all-loopback bind; startup warning when active.
- **Permission-claim gap** — the "it builds but everything 403s" trap; Step 6 is mandatory.
- **`IPermissionChecker` double-registration** — resolve intentionally.
- **Test factory weight** — needs service neutralization to be reliable.

### Critical files
- `DraCode.KoboldLair.Server/Program.cs`
- `DraCode.KoboldLair.Server/Auth/OAuthEndpoints.cs`
- `DraCode.KoboldLair.Server/Auth/AuthEndpoints.cs`
- `DraCode.KoboldLair.Server/DraCode.KoboldLair.Server.csproj`
- `DraCode.KoboldLair.Tests/DraCode.KoboldLair.Tests.csproj` (+ new `Auth/JwtMiddlewareTests.cs`)
- Read-only refs: `Birko.Security.AspNetCore/{Extensions/SecurityServiceExtensions.cs, Authentication/JwtBearerExtensions.cs, Authorization/PermissionEndpointFilter.cs, User/ClaimsCurrentUser.cs, User/ClaimMappingOptions.cs}`
