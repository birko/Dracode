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
`smoke.spec.ts` sweeps the public hash routes (`#/login`, `#/`, `#/settings` — from `src/app-shell.ts`)
and asserts each loads with **zero console errors / pageerrors / 4xx-5xx**. No `expectSelector` yet —
add stable selectors once confirmed in the login/dashboard views.

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
3. Add authenticated routes (dashboard `#/`, `#/profile`, `#/settings`) to `smoke.spec.ts`.
