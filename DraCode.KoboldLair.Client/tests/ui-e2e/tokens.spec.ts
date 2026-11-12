import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-114: the Shell auth store is the only place the token lives. The /wyvern WebSocket client, the REST client and
// the SSE client all read it; signing out clears it for all three; a token an earlier client stored elsewhere is
// moved into the auth store once.

const SERVER = 'http://localhost:57087';

/** What each client sends: the WebSocket URL, the REST Authorization header, the SSE URL. */
async function whatClientsSend(page: Page): Promise<{ ws: string; rest: string | null; sse: string }> {
  const cors = { 'access-control-allow-origin': '*', 'access-control-allow-headers': '*', 'access-control-allow-methods': '*' };
  let rest: string | null = null;
  let sse = '';
  await page.route(`${SERVER}/api/v1/**`, async (route) => {
    const request = route.request();
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers: cors });
    if (request.url().includes('/events')) {
      sse = request.url();
      return route.fulfill({ status: 200, headers: { ...cors, 'content-type': 'text/event-stream' }, body: '' });
    }
    rest = (await request.allHeaders())['authorization'] ?? null;
    return route.fulfill({ status: 200, headers: cors, json: {} });
  });

  const sseSent = page.waitForRequest((r) => r.url().includes('/events'));
  await page.evaluate(async () => {
    const { clients } = (window as any).koboldLair;
    const wyvern = clients.createWyvernClient();
    wyvern.connect();
    await clients.createRestClient().get('/whoami');
    const events = clients.createSseClient('/api/v1/runs/r1/events');
    events.connect();
    setTimeout(() => { wyvern.disconnect(); events.disconnect(); }, 1000);
  });
  await expect.poll(() => page.evaluate(() => (window as any).__wsUrls.length)).toBeGreaterThan(0);
  const ws: string = await page.evaluate(() => (window as any).__wsUrls.at(-1));
  await sseSent;
  await page.unroute(`${SERVER}/api/v1/**`);
  return { ws, rest, sse };
}

async function ready(page: Page) {
  // No server listens in the e2e run, and Playwright reports no 'websocket' event for a refused connection, so record
  // the URL each WebSocket is opened with; the real socket still goes ahead (and is refused).
  await page.addInitScript(() => {
    const Native = window.WebSocket;
    (window as any).__wsUrls = [];
    (window as any).WebSocket = class extends Native {
      constructor(url: string | URL, protocols?: string | string[]) {
        (window as any).__wsUrls.push(String(url));
        super(url, protocols);
      }
    };
  });
  await page.goto('/#/dashboard');
  await expect.poll(() => page.evaluate(() => !!(window as any).koboldLair)).toBe(true);
}

test('a token set in the auth store reaches the WebSocket, REST and SSE clients', async ({ page }) => {
  await ready(page);
  await page.evaluate(() => (window as any).koboldLair.auth.setAuth({ token: 'tok-1' }));

  const sent = await whatClientsSend(page);

  expect(new URL(sent.ws).searchParams.get('token')).toBe('tok-1');
  expect(sent.rest).toBe('Bearer tok-1');
  expect(new URL(sent.sse).searchParams.get('token')).toBe('tok-1');
});

test('signing out clears the token for all three clients', async ({ page }) => {
  await ready(page);
  await page.evaluate(() => {
    const { auth } = (window as any).koboldLair;
    auth.setAuth({ token: 'tok-1' });
    auth.clearAuth();
  });

  const sent = await whatClientsSend(page);

  expect(new URL(sent.ws).searchParams.has('token')).toBe(false);
  expect(sent.rest).toBeNull();
  expect(new URL(sent.sse).searchParams.has('token')).toBe(false);
  expect(await page.evaluate(() => localStorage.getItem('koboldlair_auth'))).toBeNull();
});

test('a token in koboldlair-config is moved into the auth store once, and per-server tokens are dropped', async ({ page }) => {
  // Storage as an earlier client left it; written before the first load only.
  await page.addInitScript(() => {
    if (sessionStorage.getItem('seeded')) return;
    sessionStorage.setItem('seeded', '1');
    localStorage.setItem('koboldlair-config', JSON.stringify({ serverUrl: 'ws://localhost:57087', authToken: 'legacy-tok' }));
    localStorage.setItem('koboldlair-servers', JSON.stringify([{ id: 'default', name: 'Default', url: 'ws://localhost:57087', token: 'legacy-tok' }]));
  });
  await ready(page);

  const stored = () => page.evaluate(() => ({
    token: (window as any).koboldLair.auth.getToken(),
    config: JSON.parse(localStorage.getItem('koboldlair-config') ?? '{}'),
    servers: JSON.parse(localStorage.getItem('koboldlair-servers') ?? '[]'),
  }));

  const first = await stored();
  expect(first.token).toBe('legacy-tok');
  expect(first.config).toEqual({ serverUrl: 'ws://localhost:57087' });
  expect(first.servers).toEqual([{ id: 'default', name: 'Default', url: 'ws://localhost:57087' }]);

  // Signing out after the migration is not undone by the next load: nothing is left to migrate.
  await page.evaluate(() => (window as any).koboldLair.auth.clearAuth());
  await page.reload();
  await expect.poll(() => page.evaluate(() => !!(window as any).koboldLair)).toBe(true);
  expect((await stored()).token).toBeNull();
});
