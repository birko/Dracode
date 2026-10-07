---
id: TASK-113
parent: STORY-039
feature: FEATURE-026
status: todo
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

- [ ] Routing uses Birko.Web.Core `Router`, routes come from the module manifests via `buildModuleRoutes`, protected routes use `createAuthGuard`
- [ ] The three sub-routes open their screens; an unknown route shows a not-found state, not the dashboard
- [ ] A `/login` route exists for TASK-056 to fill
- [ ] `router.ts` is deleted; tests cover a sub-route, an unknown route and the guard redirect

## Out of scope

- Mounting the shell in the served page (TASK-072) — this task makes the shell's routing correct once mounted

## Human test plan

N/A — route resolution and the guard are covered by tests.

## Implementation plan
