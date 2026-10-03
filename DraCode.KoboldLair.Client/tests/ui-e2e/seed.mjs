// Forward-looking, idempotent seed scaffold for the KoboldLair E2E suite.
//
// The current smoke is UNAUTHENTICATED and READ-ONLY (sweeps #/login, #/, #/settings) — it hits only
// the SPA host and needs NO seed data. This script therefore just verifies the backend is reachable,
// and carries a ready-to-fill fixture template for when the authenticated / CRUD routes are wired
// (see README → "Adding authenticated routes"). Mirrors the shape of Symbio's tests/ui-e2e/seed.mjs.
//
// Run against the backend (Testing env — isolated ./projects-test/koboldlair-test.db) after it boots:
//   node seed.mjs
// Override the backend origin via KOBOLDLAIR_SERVER_URL. The default (http://localhost:57087) is the
// Server's actual HTTP bind (DraCode.KoboldLair.Server/Properties/launchSettings.json; https is :57085).
// NOTE: the Client's ServerUrl default still reads ws://localhost:5000 — that is stale (the Server only
// binds :5000 inside the in-memory test host) and is part of the in-flight backend rewiring.

const SERVER = (process.env.KOBOLDLAIR_SERVER_URL ?? 'http://localhost:57087').replace(/\/+$/, '');

async function call(method, path, body, token) {
  const res = await fetch(`${SERVER}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json; try { json = text ? JSON.parse(text) : null; } catch { json = text; }
  return { ok: res.ok, status: res.status, json, raw: text };
}

async function main() {
  console.log(`Checking ${SERVER} …`);

  // Reachability: GET / is anonymous and returns { status: "running", endpoints: [...] }.
  const health = await call('GET', '/');
  if (!health.ok) {
    throw new Error(
      `backend not reachable (HTTP ${health.status}) at ${SERVER} — start the ` +
      `"DraCode.KoboldLair.Server (Testing)" profile, or set KOBOLDLAIR_SERVER_URL to its http origin`,
    );
  }
  console.log(`• backend reachable: ${typeof health.json === 'object' ? JSON.stringify(health.json) : health.raw.slice(0, 80)}`);

  // The read-only smoke needs no fixtures.
  console.log('• read-only smoke needs no fixtures — nothing to seed');

  // ── Fixture template (enable once auth + CRUD specs land) ──────────────────────────────────────
  // KoboldLair's CRUD surface is the authed /api/v1 group (e.g. POST /api/v1/providers — TASK-073,
  // DB mode only). Once login is wired into the suite (README → "Adding authenticated routes"), log
  // in and seed parent fixtures get-first-then-create, mirroring Symbio's seed.mjs. Sketch:
  //
  //   import { loginViaApi } from 'birko-web-testing/core';
  //   const { token } = await loginViaApi({ apiBaseUrl: SERVER, storageKey: 'koboldlair_auth' }, { login, password });
  //   const have = (await call('GET', '/api/v1/providers', undefined, token)).json ?? [];
  //   if (!have.some((p) => p.name === 'e2e-seed')) {
  //     await call('POST', '/api/v1/providers', { name: 'e2e-seed', /* … */ }, token);
  //   }

  console.log('✓ seed complete.');
}

main().catch((e) => { console.error('SEED FAILED:', e.message); process.exit(1); });
