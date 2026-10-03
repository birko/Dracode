import { test, expect } from './fixtures.js';
import { runSmoke, defineManifest } from 'birko-web-testing/playwright';

// Unauthenticated smoke of KoboldLair's public routes (hash routing; routes from src/app-shell.ts).
// No expectSelector — these pages are app-specific; the assertion is "navigated + rendered with zero
// console errors / pageerrors / 4xx-5xx". `#/` and guarded routes resolve to the login view when
// unauthenticated, which is itself a valid render. Add expectSelector/mustSee once stable selectors
// are confirmed in the login/dashboard views.
runSmoke(
  test,
  expect,
  defineManifest('KoboldLair (public)', [
    { path: '/#/login', label: 'Login' },
    { path: '/#/', label: 'Root (→ dashboard or login)' },
    { path: '/#/settings', label: 'Settings (→ login if guarded)' },
  ]),
  { ignoreRequest: (r) => r.url.endsWith('/favicon.ico') },
);
