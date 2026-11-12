---
id: TASK-072
parent: STORY-023
feature: FEATURE-026
status: done
priority: P1
assignee: ai
created: 2026-06-22
depends-on: []
blocks: [TASK-056]
pr: null
github-issue: null
jira-key: null
---

# Mount the TypeScript shell in the served page

## Context

> **2026-10-07 review:** `docs/Birko.Web.Investigation-Report.md` is mostly outdated — the esbuild aliases and the render error handling it blamed are fixed; the unmounted shell is the remaining cause of the blank page (TASK-117 archives the doc). Point the UI e2e smoke at the TS shell when it is served. Correct routing is TASK-113; styling from framework tokens is part of TASK-115.

The served `DraCode.KoboldLair.Client/wwwroot/index.html` is a self-contained **legacy
vanilla-JS app** (its own router + mock data); it does not load the built TypeScript shell
(`wwwroot/dist/app.js`) and does not mount `#app-shell` / `#route-outlet`. As a result the
TypeScript app in `src/` — where all current feature work lives (dragon view, settings, and the
TASK-056 OAuth login + API-keys UI) — **is not served by anything**. `app-shell.ts:11-13` notes
this explicitly. Discovered during the TASK-056 plan (2026-06-22); likely related to the active
`Birko.Web.Shell` investigation (commit `54412d9`).

Until the shell is mounted, TASK-056 (and any other `src/` feature) ships dark — built but never
rendered. This task makes the served page boot the TS shell so those features become reachable.

## Acceptance criteria

- [x] The served page loads the built shell bundle (`dist/app.js`) and mounts the shell root
      (`#app-shell` + `#route-outlet`, per `app-shell-init.ts` / `app-shell.ts`)
- [x] The TS router drives navigation (hash routes resolve to the `src/views/*` views), replacing
      the legacy vanilla-JS router/mock data on the served page
- [x] No regression to the build (`npm run build` produces the bundle the page references) and the
      app reaches an authenticated/landing state without console errors
- [x] Decide the fate of the legacy vanilla-JS `index.html` content (archived or removed), consistent
      with the `wwwroot/archive/old-vanilla-ui/` precedent

## Out of scope

- The OAuth login button + API-keys UI itself (TASK-056)
- Any server-side change

## Human test plan

- [x] Load the served app in a browser → the TypeScript shell renders (not the legacy mock UI); hash
      navigation works; no console errors

## Implementation plan

1. `wwwroot/index.html` becomes a thin page: the Birko.Web stylesheets, `<kobold-lair-shell id="app-shell">` with
   `#route-outlet` inside, `dist/app.js`. The legacy pages move to `wwwroot/archive/`.
2. `build.js` copies the framework tokens/reset/themes to `wwwroot/css/birko/` (gitignored).
3. Register the Birko components the shell renders; make the shell render English text.
4. `tests/ui-e2e/shell.spec.ts`; run it and the smoke against the client host.

## Implementation notes

- **Page:** `wwwroot/index.html` is now 20 lines: tokens, dark/neon/finstat themes and reset from
  `css/birko/`, the shell element with `#route-outlet` as its light-DOM child (the shell's content slot), and the
  bundle. The existing `router.ts` drives it unchanged (TASK-113 replaces it).
- **Legacy pages:** `index.html` → `wwwroot/archive/legacy-index.html`, and the other self-contained legacy page
  `index-simple.html` → `archive/legacy-index-simple.html` (the `archive/old-vanilla-ui/` precedent). `styles.css`
  stays for TASK-115.
- **Fixes needed to make the mounted shell work:**
  - `birko-web-components` was never imported, so `<b-ribbon>` / `<b-dropdown-menu>` were unknown elements and the
    shell threw (`ribbon.setContextualSets is not a function`). `app-shell-init.ts` imports it first, as Symbio does.
  - Every view recursed on its first render (`onUpdated()` → `onMount()` → `update()` → …). Fixed in its own commit
    (`52e49a2`, `KoboldComponent`).
  - The ribbon showed i18n keys (`modules.dashboard`): `simpleResolver` preferred the key over the label. It now
    prefers the manifest's English `label`; the shell's `t()` has English for the ten keys BAppShell renders
    (`status.online`, `auth.signOut`, …).
- **Mock views:** every view shows sample data; the legacy page at least opened `/wyvern`. Owner's call
  (2026-11-12): proceed, and TASK-142 wires the views to the server.
- **Docs:** client README's Development section rewritten (it described a vanilla-JS layout with no build);
  `tests/ui-e2e/README.md` scope.

## Close notes

- `tests/ui-e2e/shell.spec.ts` (11): the shell (not the legacy page) is mounted with a registered `b-ribbon`, ribbon
  and status bar show English, hash navigation swaps views, and each of the 9 views renders content and settles with
  no console errors. Against the views before `52e49a2`, 9 of 11 failed (stack overflow / hang); with the old label
  resolver the mount test fails. The generic smoke (3 routes) passed even with the render loop — it settles too early.
- `npm run build` produces the bundle and CSS the page references. `tsc --noEmit`: no errors in `src/` (the 27 it
  reports are in Birko.Web framework source — lib target — and are the same before and after).
- E2E 14/14 against the client host alone (`dotnet run --project DraCode.KoboldLair.Client`, port 57088); the
  Server was not started (local-settings gotcha), so nothing connected to a backend.
- **Human test plan run:** screenshots of dashboard, Dragon and Settings at 1400×900 — the shell renders (ribbon with
  module tabs, theme switch, user menu, status bar), hash navigation works, no console errors. Seen: the Settings view
  shows no tabs (TASK-112's known `b-tabs` misuse).
