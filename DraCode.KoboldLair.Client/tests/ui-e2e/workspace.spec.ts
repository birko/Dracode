import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-150: a project opens at #/projects/{id}/{tab}, the workspace frame with its tabs; the Spec tab shows the
// specification (Markdown, sanitised) and the features, read only. Data from /api/v1, stubbed with page.route.

const workspace = (page: Page) => page.locator('#route-outlet kobold-project-workspace');
const specTab = (page: Page) => workspace(page).locator('kobold-spec-tab');
const specBody = (page: Page) => specTab(page).locator('.spec-body');

const PROJECT = {
  id: 'p-1', name: 'waver', ownerId: 'u-1', status: 'InProgress', executionState: 'Running',
  createdAt: '2026-11-18T10:00:00Z',
};

const SPEC = [
  '# Waver',
  '',
  'A small app that **waves** back.',
  '',
  '## Requirements',
  '',
  '- greet the user',
  '- wave on request',
  '',
  '1. first step',
  '2. second step',
  '',
  '```ts',
  'export function wave(): string { return "👋"; }',
  '```',
].join('\n');

const FEATURES = [
  { id: 'f-1', name: 'Greeting', description: 'Says hello', priority: 'high', status: 'Completed' },
  { id: 'f-2', name: 'Waving', description: 'Waves on request', priority: 'normal', status: 'InProgress' },
  { id: 'f-3', name: 'Dancing', description: 'Maybe later', priority: 'low', status: 'Draft' },
];

interface Stub { project?: unknown; projectStatus?: number; spec?: string; features?: unknown[] }

/** Sign in and serve one project's endpoints. */
async function serveProject(page: Page, stub: Stub = {}): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  // The workspace docks Dragon (TASK-151): its socket is held open here and says nothing; dragon-dock.spec.ts covers it.
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  // A regex, not a glob: `**/projects**` does not match `/projects/{id}/…`.
  await page.route(/\/api\/v1\/projects(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/api/v1/projects')) return route.fulfill({ json: [{ ...PROJECT, tasks: null }] });
    if (path.endsWith('/specification')) {
      return route.fulfill({ json: { name: 'waver', content: stub.spec ?? SPEC, version: 3, contentHash: 'abc' } });
    }
    if (path.endsWith('/features')) return route.fulfill({ json: stub.features ?? FEATURES });
    if (stub.projectStatus) return route.fulfill({ status: stub.projectStatus, body: '' });
    return route.fulfill({ json: stub.project ?? PROJECT });
  });
}

function collectErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(e.message));
  return errors;
}

test('a project card opens the workspace on its Spec tab, and the URL carries project and tab', async ({ page }) => {
  await serveProject(page);
  await page.goto('/#/');

  await page.locator('#route-outlet kobold-home-view .project-card').first().click();
  await expect(page).toHaveURL(/#\/projects\/p-1\/spec$/);
  await expect(workspace(page)).toHaveCount(1);
  await expect(workspace(page).locator('.project-title')).toHaveText('waver');
});

test('the Spec tab renders the specification as Markdown and lists the features with their status', async ({ page }) => {
  const errors = collectErrors(page);
  await serveProject(page);
  await page.goto('/#/projects/p-1/spec');

  await expect(specBody(page).locator('h1')).toHaveText('Waver');
  await expect(specBody(page).locator('h2')).toHaveText('Requirements');
  await expect(specBody(page).locator('strong')).toHaveText('waves');
  await expect(specBody(page).locator('ul > li')).toHaveText(['greet the user', 'wave on request']);
  await expect(specBody(page).locator('ol > li')).toHaveText(['first step', 'second step']);
  await expect(specBody(page).locator('pre code')).toContainText('export function wave()');

  const features = specTab(page).locator('.feature');
  await expect(features).toHaveCount(3);
  await expect(features.filter({ hasText: 'Greeting' })).toContainText('Completed');
  await expect(features.filter({ hasText: 'Waving' })).toContainText('InProgress');
  await expect(features.filter({ hasText: 'Dancing' })).toContainText('Draft');
  expect(errors).toEqual([]);
});

test('the Spec tab is read only and says edits go through Dragon', async ({ page }) => {
  await serveProject(page);
  await page.goto('/#/projects/p-1/spec');

  await expect(specBody(page).locator('h1')).toHaveText('Waver');
  await expect(specTab(page).locator('textarea, input:not([type="checkbox"]), button, b-markdown-editor, [contenteditable]')).toHaveCount(0);
  await expect(specTab(page).locator('.note')).toContainText('ask Dragon');
});

test('markup in the specification cannot run script or carry handlers', async ({ page }) => {
  const errors = collectErrors(page);
  let dialogs = 0;
  page.on('dialog', (d) => { dialogs++; return d.dismiss(); });
  await serveProject(page, {
    spec: [
      '# Hostile',
      '',
      '<script>window.__pwned = 1</script>',
      '',
      '<img src=x onerror="window.__pwned = 2">',
      '',
      '[click](javascript:window.__pwned=3)',
      '',
      '[quote](x" onmouseover="window.__pwned=4)',
      '',
      '![pic](y" onerror="window.__pwned=5)',
      '',
      '[fine](https://example.com/docs)',
    ].join('\n'),
  });
  await page.goto('/#/projects/p-1/spec');

  await expect(specBody(page).locator('h1')).toHaveText('Hostile');
  const found = await specBody(page).evaluate((root) => {
    const all = [root, ...root.querySelectorAll('*')];
    return {
      scripts: root.querySelectorAll('script').length,
      handlers: all.flatMap((el) => [...el.attributes].filter((a) => a.name.startsWith('on')).map((a) => a.name)),
      badHrefs: [...root.querySelectorAll('[href], [src]')]
        .map((el) => el.getAttribute('href') ?? el.getAttribute('src') ?? '')
        .filter((v) => /^\s*javascript:/i.test(v)),
      fine: root.querySelector('a[href="https://example.com/docs"]')?.textContent,
    };
  });
  expect(found).toEqual({ scripts: 0, handlers: [], badHrefs: [], fine: 'fine' });
  await specBody(page).locator('a').first().hover().catch(() => undefined);
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
  expect(dialogs).toBe(0);
  expect(errors.filter((e) => !/Failed to load resource/.test(e))).toEqual([]);
});

test('a list right after a paragraph line stays one tight list, and a code block starts on its first line', async ({ page }) => {
  await serveProject(page, { spec: '# Tight\n\nComponents:\n- one\n- two\n- three\n\n```\nfirst line\nsecond line\n```\n' });
  await page.goto('/#/projects/p-1/spec');

  await expect(specBody(page).locator('ul > li')).toHaveText(['one', 'two', 'three']);
  await expect(specBody(page).locator('ul > br')).toHaveCount(0);
  expect(await specBody(page).locator('pre code').evaluate((el) => el.textContent)).toBe('first line\nsecond line\n');
});

test('a spec with no content and no features says so', async ({ page }) => {
  await serveProject(page, { spec: '', features: [] });
  await page.goto('/#/projects/p-1/spec');

  await expect(specTab(page)).toContainText('No specification yet');
  await expect(specTab(page)).toContainText('No features yet');
});

test('an unknown or not-owned project shows the not-found view', async ({ page }) => {
  await serveProject(page, { projectStatus: 404 });
  await page.goto('/#/projects/nope/spec');

  await expect(page.locator('#route-outlet kobold-not-found-view')).toHaveCount(1);
  await expect(specTab(page)).toHaveCount(0);
});

test('a tab the workspace does not have shows the not-found view', async ({ page }) => {
  await serveProject(page);
  await page.goto('/#/projects/p-1/nonsense');

  await expect(page.locator('#route-outlet kobold-not-found-view')).toHaveCount(1);
});

test('#/projects/{id} opens the Spec tab', async ({ page }) => {
  await serveProject(page);
  await page.goto('/#/projects/p-1');

  await expect(page).toHaveURL(/#\/projects\/p-1\/spec$/);
  await expect(specBody(page).locator('h1')).toHaveText('Waver');
});

test('below the breakpoint the workspace has no horizontal scroll, even with a long code line', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await serveProject(page, {
    spec: `# Wide\n\n\`\`\`\n${'const veryLongIdentifier = '.repeat(20)}1;\n\`\`\`\n\n| a | b | c | d | e | f |\n|---|---|---|---|---|---|\n| ${'cell '.repeat(10)} | 2 | 3 | 4 | 5 | 6 |\n`,
  });
  await page.goto('/#/projects/p-1/spec');

  await expect(specBody(page).locator('h1')).toHaveText('Wide');
  const overflow = await page.evaluate(() => {
    const shell = document.querySelector('kobold-lair-shell')!;
    const content = shell.shadowRoot!.querySelector('.app-content') as HTMLElement;
    return {
      page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      content: content.scrollWidth - content.clientWidth,
    };
  });
  expect(overflow).toEqual({ page: 0, content: 0 });
});
