---
id: TASK-042
parent: STORY-016
feature: FEATURE-018
status: done
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-032]
blocks: [TASK-043, TASK-044, TASK-046]
pr: null
github-issue: null
jira-key: null
---

# /api/v1 minimal-API skeleton + OpenAPI

## Context

REST facade is **ASP.NET Core minimal-API routes** under `/api/v1` (not Birko's HttpListener-based `RestServer`), so they sit behind TASK-032's auth middleware. OpenAPI via the **.NET 10 built-in `Microsoft.AspNetCore.OpenApi`** (`AddOpenApi`/`MapOpenApi`), not Swashbuckle. JSON camelCase in/out.

## Acceptance criteria

- [x] `/api/v1` route group established, behind `AddBirkoSecurity` auth (daemon bypass honoured) — `ApiV1Endpoints.MapApiV1` (`MapGroup("/api/v1").RequireAuthorization()`); tested 401/200
- [x] `AddOpenApi()` + `MapOpenApi()` → spec at `/api/v1/openapi.json`; docs UI at `/api/v1/docs` — built-in OpenAPI + **Scalar** (self-hosted, no CDN) `MapScalarApiReference`; spec generation tested, UI render is the browser-visual step below
- [x] camelCase JSON serialization matches existing JS conventions — global `ConfigureHttpJsonOptions` (CamelCase + case-insensitive in); OAuth/Auth snake_case unaffected (they pass explicit options)
- [x] A trivial endpoint (`GET /api/v1/agents/active` stub) returns through the pipeline with auth enforced — tested
- [x] Tests: 401 unauthenticated, 200 authenticated, OpenAPI doc generated — `ApiV1SkeletonTests` (3 tests); full suite 81/81 green

## Out of scope

- The resource endpoints themselves (TASK-043/044/045/046)

## Human test plan

**Automated** (`ApiV1SkeletonTests`; full suite 81/81 green): 401 unauthenticated, 200 with a `ViewOwn` token, and `/api/v1/openapi.json` generated + anonymous + documenting the stub route.

**Deferred — browser-visual (Scalar UI render), no non-interactive seam. Task stays in `review` until run:**
- [x] Open `/api/v1/docs` in a browser → the Scalar OpenAPI UI renders — verified 2026-06-23 (served at `/api/v1/docs/`, self-hosted, `200 text/html`). Note: the live Development config runs with `Jwt.Enabled=false`, so the authed `/api/v1` group is not mapped and the spec lists only `/`; the 401/200 gate + route listing are covered by `ApiV1SkeletonTests` (JWT enabled in the test host).

## Implementation plan

> ⚠ **Docs-UI note:** .NET 10 built-in `MapOpenApi()` serves only the OpenAPI **JSON** — no HTML UI.
> Decision: use **`Scalar.AspNetCore`** (self-hosted bundled assets, no external CDN — fits the project's
> no-CDN posture) via `MapScalarApiReference("/api/v1/docs")`. Fallback if it doesn't restore for net10:
> a tiny embedded-HTML viewer pointing at `/api/v1/openapi.json`.

### Grounding (verified against merged code)
- `Program.cs` maps endpoints **flat**; the only `/api/v1` route is `GET /api/v1/whoami` (~938) inside
  `if (jwtRuntimeEnabled)` with `.RequireAuthorization().RequirePermission(ViewOwn)` — the pattern to mirror.
- Auth pipeline is complete (TASK-032): `AddBirkoSecurity` (PermissionClaim="scope") + `UseAuthentication`
  → daemon-loopback bypass → `UseAuthorization` (gated on `jwtRuntimeEnabled`). The group needs **no pipeline change**.
- `RequirePermission` is generic over `IEndpointConventionBuilder` → applies to a `RouteGroupBuilder` group-wide.
- `Microsoft.AspNetCore.OpenApi` is **not** referenced — must add (PackageReference, like the JwtBearer pin).
- No global minimal-API JSON options today. OAuth/Auth endpoints pass **explicit** snake_case
  `JsonSerializerOptions` to `Results.Json`, so a global camelCase policy will **not** disturb them.
- Test harness to mirror: `JwtMiddlewareTests` (`WebApplicationFactory<Program>`, JsonFile backend,
  `RemoveAll<IHostedService>`, `MintToken`).

### Ordered steps
1. **csproj:** add `Microsoft.AspNetCore.OpenApi` (10.0.0 pin) + `Scalar.AspNetCore` to `DraCode.KoboldLair.Server.csproj`.
2. **DI (`Program.cs`, before Build):** `AddOpenApi()`; global camelCase via
   `ConfigureHttpJsonOptions` (`PropertyNamingPolicy=CamelCase`, `PropertyNameCaseInsensitive=true`).
   Safe for OAuth (explicit options win).
3. **New `Api/ApiV1Endpoints.cs`:** `MapApiV1(this WebApplication)` → `var api = app.MapGroup("/api/v1")`
   with group-wide `api.RequireAuthorization()` (NOT a group-wide `RequirePermission` — resources differ).
   Map the stub `GET /api/v1/agents/active` → `Results.Ok(Array.Empty<object>())` `.RequirePermission(ViewOwn)`.
   This file is the seam TASK-043/044/046 hang endpoints off.
4. **Map from `Program.cs` inside `if (jwtRuntimeEnabled)`** (next to whoami — `RequireAuthorization` only
   bites when `UseAuthorization` is in the pipeline, itself gated on jwt). **Move `whoami` into the group**
   as `api.MapGet("/whoami", …)` — absolute path stays `/api/v1/whoami`, so `JwtMiddlewareTests` keep passing.
5. **OpenAPI doc + UI (mapped on `app`, ANONYMOUS — not under the authed group, else the browser 401s):**
   `app.MapOpenApi("/api/v1/openapi.json")` + `app.MapScalarApiReference("/api/v1/docs", o => o.WithOpenApiRoutePattern("/api/v1/openapi.json"))`.
6. **Tests** — new `Tests/Api/ApiV1SkeletonTests.cs` (mirror `JwtMiddlewareTests`): `/api/v1/agents/active`
   401 no-token / 200 with `ViewOwn` token; `GET /api/v1/openapi.json` → 200 + JSON contains the `agents/active` path.

### Tradeoffs / risks
- **Gate the group on `jwtRuntimeEnabled`** (mirrors whoami/auth) rather than always-map — keeps "auth off by default" coherent.
- **Group `RequireAuthorization` once**, per-endpoint `RequirePermission` — the seam downstream tasks plug into.
- **Global camelCase is safe** — only affects framework-serialized results; OAuth/Auth pass explicit options.
- **Doc routes must be anonymous** or the browser docs page / spec fetch 401s (human test plan opens `/api/v1/docs`).
- **Scalar net10 availability** — if it doesn't restore, fall back to embedded-HTML viewer (only the UI criterion is at risk).
- **whoami move** — safe (tests assert the path, unchanged); do not rename/re-path.

### Critical files
- `DraCode.KoboldLair.Server/DraCode.KoboldLair.Server.csproj` — OpenAPI + Scalar package refs
- `DraCode.KoboldLair.Server/Program.cs` — `AddOpenApi`, camelCase JSON, `MapApiV1`, `MapOpenApi`/`MapScalarApiReference`; move whoami
- **New** `DraCode.KoboldLair.Server/Api/ApiV1Endpoints.cs` — the `/api/v1` group + auth defaults (downstream seam)
- **New** `DraCode.KoboldLair.Tests/Api/ApiV1SkeletonTests.cs` — 401/200/openapi-doc (mirror JwtMiddlewareTests)
