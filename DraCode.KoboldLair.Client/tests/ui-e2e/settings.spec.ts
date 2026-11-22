import { test, expect } from './fixtures.js';
import type { Page, Request } from '@playwright/test';

// TASK-161: Settings at #/settings, on /api/v1. Providers is Birko Shell's list page over /api/v1/providers, for an
// admin only; with provider management off on the server the tab says how to turn it on. The REST answers are stubbed.

const settings = (page: Page) => page.locator('#route-outlet kobold-settings-view');
const providers = (page: Page) => settings(page).locator('kobold-providers-page');
const rows = (page: Page) => providers(page).locator('b-data-table tbody tr');

interface Provider {
  name: string; displayName: string | null; type: string; enabled: boolean; baseUrl: string | null;
  requiresApiKey: boolean; defaultModel: string | null; compatibleAgents: string[]; hasKey: boolean;
}

const CLAUDE: Provider = {
  name: 'claude', displayName: 'Claude', type: 'claude', enabled: true, baseUrl: null, requiresApiKey: true,
  defaultModel: 'claude-sonnet', compatibleAgents: ['all'], hasKey: true,
};

/** Sign in, serve the capabilities and a providers API backed by `list`; returns the requests the page made. */
async function serve(page: Page, caps: { canManageProviders: boolean; providersEnabled: boolean }, list: Provider[] = [CLAUDE]) {
  const requests: { method: string; path: string; body: any }[] = [];
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/settings\/capabilities/, (route) => route.fulfill({
    json: { ...caps, providersHint: caps.providersEnabled ? null : 'Provider management is off on this server: set KoboldLair:ProviderConfig:MasterKey (or the KOBOLDLAIR_MASTER_KEY environment variable) and restart it.' },
  }));
  await page.route(/\/api\/v1\/providers(\/|\?|$)/, async (route) => {
    const req: Request = route.request();
    const path = new URL(req.url()).pathname.replace('/api/v1/', '');
    const body = req.postData() ? JSON.parse(req.postData()!) : null;
    requests.push({ method: req.method(), path, body });
    const [, name, sub] = path.split('/');
    if (req.method() === 'GET' && !name) return route.fulfill({ json: list });
    if (req.method() === 'GET') {
      const p = list.find((x) => x.name === name);
      return p ? route.fulfill({ json: p }) : route.fulfill({ status: 404, body: '' });
    }
    if (req.method() === 'POST' && !name) {
      list.push({ ...body, hasKey: false });
      return route.fulfill({ json: { name: body.name } });
    }
    if (req.method() === 'PUT') {
      const i = list.findIndex((x) => x.name === name);
      list[i] = { ...list[i], ...body, name };
      return route.fulfill({ json: { name } });
    }
    if (req.method() === 'PATCH' && sub === 'key') {
      const p = list.find((x) => x.name === name)!;
      p.hasKey = !!body.apiKey;
      return route.fulfill({ status: 204, body: '' });
    }
    if (req.method() === 'POST' && sub === 'validate') {
      const p = list.find((x) => x.name === name)!;
      return route.fulfill({ json: p.hasKey && name !== 'broken'
        ? { name, valid: true, message: 'Connected: the provider answered (model claude-sonnet).' }
        : { name, valid: false, message: 'The provider answered with an error: Incorrect API key provided' } });
    }
    if (req.method() === 'DELETE') {
      list.splice(list.findIndex((x) => x.name === name), 1);
      return route.fulfill({ status: 204, body: '' });
    }
    return route.fulfill({ status: 405, body: '' });
  });
  await page.goto('/#/settings');
  return requests;
}

async function rowAction(page: Page, name: string, action: RegExp) {
  await providers(page).locator(`b-data-table .row-action-trigger[data-id="${name}"]`).click();
  await page.getByRole('menuitem', { name: action }).click();
}

test('an admin sees the providers in a table, with whether a key is set', async ({ page }) => {
  await serve(page, { canManageProviders: true, providersEnabled: true });

  await expect(settings(page).locator('b-tabs').getByRole('tab', { name: 'Providers' })).toBeVisible();
  await expect(rows(page)).toHaveCount(1);
  await expect(rows(page).first()).toContainText('claude');
  await expect(rows(page).first()).toContainText('claude-sonnet');
  await expect(rows(page).first()).toContainText('Set');
  await expect(settings(page)).not.toContainText('General');
});

test('adding a provider saves it, then its API key, and the key is never shown', async ({ page }) => {
  const requests = await serve(page, { canManageProviders: true, providersEnabled: true });
  await expect(rows(page)).toHaveCount(1);

  await providers(page).locator('#btn-create').click();
  const form = providers(page).locator('#modal b-form');
  // A new provider starts enabled, needing a key, for all agents
  await expect.poll(() => form.evaluate((f: any) => f.getValues())).toMatchObject({ enabled: true, requiresApiKey: true, compatibleAgents: 'all' });
  await form.evaluate((f: any) => f.setValues({ name: 'local', type: 'vllm', baseUrl: 'http://localhost:8000', defaultModel: 'qwen', apiKey: 'sk-secret' }));
  await providers(page).locator('#btn-save').click();

  await expect(rows(page)).toHaveCount(2);
  const post = requests.find((r) => r.method === 'POST' && r.path === 'providers')!;
  expect(post.body).toMatchObject({ name: 'local', type: 'vllm', baseUrl: 'http://localhost:8000', defaultModel: 'qwen', compatibleAgents: ['all'] });
  expect(post.body).not.toHaveProperty('apiKey');
  expect(requests.find((r) => r.method === 'PATCH')).toMatchObject({ path: 'providers/local/key', body: { apiKey: 'sk-secret' } });
  await expect(rows(page).filter({ hasText: 'local' })).toContainText('Set');
  await expect(settings(page)).not.toContainText('sk-secret');
});

test('editing a provider sends PUT by its name; an empty key field keeps the key', async ({ page }) => {
  const requests = await serve(page, { canManageProviders: true, providersEnabled: true });
  await expect(rows(page)).toHaveCount(1);

  await rowAction(page, 'claude', /edit/i);
  const form = providers(page).locator('#modal b-form');
  await expect.poll(() => form.evaluate((f: any) => f.getValues().defaultModel)).toBe('claude-sonnet');
  await form.evaluate((f: any) => f.setValues({ defaultModel: 'claude-opus' }));
  await providers(page).locator('#btn-save').click();

  await expect(rows(page).first()).toContainText('claude-opus');
  expect(requests.find((r) => r.method === 'PUT')).toMatchObject({ path: 'providers/claude', body: { name: 'claude', defaultModel: 'claude-opus' } });
  expect(requests.some((r) => r.method === 'PATCH')).toBe(false);
});

test('Validate shows what the server found, a failure as an error', async ({ page }) => {
  await serve(page, { canManageProviders: true, providersEnabled: true },
    [CLAUDE, { ...CLAUDE, name: 'broken', displayName: 'Broken' }]);
  await expect(rows(page)).toHaveCount(2);

  await rowAction(page, 'claude', /validate/i);
  await expect(page.locator('b-toast-item[variant="success"]')).toContainText('claude: Connected');

  await rowAction(page, 'broken', /validate/i);
  await expect(page.locator('b-toast-item[variant="error"]')).toContainText('Incorrect API key provided');
});

test('deleting a provider asks first, then removes it', async ({ page }) => {
  const requests = await serve(page, { canManageProviders: true, providersEnabled: true });
  await expect(rows(page)).toHaveCount(1);

  await rowAction(page, 'claude', /delete/i);
  await providers(page).locator('b-confirm-dialog b-button.btn-confirm:visible').click();

  await expect(rows(page).filter({ hasText: 'claude-sonnet' })).toHaveCount(0);
  expect(requests.find((r) => r.method === 'DELETE')).toMatchObject({ path: 'providers/claude' });
});

test('a non-admin sees no Providers tab', async ({ page }) => {
  const requests = await serve(page, { canManageProviders: false, providersEnabled: true });

  await expect(settings(page)).toContainText('Nothing to set here for your account yet');
  await expect(settings(page).locator('b-tabs')).toHaveCount(0);
  expect(requests).toEqual([]);
});

test('with provider management off, the tab says how to turn it on', async ({ page }) => {
  const requests = await serve(page, { canManageProviders: true, providersEnabled: false });

  await expect(settings(page).locator('.providers-off')).toContainText('KoboldLair:ProviderConfig:MasterKey');
  await expect(providers(page)).toHaveCount(0);
  expect(requests).toEqual([]);
});
