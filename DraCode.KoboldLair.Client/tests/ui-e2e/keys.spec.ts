import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-162: Settings → API keys, for any signed-in user, on /api/v1/keys (stubbed). A new key's secret is shown once,
// with Copy; revoking asks first and marks the key revoked.

const settings = (page: Page) => page.locator('#route-outlet kobold-settings-view');
const keysPage = (page: Page) => settings(page).locator('kobold-api-keys-page');
const rows = (page: Page) => keysPage(page).locator('b-data-table tbody tr');

interface Key { clientId: string; name: string; ownerId: string; scopes: string[]; createdAt: string; disabled: boolean }

async function serve(page: Page, keysEnabled = true, keys: Key[] = [
  { clientId: 'c-1', name: 'old-runner', ownerId: 'u-1', scopes: ['view_own'], createdAt: '2026-11-20T10:00:00Z', disabled: false },
]) {
  const requests: { method: string; path: string; body: any }[] = [];
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/settings\/capabilities/, (route) => route.fulfill({ json: {
    canManageProviders: false, providersEnabled: false, providersHint: null, keysEnabled,
    keysHint: keysEnabled ? null : 'API keys are off on this server: set Authentication:OAuth:Enabled (with JWT on) and restart it.',
  } }));
  await page.route(/\/api\/v1\/keys(\/|\?|$)/, async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname.replace('/api/v1/', '');
    const body = req.postData() ? JSON.parse(req.postData()!) : null;
    requests.push({ method: req.method(), path, body });
    const [, id, action] = path.split('/');
    if (req.method() === 'GET') return route.fulfill({ json: keys });
    if (req.method() === 'POST' && !id) {
      const key = { clientId: 'c-new', name: body.name, ownerId: 'u-1', scopes: ['view_own', 'manage_projects', 'execute_agents'],
        createdAt: '2026-11-22T12:00:00Z', disabled: false };
      keys.unshift(key);
      return route.fulfill({ status: 201, json: { ...key, clientSecret: 'S3CRET-only-once' } });
    }
    if (req.method() === 'POST' && action === 'revoke') {
      const key = keys.find((k) => k.clientId === id)!;
      key.disabled = true;
      return route.fulfill({ json: key });
    }
    return route.fulfill({ status: 405, body: '' });
  });
  await page.goto('/#/settings/keys');
  return requests;
}

async function openRevoke(page: Page, clientId: string) {
  await keysPage(page).locator(`b-data-table .row-action-trigger[data-id="${clientId}"]`).click();
  await page.getByRole('menuitem', { name: /revoke/i }).click();
}

test('the API keys tab lists the user\'s keys and their status', async ({ page }) => {
  await serve(page);
  await expect(settings(page).locator('b-tabs').getByRole('tab', { name: 'API keys' })).toHaveAttribute('aria-selected', 'true');
  await expect(rows(page)).toHaveCount(1);
  await expect(rows(page).first()).toContainText('old-runner');
  await expect(rows(page).first()).toContainText('Active');
});

test('a new key\'s secret is shown once with a Copy button, then never again', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  const requests = await serve(page);
  await expect(rows(page)).toHaveCount(1);

  await keysPage(page).locator('#btn-create').click();
  await keysPage(page).locator('#modal b-form').evaluate((f: any) => f.setValues({ name: 'ci-runner' }));
  await keysPage(page).locator('#btn-save').click();

  const dialog = keysPage(page).locator('#secret-modal');
  await expect(dialog.locator('.client-secret')).toHaveText('S3CRET-only-once');
  await expect(dialog.locator('.client-id')).toHaveText('c-new');
  expect(requests.find((r) => r.method === 'POST')?.body).toEqual({ name: 'ci-runner' });

  await dialog.locator('b-button[data-copy="S3CRET-only-once"]').click();
  await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toBe('S3CRET-only-once');

  await page.keyboard.press('Escape');
  await expect(dialog.locator('.client-secret')).toHaveCount(0);
  await expect(rows(page)).toHaveCount(2);
  await expect(keysPage(page)).not.toContainText('S3CRET-only-once');
});

test('revoking asks first, then marks the key revoked', async ({ page }) => {
  const requests = await serve(page);
  await expect(rows(page)).toHaveCount(1);

  await openRevoke(page, 'c-1');
  await keysPage(page).locator('#revoke-confirm b-button.btn-confirm').click();

  await expect(rows(page).first()).toContainText('Revoked');
  expect(requests.find((r) => r.path === 'keys/c-1/revoke')).toBeTruthy();
});

test('cancelling the revoke leaves the key alone', async ({ page }) => {
  const requests = await serve(page);
  await expect(rows(page)).toHaveCount(1);

  await openRevoke(page, 'c-1');
  await keysPage(page).locator('#revoke-confirm b-button.btn-cancel').click();

  await expect(rows(page).first()).toContainText('Active');
  expect(requests.some((r) => r.path.endsWith('/revoke'))).toBe(false);
});

test('with OAuth off, the tab says how to turn API keys on', async ({ page }) => {
  const requests = await serve(page, false);
  await expect(settings(page).locator('.keys-off')).toContainText('Authentication:OAuth:Enabled');
  expect(requests).toEqual([]);
});
