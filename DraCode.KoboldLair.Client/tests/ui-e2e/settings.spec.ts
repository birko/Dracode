import { test, expect } from './fixtures.js';
import type { Page, Request } from '@playwright/test';

// TASK-161 / TASK-192: Settings at #/settings, on /api/v1. Providers is Birko Shell's list page over /api/v1/providers, for an
// admin only; with provider management off on the server the tab says how to turn it on. The REST answers are stubbed.

const settings = (page: Page) => page.locator('#route-outlet kobold-settings-view');
const providers = (page: Page) => settings(page).locator('kobold-providers-page');
const rows = (page: Page) => providers(page).locator('b-data-table tbody tr');

interface Provider {
  name: string; displayName: string | null; type: string; enabled: boolean; baseUrl: string | null;
  requiresApiKey: boolean; defaultModel: string | null; compatibleAgents: string[]; hasKey: boolean;
  models?: { modelId: string; displayName: string | null; enabled: boolean; sortOrder: number }[];
}

const CLAUDE: Provider = {
  name: 'claude', displayName: 'Claude', type: 'claude', enabled: true, baseUrl: null, requiresApiKey: true,
  defaultModel: 'claude-sonnet', compatibleAgents: ['all'], hasKey: true,
  models: [{ modelId: 'claude-sonnet', displayName: 'Sonnet', enabled: true, sortOrder: 0 }],
};

/** What GET providers/settings answers, and PUTs change. */
const SETTINGS = () => ({
  dragonProvider: 'claude', dragonModel: null, wyvernProvider: 'claude', wyvernModel: null,
  wyrmProvider: 'claude', wyrmModel: null, koboldProvider: 'claude', koboldModel: null,
  koboldAgentTypeSettings: [{ agentType: 'python', provider: 'claude', model: 'claude-haiku' }],
});

/** Sign in, serve the capabilities and a providers API backed by `list`; returns the requests the page made. */
async function serve(page: Page, caps: { canManageProviders: boolean; providersEnabled: boolean },
  list: Provider[] = [structuredClone(CLAUDE)], settings: any = SETTINGS()) {
  const requests: { method: string; path: string; body: any }[] = [];
  let defaultProvider = 'claude';
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/settings\/capabilities/, (route) => route.fulfill({
    json: { ...caps, providersHint: caps.providersEnabled ? null : 'Provider management is off on this server: set KoboldLair:ProviderConfig:MasterKey (or the KOBOLDLAIR_MASTER_KEY environment variable) and restart it.' },
  }));
  await page.route(/\/api\/v1\/providers(\/|\?|$)/, async (route) => {
    const req: Request = route.request();
    const path = new URL(req.url()).pathname.replace('/api/v1/', '');
    const body = req.postData() ? JSON.parse(req.postData()!) : null;
    requests.push({ method: req.method(), path, body });
    const [, name, sub, subId] = path.split('/');
    if (req.method() === 'GET' && !name) return route.fulfill({ json: list });
    if (name === 'agent-types') return route.fulfill({ json: ['csharp', 'python', 'typescript'] });
    if (name === 'default') {
      if (req.method() === 'PUT') { defaultProvider = body.provider; return route.fulfill({ status: 204, body: '' }); }
      return route.fulfill({ json: { defaultProvider } });
    }
    if (name === 'settings' && !sub) return route.fulfill({ json: settings });
    if (name === 'settings' && sub === 'agent') {
      if (body.provider === 'broken') return route.fulfill({ status: 400, json: { error: "Provider 'broken' is not compatible with 'dragon'" } });
      settings[`${body.agentType}Provider`] = body.provider;
      settings[`${body.agentType}Model`] = body.model;
      return route.fulfill({ status: 204, body: '' });
    }
    if (name === 'settings' && sub === 'kobold') {
      settings.koboldAgentTypeSettings = settings.koboldAgentTypeSettings.filter((o: any) => o.agentType !== body.agentType);
      if (body.provider) settings.koboldAgentTypeSettings.push({ agentType: body.agentType, provider: body.provider, model: body.model });
      return route.fulfill({ status: 204, body: '' });
    }
    if (sub === 'models') {
      const p = list.find((x) => x.name === name)!;
      p.models ??= [];
      if (req.method() === 'POST') {
        p.models = p.models.filter((m) => m.modelId !== body.modelId);
        p.models.push({ sortOrder: 0, displayName: null, ...body });
        return route.fulfill({ json: { name, model: body.modelId } });
      }
      if (req.method() === 'DELETE') {
        p.models = p.models.filter((m) => m.modelId !== decodeURIComponent(subId));
        return route.fulfill({ status: 204, body: '' });
      }
    }
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

const modelRows = (page: Page) => providers(page).locator('#models-modal .models-table tbody tr[data-model-row]');

test('Models lists a provider\'s models; one is added, disabled and removed', async ({ page }) => {
  const requests = await serve(page, { canManageProviders: true, providersEnabled: true });
  await expect(rows(page)).toHaveCount(1);

  await rowAction(page, 'claude', /models/i);
  await expect(modelRows(page)).toHaveCount(1);
  await expect(modelRows(page).first()).toContainText('claude-sonnet');

  await providers(page).locator('#modelForm').evaluate((f: any) => f.setValues({ modelId: 'claude-opus', displayName: 'Opus' }));
  await providers(page).locator('#btn-add-model').click();
  await expect(modelRows(page)).toHaveCount(2);
  expect(requests.find((r) => r.method === 'POST' && r.path === 'providers/claude/models')?.body)
    .toMatchObject({ modelId: 'claude-opus', displayName: 'Opus', enabled: true });

  await modelRows(page).filter({ hasText: 'claude-opus' }).locator('b-button[data-model-action="toggle"]').click();
  await expect(modelRows(page).filter({ hasText: 'claude-opus' })).toContainText('No');
  await modelRows(page).filter({ hasText: 'claude-opus' }).locator('b-button[data-model-action="remove"]').click();
  await expect(modelRows(page)).toHaveCount(1);
  expect(requests.some((r) => r.method === 'DELETE' && r.path === 'providers/claude/models/claude-opus')).toBe(true);
});

const agents = (page: Page) => settings(page).locator('kobold-agent-providers');

async function openAgents(page: Page, list?: Provider[]) {
  const requests = await serve(page, { canManageProviders: true, providersEnabled: true },
    list ?? [structuredClone(CLAUDE), { ...structuredClone(CLAUDE), name: 'local', displayName: 'Local', type: 'vllm' }]);
  await settings(page).locator('b-tabs').getByRole('tab', { name: 'Agents' }).click();
  await expect(agents(page).locator('#agentsForm')).toBeVisible();
  await expect.poll(() => agents(page).locator('#agentsForm').evaluate((f: any) => f.getValues().dragonProvider)).toBe('claude');
  return requests;
}

test('the Agents tab sets the default provider and the provider and model of an orchestrator', async ({ page }) => {
  const requests = await openAgents(page);

  await agents(page).locator('#defaultForm').evaluate((f: any) => f.setValues({ provider: 'local' }));
  await agents(page).locator('#saveDefault').click();
  await expect.poll(() => requests.find((r) => r.method === 'PUT' && r.path === 'providers/default')?.body).toEqual({ provider: 'local' });

  await agents(page).locator('#agentsForm').evaluate((f: any) => f.setValues({ dragonProvider: 'local', dragonModel: 'qwen' }));
  await agents(page).locator('#saveAgents').click();
  await expect(page.locator('b-toast-item[variant="success"]').last()).toContainText('Dragon now uses local (qwen)');
  const puts = requests.filter((r) => r.method === 'PUT' && r.path === 'providers/settings/agent');
  expect(puts.map((r) => r.body)).toEqual([{ agentType: 'dragon', provider: 'local', model: 'qwen' }]);
});

test('a refused orchestrator change shows the server\'s reason', async ({ page }) => {
  await openAgents(page, [structuredClone(CLAUDE), { ...structuredClone(CLAUDE), name: 'broken', displayName: null }]);

  await agents(page).locator('#agentsForm').evaluate((f: any) => f.setValues({ dragonProvider: 'broken' }));
  await agents(page).locator('#saveAgents').click();
  await expect(page.locator('b-toast-item[variant="error"]')).toContainText("not compatible with 'dragon'");
});

test('Kobold overrides are listed, added and removed', async ({ page }) => {
  const requests = await openAgents(page);
  const overrideRows = agents(page).locator('.overrides tbody tr[data-agent-type]');
  await expect(overrideRows).toHaveCount(1);
  await expect(overrideRows.first()).toContainText('python');

  await agents(page).locator('#overrideForm').evaluate((f: any) => f.setValues({ agentType: 'csharp', provider: 'local', model: 'qwen-coder' }));
  await agents(page).locator('#addOverride').click();
  await expect(overrideRows).toHaveCount(2);
  expect(requests.find((r) => r.path === 'providers/settings/kobold')?.body).toEqual({ agentType: 'csharp', provider: 'local', model: 'qwen-coder' });

  await overrideRows.filter({ hasText: 'python' }).locator('b-button[data-remove]').click();
  await expect(overrideRows).toHaveCount(1);
  expect(requests.filter((r) => r.path === 'providers/settings/kobold').at(-1)?.body).toEqual({ agentType: 'python', provider: null, model: null });
});

test('#/settings/agents opens the Agents tab', async ({ page }) => {
  await serve(page, { canManageProviders: true, providersEnabled: true });
  await page.goto('/#/settings/agents');
  await expect(settings(page).locator('b-tabs').getByRole('tab', { name: 'Agents' })).toHaveAttribute('aria-selected', 'true');
});

test('a provider added on the Providers tab can be chosen on the Agents tab right away', async ({ page }) => {
  // TASK-192: the Agents tab loaded its providers once; one added afterwards could not be picked
  await serve(page, { canManageProviders: true, providersEnabled: true });
  await settings(page).locator('b-tabs').getByRole('tab', { name: 'Agents' }).click();
  await expect(agents(page).locator('#agentsForm')).toBeVisible();

  await settings(page).locator('b-tabs').getByRole('tab', { name: 'Providers' }).click();
  await providers(page).locator('#btn-create').click();
  await providers(page).locator('#modal b-form').evaluate((f: any) => f.setValues({ name: 'later', type: 'vllm' }));
  await providers(page).locator('#btn-save').click();
  await expect(rows(page)).toHaveCount(2);

  await settings(page).locator('b-tabs').getByRole('tab', { name: 'Agents' }).click();
  await expect.poll(() => agents(page).locator('#agentsForm').evaluate((f: any) => {
    f.setValues({ dragonProvider: 'later' });
    return f.getValues().dragonProvider;
  })).toBe('later');
});

test('an orchestrator set to a provider that is not available shows it as such', async ({ page }) => {
  const s = SETTINGS();
  s.dragonProvider = 'zai';
  await serve(page, { canManageProviders: true, providersEnabled: true }, [structuredClone(CLAUDE)], s);
  await settings(page).locator('b-tabs').getByRole('tab', { name: 'Agents' }).click();

  await expect.poll(() => agents(page).locator('#agentsForm').evaluate((f: any) => f.getValues().dragonProvider)).toBe('zai');
  await expect(agents(page).locator('#agentsForm')).toContainText('zai (not available)');
});
