---
id: TASK-056
parent: STORY-023
feature: FEATURE-026
status: blocked
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-033, TASK-035, TASK-072]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Web OAuth login button + service-account key management UI

## Context

Replace the existing token-paste field (`auth-store.ts`) with a GitHub OAuth login button integrating STORY-017's flow (TASK-033), and add a service-account key management UI under Settings → API Keys (mirrors CLI `koboldlair keys`, TASK-035).

## Acceptance criteria

- [ ] OAuth login button replaces the token-paste field; completes the GitHub flow and stores the session via `auth-store.ts`
- [ ] Logout clears the session; expired token routes back to login
- [ ] Settings → API Keys: create / list / revoke service-account keys (same operations as CLI)
- [ ] Newly created key secret shown once, with copy affordance
- [ ] Tests where supported; manual flow covered below

## Out of scope

- Server-side OAuth/keys (STORY-017 tasks)
- Daemon status / switcher (TASK-055)

## Human test plan

- [ ] Click "Sign in with GitHub" → complete consent → land authenticated; open Settings → API Keys → create a key, see it once, revoke it
- [ ] **Also verify TASK-034's deferred end-to-end scoping** (it's in `review` waiting on this UI): log in as user A, create a project; log in as user B → B's project list omits A's project, B's own creates carry B's `sub`; an admin (`ViewAll`) sees both; under loopback-dev, creates are owned by `Guid.Empty` and the list shows all. When this passes, close TASK-034 → `done`.

## Implementation plan

> ⚠ **Acceptance criteria question — AC#3 "list service-account keys" has no server support.**
> The merged server exposes only `POST /register` + `POST /register/{clientId}/disable`; there is **no
> enumeration endpoint** (`SqlOAuthClientStore.ReadAsync(predicate)` throws `NotSupportedException`), and
> server changes are out of scope here. Options (none edit the criteria):
> (a) implement create/revoke now + render "list" as a **client-side session ledger** (localStorage,
>     `clientId`+name only, never secrets), labelled not-authoritative;
> (b) descope "list" → a follow-up server task for `GET /register` enumeration;
> (c) block AC#3 pending that server task.
> **Owner decision required.**

> ⚠ **Prerequisite risk — the served page does not mount the TS shell.** `wwwroot/index.html` is a
> self-contained **legacy vanilla-JS app**; it does not load `dist/app.js` or mount `#app-shell` /
> `#route-outlet`. The TypeScript shell in `src/` (where this feature lives) **isn't currently served by
> anything** (see `app-shell.ts:11-13`). Until the shell is mounted, this feature **ships dark.** This
> likely belongs to the active `Birko.Web.Shell` work (commit `54412d9`), not this task. **Owner decision:
> is shell-mounting in scope for TASK-056, or a separate prerequisite?**

### Findings that shape the work
- **No literal "token-paste field" in `auth-store.ts`** — `auth-store.ts` is a thin Birko factory wrapper;
  the real token inputs are the Auth-Token fields in `components/server-selector.ts` (`#serverToken` +
  manage-modal), which write `configService.setAuthToken()`. "Replace the token-paste field" = remove those.
- **Two disconnected token stores:** Birko `authStore` (localStorage `koboldlair_auth`, used for identity
  display) and `configService.authToken` (localStorage `koboldlair-config`, what the WS client actually sends
  via `?token=`). The GitHub JWT must land in **both** — bridge at the bootstrap + logout/refresh write points.
- **No JS/TS test runner** (`package.json` has only build/watch/type-check). "Tests where supported" =
  pure, dependency-free helpers (`parseAuthFragment`, `exp`-expiry check, snake_case secret extraction) +
  `npm run type-check` as the compile gate. Adding a runner is out of scope.
- **WS client doesn't 401-redirect** (it's a socket); expiry routing must be client-driven (JWT `exp` check
  on load + timer). The Birko HTTP `ApiClient` (`birko-web-core/http`) has an `onUnauthorized` hook for the
  REST `/register` + `/auth/*` calls.

### Server contract to integrate (already merged; do NOT change)
- `GET /auth/github/login` (302→GitHub) and `/auth/github/callback` → **302 to
  `{PostLoginRedirectUri}#access_token=…&refresh_token=…&expires_at=…`** (tokens in the URL **fragment**).
- `POST /register` (camelCase body `{ name, allowedGrantTypes:["client_credentials"], allowedScopes:[…] }`,
  needs `manage_users`; returns snake_case `{ client_id, client_secret, … }` once) · `POST /register/{id}/disable`.
- `POST /auth/refresh` `{refreshToken}` · `POST /auth/logout` `{refreshToken}`.

### Step-by-step (assuming option (a) for list, and shell-mounting resolved)
1. **Hash bootstrap (AC#1, security-core):** new `src/services/oauth-callback.ts` — pure
   `parseAuthFragment(hash)` (+ `exp`-expiry helper). In `app-shell-init.ts`, **before router init**: read
   `location.hash`, on token shape `setAuth(...)` (Birko) **and** `configService.setAuthToken(jwt)`, then
   **strip the fragment** via `history.replaceState` (must precede router/hashchange). Guard the `atob` JWT
   decode against malformed input (else white-screen).
2. **Login button (AC#1/#2):** new `src/views/login-view.ts` ("Sign in with GitHub" → full nav to
   `<httpOrigin>/auth/github/login`; ws→http origin helper in `config.ts`). Add `/login` route to `router.ts`
   (currently missing). Auth guard → `#/login` when unauthenticated/expired (reuse Birko `createAuthGuard`).
3. **Neutralize token-paste (AC#1):** remove the Auth-Token inputs + `setAuthToken` calls from
   `server-selector.ts` (server selection keeps URL only; token comes from the OAuth session).
4. **Logout + expiry (AC#2):** extend `app-shell.ts:onSignOut()` to also `POST /auth/logout` (best-effort) +
   clear `configService` token + disconnect WS. On load/timer/WS-close: if `exp` passed, try `/auth/refresh`
   → re-`setAuth`; on failure `clearAuth()` + `#/login`.
5. **Settings → API Keys (AC#3/#4):** new `src/services/keys-client.ts` (HTTP wrapper, Bearer from authStore):
   `createKey(name,scopes)` → `POST /register`; `revokeKey(clientId)` → disable. Add an "API Keys" `b-tab` to
   `settings-view.ts`: create-modal (name + scope checkboxes from **permission constants** per TASK-035 D15),
   list table from the **local ledger** (option a), per-row Revoke. On create, show `client_secret` **once** in
   a modal with Copy (`navigator.clipboard`) + "won't see again" warning; persist only `clientId`+name.
6. **Tests (AC#5):** pure-function assertions (`parseAuthFragment`, expiry, snake_case secret) runnable via
   `node` (no new dep) + `npm run type-check`; manual flow per the Human test plan (incl. TASK-034 e2e ownership).

### Risks
- **Shell not mounted** (Phase-0 ⚠) — highest; feature invisible until resolved.
- **Fragment-strip ordering** — router/`hashchange` must not see `#access_token=…`; bootstrap first + `replaceState`.
- **Token in localStorage** — XSS-exfiltratable; matches existing codebase pattern, but flag (refresh token
  safer in-memory/`sessionStorage`). Strip fragment immediately regardless.
- **Revocation latency** — disabling doesn't kill already-issued JWTs (TASK-035 note); UI must not imply instant.
- **CORS** — `/register` + `/auth/refresh` are XHR from SPA origin to server origin; if they differ, server CORS
  must allow it (server config, out of scope — flag for manual test).
- **Local ledger** won't show keys created via CLI or another browser (consequence of the no-list gap).

### Critical files
- `DraCode.KoboldLair.Client/src/app-shell-init.ts` — hash bootstrap + login guard + expiry routing (before router)
- `DraCode.KoboldLair.Client/src/components/server-selector.ts` — remove token-paste inputs / stop writing authToken
- `DraCode.KoboldLair.Client/src/views/settings-view.ts` — "API Keys" tab (create/list/revoke + secret-once modal)
- `DraCode.KoboldLair.Client/src/router.ts` — add `/login` route + import
- `DraCode.KoboldLair.Client/src/services/config.ts` — bridge OAuth JWT → `setAuthToken`; ws→http origin helper
- **New:** `src/services/oauth-callback.ts` (pure fragment/exp parsing), `src/views/login-view.ts`, `src/services/keys-client.ts`
- **Possibly:** `wwwroot/index.html` — mount `dist/app.js` (Phase-0 prerequisite, pending owner decision)
