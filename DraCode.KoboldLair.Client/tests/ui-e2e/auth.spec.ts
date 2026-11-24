import { test, expect } from './fixtures.js';
import type { Page, Route } from '@playwright/test';

// TASK-056: sign in with GitHub. The client host's /api/config is patched to say sign-in is required; the server's
// GitHub flow is stubbed as one redirect back to the page with the tokens in the fragment, as the real callback does;
// /auth/refresh and /auth/logout are stubbed and recorded.

const SERVER = 'http://localhost:57087';

function b64url(value: object): string {
  return Buffer.from(JSON.stringify(value)).toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** An unsigned JWT with the claims the server's GitHub sign-in puts in it. */
function jwt(claims: { sub?: string; name?: string; expIn?: number } = {}): string {
  const exp = Math.floor(Date.now() / 1000) + (claims.expIn ?? 3600);
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url({ sub: claims.sub ?? 'github:4242', name: claims.name ?? 'Ada Lovelace', exp })}.sig`;
}

interface Server { refreshes: string[]; logouts: string[]; refreshAnswer: () => { status: number; json?: object } }

async function serve(page: Page, options: { authRequired?: boolean; projects?: (route: Route) => unknown } = {}): Promise<Server> {
  const server: Server = { refreshes: [], logouts: [], refreshAnswer: () => ({ status: 200, json: { token: jwt(), refreshToken: 'r2' } }) };
  await page.route('**/api/config', async (route) => {
    const response = await route.fetch();
    const config = await response.json();
    await route.fulfill({ json: { ...config, authRequired: options.authRequired ?? true } });
  });
  await page.route(`${SERVER}/auth/github/login`, (route) => route.fulfill({
    status: 302,
    headers: { location: `http://localhost:57088/#access_token=${jwt()}&refresh_token=r1&expires_at=2026-11-22T12%3A00%3A00Z` },
  }));
  await page.route(`${SERVER}/auth/refresh`, (route) => {
    server.refreshes.push((route.request().postDataJSON() as { refreshToken: string }).refreshToken);
    const answer = server.refreshAnswer();
    return route.fulfill({ status: answer.status, json: answer.json ?? {} });
  });
  await page.route(`${SERVER}/auth/logout`, (route) => {
    server.logouts.push((route.request().postDataJSON() as { refreshToken: string }).refreshToken);
    return route.fulfill({ json: { message: 'Logged out' } });
  });
  await page.route(/\/api\/v1\/projects(\/|\?|$)/, options.projects ?? ((route) => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/v1/projects') return route.fulfill({ json: [] });
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: 'p-1', content: '# Spec', version: 1 } });
    if (path.endsWith('/features')) return route.fulfill({ json: [] });
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'github:4242', status: 'New', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  }));
  return server;
}

const signedIn = (page: Page, token: string, refreshToken = 'r1') => page.addInitScript(([t, r]) => {
  if (!sessionStorage.getItem('seeded')) {
    sessionStorage.setItem('seeded', '1');
    localStorage.setItem('koboldlair_auth', JSON.stringify({ token: t, refreshToken: r, userId: 'github:4242', userName: 'Ada Lovelace', isAuthenticated: true }));
  }
}, [token, refreshToken]);

// A test can end while serve()'s /api/config handler is still forwarding the request; closing the page then makes
// that handler throw and fails a test that passed (TASK-189). Drop the routes first and ignore what they are doing.
test.afterEach(({ page }) => page.unrouteAll({ behavior: 'ignoreErrors' }));

const store = (page: Page, key: string) => page.evaluate((k) => (window as any).koboldLair.auth.store.get(k), key);
const userMenu = (page: Page) => page.locator('kobold-lair-shell [data-header-slot="user-menu"]');
const outlet = (page: Page, tag: string) => page.locator(`#route-outlet ${tag}`);

test('signed out, a protected page goes to #/login, which offers only "Sign in with GitHub"', async ({ page }) => {
  await serve(page);
  await page.goto('/#/');
  await expect(page).toHaveURL(/#\/login$/);
  const link = outlet(page, 'kobold-login-view').getByRole('link', { name: 'Sign in with GitHub' });
  await expect(link).toHaveAttribute('href', `${SERVER}/auth/github/login`);
  await expect(outlet(page, 'kobold-login-view').locator('input, textarea')).toHaveCount(0); // no token to paste
  await expect(userMenu(page).locator('.user-name')).toHaveCount(0);
});

test('"Sign in with GitHub" comes back signed in: tokens stored, fragment gone, on the homepage with the user shown', async ({ page }) => {
  const server = await serve(page);
  await page.goto('/#/login');

  await outlet(page, 'kobold-login-view').getByRole('link', { name: 'Sign in with GitHub' }).click();

  await expect(outlet(page, 'kobold-home-view')).toHaveCount(1);
  expect(new URL(page.url()).hash).not.toContain('access_token');
  expect(await page.evaluate(() => history.length > 0 && !location.href.includes('access_token'))).toBe(true);
  expect(await store(page, 'refreshToken')).toBe('r1');
  expect(await store(page, 'userId')).toBe('github:4242');
  await expect(userMenu(page).locator('.user-name')).toHaveText('Ada Lovelace');
  await expect(userMenu(page).locator('.user-avatar')).toHaveText('AD');
  expect(server.refreshes).toEqual([]); // an hour left: nothing to refresh yet
});

test('the token is refreshed before it expires', async ({ page }) => {
  const server = await serve(page);
  const fresh = jwt({ expIn: 3600 });
  server.refreshAnswer = () => ({ status: 200, json: { token: fresh, refreshToken: 'r2' } });
  await signedIn(page, jwt({ expIn: 62 })); // refreshed a minute before exp: in ~2 s
  await page.goto('/#/');
  await expect(outlet(page, 'kobold-home-view')).toHaveCount(1);

  await expect.poll(() => server.refreshes, { timeout: 10_000 }).toEqual(['r1']);
  await expect.poll(() => store(page, 'token')).toBe(fresh);
  expect(await store(page, 'refreshToken')).toBe('r2');
});

test('a 401 the refresh cannot fix ends the session and returns to #/login', async ({ page }) => {
  const server = await serve(page, { projects: (route) => route.fulfill({ status: 401, body: '' }) });
  server.refreshAnswer = () => ({ status: 401 });
  await signedIn(page, jwt());
  await page.goto('/#/');

  await expect(page).toHaveURL(/#\/login$/);
  expect(server.refreshes).toEqual(['r1']);
  expect(await store(page, 'token')).toBeNull();
});

test('Sign out revokes the refresh token, clears the store, closes Dragon\'s socket, and Back shows the sign-in screen', async ({ page }) => {
  const server = await serve(page);
  let socketClosed = false;
  await page.routeWebSocket(/\/dragon(\?|$)/, (ws) => {
    ws.onClose(() => { socketClosed = true; });
    ws.onMessage((raw) => {
      const msg = JSON.parse(String(raw));
      if (msg.type === 'select_project') ws.send(JSON.stringify({ type: 'project_selected', sessionId: 's-1', projectId: msg.projectId, projectName: 'Shop', messageCount: 0 }));
    });
  });
  await signedIn(page, jwt());
  await page.goto('/#/projects/p-1/spec');
  await expect(outlet(page, 'kobold-dragon-dock').locator('b-chat textarea')).toBeEnabled();

  await userMenu(page).locator('.user-trigger').click();
  await userMenu(page).locator('b-dropdown-menu').getByText('Sign out').click();

  await expect(page).toHaveURL(/#\/login$/);
  await expect.poll(() => server.logouts).toEqual(['r1']);
  expect(await store(page, 'token')).toBeNull();
  await expect.poll(() => socketClosed).toBe(true);
  await expect(userMenu(page).locator('.user-name')).toHaveCount(0);

  await page.goBack();
  await expect(page).toHaveURL(/#\/login$/);
  await expect(outlet(page, 'kobold-project-workspace')).toHaveCount(0);
});

test('the user name from the token is shown as text', async ({ page }) => {
  await serve(page);
  await signedIn(page, jwt({ name: '<img src=x onerror="window.__pwned=1">' }));
  await page.addInitScript(() => {
    const raw = JSON.parse(localStorage.getItem('koboldlair_auth') ?? '{}');
    localStorage.setItem('koboldlair_auth', JSON.stringify({ ...raw, userName: '<img src=x onerror="window.__pwned=1">' }));
  });
  await page.goto('/#/');
  await expect(userMenu(page).locator('.user-name')).toHaveText('<img src=x onerror="window.__pwned=1">');
  await expect(userMenu(page).locator('img')).toHaveCount(0);
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
});

test('the legacy pages with their token-paste fields are not served', async ({ page }) => {
  for (const path of ['/archive/legacy-index.html', '/archive/old-vanilla-ui/server-selector.js', '/archive/old-vanilla-ui/']) {
    expect((await page.request.get(path)).status(), path).toBe(404);
  }
});

test('when the server needs no sign-in the app opens without a sign-in screen', async ({ page }) => {
  await serve(page, { authRequired: false });
  await page.goto('/#/');
  await expect(outlet(page, 'kobold-home-view')).toHaveCount(1);
  await expect(page).not.toHaveURL(/#\/login/);
});
