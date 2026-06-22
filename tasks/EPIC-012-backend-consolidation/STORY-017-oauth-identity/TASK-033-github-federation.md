---
id: TASK-033
parent: STORY-017
feature: FEATURE-019
status: review
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-030]
blocks: [TASK-036]
pr: null
github-issue: null
jira-key: null
---

# GitHub federation for human login

## Context

Human login federates to GitHub via the existing `GitHubOAuthProvider` (`Birko.Communication.OAuth.Providers`). DraCode consumes the upstream GitHub identity, then mints its **own** tokens (TASK-030's server) for its clients. The stable GitHub `sub` maps to a DraCode `User` (TASK-034).

## Acceptance criteria

- [x] `GitHubOAuthProvider` wired with client id/secret config (env vars, disabled-by-default) — `GitHubFederationConfiguration` (`${ENV_VAR}` expansion), `appsettings.json` block, endpoints gated behind `Enabled` + `Jwt.Enabled`
- [x] Web UI login redirects to GitHub OAuth; callback completes the flow and issues a DraCode token — `/auth/github/login` + `/auth/github/callback` (state-guarded → SPA fragment redirect); button UI is TASK-056
- [x] Upstream GitHub `sub` resolved → DraCode `User` (created on first login; reused after) — `GitHubFederationService` upserts `User{ Sub="github:{id}" }`; covered by service tests
- [x] CLI device-code flow (TASK-030) and GitHub web flow both terminate in a DraCode-issued token — web flow implemented+tested; device flow verified to need no GitHub-specific code (decision 5, transitively allowlist-gated)
- [x] Tests cover the callback → user-resolution path (GitHub calls mocked) — `GitHubFederationTests` (6 tests: mint/reuse/deny/no-repo + callback redirect + bad-state); full suite 73/73 green

## Out of scope

- The `User` entity schema + `ownerId` (TASK-034)
- Additional IdPs (Microsoft/Google) — config-extensible but not implemented here

## Human test plan

**Automated** (covered by `GitHubFederationTests` + the existing `JwtMiddlewareTests`; full suite 73/73 green):
- [x] Callback with a valid `state` federates and 302-redirects to the SPA with `#access_token=`; that token authenticates `/api/v1/whoami`
- [x] Unknown/expired `state` → 400; GitHub id not on the allowlist → denied, no token, no `User` row; first login creates the user, second reuses it
- [x] `RefreshTokenStore` widening (D12) didn't regress the existing JWT login/refresh round-trip

**Deferred to live verification — requires a registered GitHub OAuth app + running server + browser, which has no non-interactive seam. Task stays in `review` until run:**
- [ ] Click "Login with GitHub" in the Web UI (TASK-056) → complete GitHub consent → land back authenticated with a DraCode session
- [ ] Run `koboldlair login` (device flow) → approve in browser (GitHub login) → CLI receives and caches a token

## Implementation plan

> ✅ **Design decisions (confirmed with owner via grill, 2026-06-22):**
> 1. **World A — `sub` stays the string `github:{githubId}`.** Federated identity uses the same
>    string-`sub` model the whole codebase adopted in TASK-034 (FEATURE-019 D9: own by raw `sub`,
>    not an internal Guid). The GitHub web callback **mints a DraCode JWT directly** via
>    `ITokenProvider.GenerateToken`, mirroring `AuthEndpoints.HandleLogin`. World B (mint a
>    DraCode-internal Guid identity, demote `github:{id}` to a mapping) was rejected for this task —
>    architecturally cleaner but reopens the just-approved D9; revisit later as its own refactor.
> 2. **`RefreshTokenStore` widens `Guid UserId` → `string OwnerSub`.** Required, not optional: on
>    `/auth/refresh` the store **re-emits its stored key as the next token's `sub`**
>    (`HandleRefresh`: `sub = entry.UserId.ToString()`), so to preserve `github:{id}` across refresh
>    the store must carry the string sub. This is the lone Guid holdout in the post-TASK-034 identity
>    model; widening it makes the store consistent with `User`/`Project.OwnerId`. **Touches the
>    existing `/auth/login` + `/auth/refresh` paths** (config users now pass `Id.ToString()`).
> 3. **Admission control = numeric-id allowlist (deny-all default).** GitHub OAuth authenticates *who*,
>    not *whether allowed*. `AllowedGitHubIds: List<long>` on the config; empty ⇒ **deny all**. Enforced
>    in `GitHubFederationService` right after the userinfo call — `gh.Id` not in list ⇒ `403`, no token,
>    no `User` row. Keyed on the stable numeric id (usernames are mutable).
> 4. **Web callback hands the token to the SPA via URL fragment.** `GET /auth/github/callback` ends in a
>    `302` to `{PostLoginRedirectUri}#access_token=…&refresh_token=…`. The fragment never reaches the
>    server/logs; the SPA reads `location.hash`, stores it as `authToken`, strips it. **TASK-033 owns the
>    redirect + `PostLoginRedirectUri` config; TASK-056 owns** the "Login with GitHub" button + hash
>    bootstrap. Accepted: token-in-fragment is visible in browser history — fine for a loopback-first
>    self-hosted tool, mitigated by short access-token lifetime.
> 5. **Device flow (AC #4) needs no GitHub-specific code.** The CLI device grant already mints a
>    DraCode token subjected to the approver (`TokenEndpointHandler` issues `sub = device.UserId`, set by
>    `ApproveAsync` from `ctx.User`). The approver authenticates in-browser via *this task's* GitHub web
>    login, so their `sub` is `github:{id}`, and the allowlist is enforced **transitively** (the only way
>    to get a JWT to authorize `/device/approve` is the gated web flow). Standard device-flow UX.
> 6. **Web-flow authorize endpoint kept local to DraCode** (reuse `GitHubOAuthProvider.TokenEndpoint`,
>    define the authorize URL locally) — no framework edit for one consumer. Follow-up: upstream a
>    `CreateWebFlowSettings` helper to `GitHubOAuthProvider`.
> 7. **CSRF `state` = dedicated in-memory `ConcurrentDictionary<string,DateTime>`**, short TTL +
>    single-use; not `RefreshTokenStore` (different lifetime/concern). Single-process daemon; losing
>    state on restart mid-login is acceptable (login is seconds-scale).

### How the existing pieces fit (grounded)
- **DraCode mints its own tokens** via `AuthEndpoints.HandleLogin` (`/auth/login`) and the OAuth server
  (`OAuthServer.Token.HandleAsync`). Both share the JWT secret/issuer/audience, so any token validates
  under the TASK-032 bearer middleware. Federation terminates in the **direct-mint** path (decision 1).
- **`GitHubOAuthProvider`** (`Birko.Communication.OAuth.Providers`) exposes only **device-flow** helpers +
  `TokenEndpoint` const. The underlying `OAuthClient` does the web flow (`BuildAuthorizationUrl(state)`,
  `ExchangeCodeAsync(code)`) if `AuthorizationEndpoint` is set (`https://github.com/login/oauth/authorize`).
- **GitHub's stable `sub`** = numeric `id` from `GET https://api.github.com/user` (separate authenticated
  call). No Birko helper — the **mockable seam** for tests.
- **User seam exists** (TASK-034): `IUserRepository.GetBySubAsync` / `UpsertAsync` keyed on `User.Sub`.
  Reuse it; it is `null!` off-SQLite (`Program.cs` ~260) → null-guard.

### Ordered steps
**Identity store (decision 2)**
1. **Widen `RefreshTokenStore`** — `Store(string refreshToken, string ownerSub, string username, List<string> roles, DateTime expiresAt)`; `RefreshTokenEntry.UserId: Guid → OwnerSub: string`; update `RevokeAllForUser`/lookups. Update the two existing callers in `AuthEndpoints` (`HandleLogin` passes `user.Id.ToString()`, `HandleRefresh` re-emits `entry.OwnerSub` as the new `sub`).

**Config (decisions 3, 4, 6, 7)**
2. New `Auth/GitHubFederationConfiguration.cs`, bound from `Authentication:GitHub`, **disabled-by-default**
   (`Enabled=false`): `ClientId`/`ClientSecret` with the `${ENV_VAR}` expansion of
   `JwtAuthenticationConfiguration.ResolveSecret()` (`${KOBOLDLAIR_GITHUB_CLIENT_ID}` /
   `${KOBOLDLAIR_GITHUB_CLIENT_SECRET}`), `RedirectUri`, `Scope` (default `read:user`), `DefaultRoles`
   (default `["user"]`), **`AllowedGitHubIds: List<long>` (default `[]` = deny-all)**, `PostLoginRedirectUri`.
   Add the disabled block to `appsettings.json` under `Authentication`; document env vars in `appsettings.local.example.json`.

**GitHub HTTP seams (mockable)**
3. New `Auth/GitHubUserInfoClient.cs`: `IGitHubUserInfoClient { Task<GitHubUserInfo> GetUserAsync(accessToken, ct) }`
   + `HttpClient` impl → `GET https://api.github.com/user` (sets `User-Agent`, `Authorization`,
   `Accept: application/vnd.github+json`). `GitHubUserInfo { long Id; string Login; string? Name; string? Email }`.
   Register `AddHttpClient<IGitHubUserInfoClient, GitHubUserInfoClient>()`.
4. New `IGitHubTokenExchanger` wrapping `OAuthClient.ExchangeCodeAsync` (concrete, not DI-friendly) so the
   endpoint test mocks the token exchange too.

**Federation service (decisions 1, 2, 3)**
5. New `Auth/GitHubFederationService.cs`, `FederateAsync(githubAccessToken) → LoginResponse`:
   `gh = userInfo.GetUserAsync(...)` → **allowlist gate: `gh.Id` ∉ `AllowedGitHubIds` ⇒ throw/`403`** (no
   token, no `User` row) → `sub = "github:{gh.Id}"` → `GetBySubAsync` else
   `UpsertAsync(new User{ Sub=sub, DisplayName=gh.Name??gh.Login, Email=gh.Email })` (AC #3; null-guard
   off-SQLite) → mint like `HandleLogin` (claims `sub`,`name`,`email`,`roles`,`scope=ExpandRolesToPermissions(DefaultRoles)`
   comma-joined; refresh token via the widened `RefreshTokenStore.Store(token, sub, …)`). **Primary test unit.**

**Endpoints (decisions 4, 6, 7)**
6. New `Auth/GitHubAuthEndpoints.cs`, `MapGitHubAuthEndpoints()`:
   - `GET /auth/github/login` → build authorize URL (`OAuthSettings` w/ `AuthorizationCode` grant + local
     GitHub authorize URL + `GitHubOAuthProvider.TokenEndpoint`), generate crypto-random `state`, store it in
     the in-memory TTL dict, `Results.Redirect(authUrl)`.
   - `GET /auth/github/callback?code=&state=` → **validate `state`** (mismatch/expiry ⇒ 400, single-use),
     `ExchangeCodeAsync(code)`, `FederateAsync(token.AccessToken)`, then **`302` to
     `{PostLoginRedirectUri}#access_token=…&refresh_token=…`** (decision 4).
7. New in-memory `OAuthStateStore` (`ConcurrentDictionary<string,DateTime>`, TTL + single-use consume).

**Wire `Program.cs`**
8. `Configure<GitHubFederationConfiguration>("Authentication:GitHub")` (unconditional, inert when disabled);
   register the user-info client, token exchanger, federation service, state store. After `MapAuthEndpoints()`,
   add a gated block mirroring the OAuth one (~932): `if (githubConfig.Enabled)` → fail fast unless
   `jwtRuntimeEnabled` (federation mints JWTs the bearer middleware validates) → `app.MapGitHubAuthEndpoints()`.

**Device flow (decision 5)** — no code; verify AC #4 holds via the existing device grant. One-line note that the
device token's `scope` comes from `device.Scope` (client-requested, validated against the client's allowed
scopes), **not** the user's role permissions — divergence from the web token is expected (OAuth server / TASK-030/035 territory).

**Tests** (xUnit + FluentAssertions, GitHub HTTP mocked)
9. New `DraCode.KoboldLair.Tests/Auth/GitHubFederationTests.cs`:
   - *Service-level (primary):* `GitHubFederationService` + fake `IGitHubUserInfoClient`, real `JwtTokenProvider`,
     in-memory `IUserRepository`, widened `RefreshTokenStore`. First call creates `User{ Sub="github:{id}" }` +
     returns a token; second call same id **reuses** the user (AC #3); **id not in `AllowedGitHubIds` ⇒ 403,
     no token, no user row** (decision 3).
   - *Endpoint-level (callback):* `WebApplicationFactory<Program>` like `JwtMiddlewareTests.CreateFactory`
     (`Jwt.Enabled=true`, `GitHub.Enabled=true`, allowlist seeded with the test id, JsonFile backend,
     `RemoveAll<IHostedService>()`), `ConfigureTestServices` replacing `IGitHubUserInfoClient` +
     `IGitHubTokenExchanger` with fakes. Pre-seed a valid `state`, drive `GET /auth/github/callback?code=&state=`,
     assert a **`302` whose `Location` carries `#access_token=`**, then that token authenticates a protected endpoint.
   - *Refresh regression:* a `/auth/refresh` round-trip preserves the string `sub` (decision 2 didn't break config users).

### Risks
- **CSRF `state` (security-critical)** — callback MUST validate `state` against the store, single-use, short-TTL,
  or it is a login-CSRF / open-redirect vector. The `PostLoginRedirectUri` must be a fixed config value, never
  reflected from the request.
- **`RefreshTokenStore` widening touches live paths** — `/auth/login` + `/auth/refresh` are config-users-only
  today; the change is mechanical (`Guid → string`) but must keep the refresh round-trip green (covered by the regression test).
- **Token-minting reuse** — identical claim set as `HandleLogin`; `scope` is comma-joined (`ClaimsCurrentUser`
  splits on commas) — keep comma-join, or federated tokens validate but fail `PermissionEndpointFilter`.
- **`sub` stability** — key on GitHub numeric `id`, never `login` (mutable). `read:user` suffices.
- **`IUserRepository` is `null!` off-SQLite** — null-guard (mint without persistence); the tests use the JSON backend.
- **`HttpClient` hygiene** — `api.github.com` 403s without a `User-Agent`; pooled `AddHttpClient` + header.

### Critical files
- `DraCode.KoboldLair.Server/Auth/RefreshTokenStore.cs` — **widen `Guid UserId → string OwnerSub`** (decision 2)
- `DraCode.KoboldLair.Server/Auth/AuthEndpoints.cs` — minting shape to mirror; update the two callers for the widened store
- `DraCode.KoboldLair.Server/Program.cs` — DI wiring + gated `MapGitHubAuthEndpoints` block + fail-fast precedent (~932); `IUserRepository` null-guard (~260)
- `DraCode.KoboldLair/Data/Repositories/IUserRepository.cs` + `Models/Users/User.cs` — the `GetBySubAsync`/`UpsertAsync` seam to reuse
- `Birko.Communication.OAuth.Providers/GitHubOAuthProvider.cs` — `TokenEndpoint` const (authorize URL kept local; follow-up to upstream a helper)
- `DraCode.KoboldLair.Tests/Auth/JwtMiddlewareTests.cs` — `WebApplicationFactory` + `ConfigureTestServices` mocking pattern to copy
- **New:** `Auth/GitHubFederationConfiguration.cs`, `Auth/GitHubUserInfoClient.cs`, `Auth/GitHubTokenExchanger.cs`, `Auth/GitHubFederationService.cs`, `Auth/GitHubAuthEndpoints.cs`, `Auth/OAuthStateStore.cs`, `Tests/Auth/GitHubFederationTests.cs`
