---
id: TASK-113
parent: STORY-039
feature: FEATURE-026
status: done
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Use the Birko.Web router with module routes and an auth guard; drop the hand-rolled router

## Context

`DraCode.KoboldLair.Client/src/router.ts` (126 lines) matches hash paths exactly — no parameters, no guard. Three module sub-routes listed in
`DraCode.KoboldLair.Client/src/module-store.ts` (`#/dragon/specifications`, `#/metrics/costs`, `#/settings/providers`) match nothing and fall back to the
dashboard, and there is no `/login` route. Birko.Web.Core's `Router` plus Shell's `buildModuleRoutes` and `createAuthGuard`
cover all of it.

## Acceptance criteria

- [x] Routing uses Birko.Web.Core `Router`, routes come from the module manifests via `buildModuleRoutes`, protected routes use `createAuthGuard`
- [x] The three sub-routes open their screens; an unknown route shows a not-found state, not the dashboard
- [x] A `/login` route exists for TASK-056 to fill
- [x] `router.ts` is deleted; tests cover a sub-route, an unknown route and the guard redirect

## Out of scope

- Mounting the shell in the served page (TASK-072) — this task makes the shell's routing correct once mounted

## Human test plan

N/A — route resolution and the guard are covered by tests.

## Implementation plan

1. `src/routes.ts`: Birko.Web.Core `Router` over `#route-outlet`; routes from `buildModuleRoutes(modules)`, each
   option mapped to its view; a few non-ribbon screens; `/login`; `*` → not-found.
2. Guard: `createAuthGuard(authStore, '/login')`, applied only when the host says the server requires sign-in.
3. Boot: load modules into the module store, then start the router; the shell renders its ribbon from the store.
4. Delete `router.ts`; Playwright `routes.spec.ts`.

## Implementation notes

- **Routes** (`src/routes.ts`): every module option route becomes a Router route (the manifests write `#/x`; the `#`
  is stripped). `OPTION_VIEWS` maps `moduleId/optionId` to a view: `settings/providers|general` → settings view with
  `tab="providers|general"` (the view reads it in `onMount`); `metrics/costs|performance` → the metrics view, which
  shows both; `dragon/specifications` → the Dragon view (its spec panel is on that screen); `projects/create` → the
  Dragon view (projects are created by talking to Dragon). An option with no mapping opens not-found. Outside the
  ribbon: `/`, `/settings`, `/metrics` (the old router served it), `/project-config`, `/impact`. `*` →
  `kobold-not-found-view`; `/login` → `kobold-login-view` (a placeholder for TASK-056). Document titles kept.
- **Guard — owner's decision (2026-11-12):** the server runs with JWT off by default and there is no sign-in yet, so
  an unconditional guard would lock every screen behind an empty `/login`. The client host's `/api/config` now
  returns `authRequired` from `KoboldLair:AuthRequired` (default `false`); `protectedGuard()` applies
  `createAuthGuard` only when it is `true`. `config.ts` takes `authRequired` from the host only (a stored user
  override cannot switch it off).
- **Boot:** `app-shell-init` loads the modules into `moduleStore` and starts the router; the shell no longer loads
  them itself, it renders the ribbon from the store and follows `modules` changes.
- `router.ts` deleted; `index.ts` exports `startRouter` / `getRouter` / `buildRoutes`.

## Close notes

- `tests/ui-e2e/routes.spec.ts` (9): settings sub-routes open their tab, `#/metrics/costs`, `#/dragon/specifications`,
  the ribbon marks the sub-route's module, unknown route → not-found (not the dashboard), `#/login`, guard: open with
  auth off (and the host reports `authRequired: false`), signed-out → `#/login` when required (stubbed
  `/api/config`), signed-in passes. Against the old `router.ts` 6 of 9 fail (sub-routes, not-found, `/login`,
  redirect); the 3 that pass held before too.
- E2E 23/23 against the client host; `tsc` clean in `src/`.
- Not done: deriving the smoke's route list from the manifests (CLAUDE.md's self-maintaining smoke) — the smoke
  imports through the Birko aliases, which Playwright cannot resolve; `shell.spec.ts` lists the views by hand.
- Human test plan: N/A per the task.

