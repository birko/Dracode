# KoboldLair UI · E2E (birko-web-testing)

Unauthenticated smoke for the KoboldLair client, using the shared
[`birko-web-testing`](../../../../../Web/Birko.Web.Testing/README.md) package. This is the **second
reference adoption** (after Symbio); it shows the `requireAuth: false` mode for an app whose API login
isn't wired into the test yet.

## Prerequisites
- Serve the SPA: build the frontend (`npm run build` in the client) and `dotnet run` the
  `DraCode.KoboldLair.Client` host → it serves `wwwroot` at **http://localhost:57088**
  (`Properties/launchSettings.json`). The agent backend (`DraCode.KoboldLair.Server`) binds
  **http://localhost:57087 / https://localhost:57085**; the Client's `ServerUrl` default still reads
  `ws://localhost:5000`, which is stale and being reworked (the smoke is read-only and doesn't hit it).
- One shared Chromium: `npm run install:browser` (or `npx playwright install chromium`). See the
  package [`ENV.md`](../../../../../Web/Birko.Web.Testing/ENV.md) for the one-Chromium / Puppeteer-reuse policy.

## Run
```bash
cd tests/ui-e2e
npm install        # set PUPPETEER_SKIP_DOWNLOAD=1 to reuse Playwright's Chromium
npm test
npm run report
```
Override the origin with `KOBOLDLAIR_BASE_URL`.

## Scope
`shell.spec.ts` (TASK-072): the served page mounts the TypeScript shell (not the archived legacy page), its header
shows English labels, hash navigation swaps views, and every view renders and settles with no console errors (it
catches render loops the smoke settles too early to see).

`home.spec.ts` (TASK-145): the header (product name, then slots for the project picker, the bell and the user menu)
replaces the ribbon, and `#/` is the projects homepage. With `GET /api/v1/projects` stubbed by `page.route`: a card per
project with its status, execution state and progress; the request asks for `scope=all` with the token; an empty list
says to start a project with Dragon; signed out it asks to sign in and requests nothing; a failed load says so; at
375px wide the cards stack in one column with no horizontal scroll. The deleted ribbon views show the not-found view.

`workspace.spec.ts` (TASK-150): a project card opens `#/projects/{id}/spec`, the workspace frame with its Spec tab. With
the project, specification and features stubbed (a regex route: the glob `**/projects**` misses `/projects/{id}/…`):
the Markdown renders (headings, lists, code) beside the features and their status; the tab has no edit controls and
points to Dragon; hostile markup leaves no script, handler or `javascript:` URL; an unknown or not-owned project (404)
and an unknown tab show the not-found view; `#/projects/{id}` opens the Spec tab; at 375px a long code line and a wide
table scroll inside themselves.

`board.spec.ts` (TASK-153): the Board tab, the workspace's first tab, with the tasks stubbed: four status columns with
their counts, cards sorted by priority with title, agent and priority, a failed card with the start of its error and
Retry; Retry posts and moves the card to To do; a refused Retry says why on the card; the board refreshes on its own;
an empty project says where tasks come from; in narrow space (the board's own width, as Dragon expanded will give it)
and at 375px the columns become a compact list with no horizontal scroll.

`dragon-dock.spec.ts` (TASK-151): Dragon docked on the right of the workspace (360px; under the tabs below the
breakpoint, no horizontal scroll), against a fake `/dragon` server (`page.routeWebSocket`): the project's conversation is
replayed after `project_selected`; sending shows the user's message, typing, what Dragon is doing and the reply as it
streams, then the final reply; opening another project sends `select_project` on the same connection and drops the old
project's late reply; a dropped connection reconnects with its `sessionId` and replays (`session_resumed`), or opens the
project again when the session is gone; `project_not_found` closes the chat; hostile Markdown in a reply is sanitised;
`#/dragon` and `#/dragon/specifications` show the not-found view. `workspace.spec.ts` holds the socket open silently.

`routes.spec.ts` (TASK-113): module sub-routes open their screens (`#/settings/providers` and `#/settings/general` on
their tabs), an unknown route shows the not-found view, `#/login` exists, and the
auth guard sends a signed-out user to `#/login` only when `/api/config` says `authRequired` (stubbed with
`page.route`; the host default is `false`).

`settings.spec.ts` (TASK-112): both settings tabs render and switch; saving shows a success toast with its text, and
a required field left empty shows an error toast with its text.

`tokens.spec.ts` (TASK-114): a token set in the auth store reaches the `/wyvern` WebSocket (`?token=`), the REST
client (`Authorization: Bearer`) and the SSE client (`?token=`); signing out clears it for all three; a token an
earlier client left in `koboldlair-config` is moved into the auth store once. It drives the clients through the
`window.koboldLair` debug handle, stubs the server's HTTP with `page.route`, and records WebSocket URLs from the
constructor (no server listens, and Playwright reports no event for a refused WebSocket).

`smoke.spec.ts` sweeps the public hash routes (`#/login`, `#/`, `#/settings` — from `src/app-shell.ts`)
and asserts each loads with **zero console errors / pageerrors / 4xx-5xx**. No `expectSelector` yet —
add stable selectors once confirmed in the login/home views.

## Test data isolation
Run the **agent backend (`DraCode.KoboldLair.Server`) in the `Testing` environment** so it uses an
isolated SQLite DB + agent working dirs (`./projects-test/koboldlair-test.db`, see
`DraCode.KoboldLair.Server/appsettings.Testing.json`), leaving the real `./projects` and
`koboldlair.db` untouched:

```sh
# Backend on its own test DB:
dotnet run --project DraCode.KoboldLair.Server --launch-profile "DraCode.KoboldLair.Server (Testing)"
# (equivalent: ASPNETCORE_ENVIRONMENT=Testing dotnet run --project DraCode.KoboldLair.Server)

# SPA host (serves wwwroot on :57088):
dotnet run --project DraCode.KoboldLair.Client
```

This smoke is read-only and only needs the SPA host, but the authenticated/CRUD routes below hit the
backend and mutate data — never point them at a shared or hosted instance. Keep the committed `baseURL`
on `localhost:57088` (override via `KOBOLDLAIR_BASE_URL`), use a dedicated test account, and have any
CRUD spec create→assert→delete + clean up. Delete `projects-test/` to reset. See the package README's
[*Test data isolation*](../../../../../Web/Birko.Web.Testing/README.md) section.

`npm run seed` (`seed.mjs`) is an idempotent seed **scaffold**: today it just verifies the backend is
reachable (the read-only smoke needs no fixtures) and carries a ready-to-fill fixture template for when
the authenticated/CRUD routes land. Override the backend origin via `KOBOLDLAIR_SERVER_URL`.

## Adding authenticated routes
KoboldLair uses `createAuthStore({ storageKey: 'koboldlair_auth', claimMappings: { userName: 'name',
email: 'email', tenantId: 'tenant_id', permissions: 'permission' } })` (`src/auth-store.ts`). Once the
REST login endpoint + dev credentials are known:

1. Flip the config to authenticated:
   ```ts
   // playwright.config.ts — drop requireAuth:false, add storageState
   birkoPlaywrightPreset({ baseURL: 'http://localhost:57088', storageState: '.auth/koboldlair.json' })
   ```
2. Add `auth.setup.ts` (mirrors Symbio's), passing KoboldLair's claim mappings:
   ```ts
   import { test as setup } from '@playwright/test';
   import { loginViaApi } from 'birko-web-testing/core';

   setup('authenticate', async ({ page }) => {
     const auth = await loginViaApi(
       {
         apiBaseUrl: '<<API origin>>',
         storageKey: 'koboldlair_auth',
         loginPath: '<<login path, e.g. api/auth/login>>',
         claimMappings: { userName: 'name', email: 'email', tenantId: 'tenant_id', permissions: 'permission' },
       },
       { login: '<<user>>', password: '<<pass>>' },
     );
     await page.goto('/');
     await page.evaluate(([k, v]) => localStorage.setItem(k, v), [auth.storageKey, auth.storageValue] as [string, string]);
     await page.context().storageState({ path: '.auth/koboldlair.json' });
   });
   ```
3. Add authenticated routes (the projects homepage `#/`, `#/profile`, `#/settings`) to `smoke.spec.ts`.
