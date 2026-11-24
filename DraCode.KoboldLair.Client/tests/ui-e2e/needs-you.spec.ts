import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-147: "Needs you" — the homepage lists what waits on the user, grouped by project, newest first, each item
// opening its project; inside a project the header pill counts the other projects with items, hidden at zero; an
// answered decision leaves at once. REST stubbed (the list is the test's to change); /dragon played by a small fake.

interface Item { id: string; kind: string; projectId: string; projectName: string; title: string; createdAt: string }

const minutesAgo = (m: number) => new Date(Date.now() - m * 60000).toISOString();
const PROJECTS = [
  { id: 'p-1', name: 'Shop' },
  { id: 'p-2', name: 'Blog' },
  { id: 'p-3', name: 'Wiki' },
];
const ITEMS = (): Item[] => [
  { id: 'i-1', kind: 'failed_task', projectId: 'p-1', projectName: 'Shop', title: 'Build the API', createdAt: minutesAgo(50) },
  { id: 'i-2', kind: 'decision', projectId: 'p-2', projectName: 'Blog', title: 'Which theme should the blog use?', createdAt: minutesAgo(5) },
  { id: 'i-3', kind: 'merge_ready', projectId: 'p-1', projectName: 'Shop', title: 'Feature "Checkout" is complete! Branch `feature/checkout` is ready for merge into main.', createdAt: minutesAgo(10) },
  { id: 'i-4', kind: 'blocked_task', projectId: 'p-3', projectName: 'Wiki', title: 'Wire the search', createdAt: minutesAgo(30) },
];

async function serve(page: Page, items: { list: Item[] }): Promise<{ needsYouCalls: () => number }> {
  let calls = 0;
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/needs-you(\/|\?|$)/, (route) => { calls++; return route.fulfill({ json: items.list }); });
  await page.route(/\/api\/v1\/(projects|tasks)(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    const full = (p: typeof PROJECTS[number]) => ({ ...p, ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z', tasks: null });
    if (path === '/api/v1/projects') return route.fulfill({ json: PROJECTS.map(full) });
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: 'x', content: '# Spec', version: 1 } });
    if (path.endsWith('/features') || path.endsWith('/tasks')) return route.fulfill({ json: [] });
    const project = PROJECTS.find((p) => p.id === path.split('/')[4]);
    return project ? route.fulfill({ json: full(project) }) : route.fulfill({ status: 404, body: '' });
  });
  return { needsYouCalls: () => calls };
}

const home = (page: Page) => page.locator('#route-outlet kobold-home-view');
const groups = (page: Page) => home(page).locator('.needs-group');
const pill = (page: Page) => page.locator('kobold-lair-shell kobold-needs-you-pill a');
/** The pill's text as shown: the long form, or the short one on a phone. */
const pillText = (page: Page) => pill(page).locator('span:visible');

test('the homepage lists what needs you, grouped by project, newest first, each item opening its project', async ({ page }) => {
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await serve(page, { list: ITEMS() });
  await page.goto('/#/');

  await expect(groups(page).locator('h3')).toHaveText(['Blog', 'Shop', 'Wiki']); // the project with the newest item first
  await expect(groups(page).nth(1).locator('.needs-item b-badge')).toHaveText(['Ready to merge', 'Failed']);
  await expect(groups(page).nth(1).locator('.needs-title')).toHaveText([
    'Feature "Checkout" is complete! Branch `feature/checkout` is ready for merge into main.', 'Build the API']);
  await expect(groups(page).nth(0).locator('b-badge')).toHaveText('Decision');
  await expect(groups(page).nth(2).locator('b-badge')).toHaveText('Blocked');
  await expect(groups(page).nth(0).locator('.needs-when')).toHaveText('5 min ago');

  await groups(page).nth(2).locator('.needs-item').click();
  await expect(page).toHaveURL(/#\/projects\/p-3\/board$/);
});

test('with nothing waiting the homepage says so', async ({ page }) => {
  await serve(page, { list: [] });
  await page.goto('/#/');
  await expect(home(page).locator('.project-card')).toHaveCount(3);
  await expect(home(page).locator('.needs-you')).toHaveText('Nothing needs you right now.');
});

test('inside a project the pill counts the other projects that need you, leads home, and is hidden at zero', async ({ page }) => {
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  const items = { list: ITEMS() };
  await serve(page, items);
  await page.goto('/#/projects/p-1/spec');
  await expect(pillText(page)).toHaveText('2 other projects need you'); // Blog and Wiki; Shop is the one open
  await expect(pill(page)).toHaveAttribute('href', '#/');

  items.list = [ITEMS()[0]]; // only Shop's own item is left
  await page.reload();
  await expect(page.locator('#route-outlet kobold-project-workspace .project-title')).toHaveText('Shop');
  await expect(pill(page)).toHaveCount(0);

  await page.goto('/#/projects/p-2/spec');
  await expect(pillText(page)).toHaveText('1 other project needs you');
  await pill(page).click();
  await expect(home(page).locator('.needs-group h3')).toHaveText(['Shop']);
  await expect(pill(page)).toHaveCount(0); // not on the homepage
});

test('an answered decision leaves Needs you at once', async ({ page }) => {
  const items = { list: ITEMS() };
  const server = await serve(page, items);
  await page.routeWebSocket(/\/dragon(\?|$)/, (ws) => {
    ws.onMessage((raw) => {
      const msg = JSON.parse(String(raw));
      if (msg.type === 'select_project') {
        ws.send(JSON.stringify({ type: 'project_selected', sessionId: 's-1', projectId: msg.projectId, projectName: 'Blog', messageCount: 0 }));
      } else if (msg.type === 'client_ready') {
        ws.send(JSON.stringify({
          type: 'project_notification', notificationType: 'decision_required', sessionId: 's-1', projectName: 'Blog', message: 'Which theme should the blog use?',
          metadata: { decisionId: 'i-2', question: 'Which theme should the blog use?', source: 'AskHuman', context: '', timeoutAt: '',
            responseSchema: JSON.stringify({ kind: 'choice', options: ['Light', 'Dark'], allowFreeText: false }) },
        }));
      } else if (msg.type === 'answer_decision') {
        items.list = items.list.filter((i) => i.id !== 'i-2');
        ws.send(JSON.stringify({ type: 'decision_answer_result', sessionId: 's-1', decisionId: 'i-2', result: 'Resolved', message: 'Answered.' }));
      }
    });
  });
  await page.goto('/#/projects/p-1/spec');
  await expect(pillText(page)).toHaveText('2 other projects need you');
  await page.goto('/#/projects/p-2/spec');
  const dock = page.locator('#route-outlet kobold-dragon-dock');
  await dock.locator('article.decision .decision-head').click();
  const before = server.needsYouCalls();
  await dock.locator('b-button[data-answer="Dark"]').click();

  await expect(dock.locator('article.decision')).toHaveCount(0);
  await expect.poll(() => server.needsYouCalls()).toBeGreaterThan(before);
  await page.goto('/#/');
  await expect(groups(page).locator('h3')).toHaveText(['Shop', 'Wiki']);
});

test('opening the homepage shows the current list, even while the pill keeps an older one', async ({ page }) => {
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  const items = { list: ITEMS() };
  const server = await serve(page, items);
  await page.goto('/#/projects/p-1/spec');
  await expect(pillText(page)).toHaveText('2 other projects need you');

  items.list = items.list.filter((i) => i.projectId !== 'p-3'); // Wiki's blocked task was dealt with elsewhere
  const before = server.needsYouCalls();
  await pill(page).click();

  await expect(groups(page).locator('h3')).toHaveText(['Blog', 'Shop'], { timeout: 3000 }); // well before the next poll
  expect(server.needsYouCalls()).toBeGreaterThan(before);
});

test('item text is shown as text', async ({ page }) => {
  await serve(page, { list: [{ ...ITEMS()[0], title: '<img src=x onerror="window.__pwned=1">', projectName: '<b>Shop</b>' }] });
  await page.goto('/#/');
  await expect(groups(page).locator('.needs-title')).toHaveText('<img src=x onerror="window.__pwned=1">');
  await expect(groups(page).locator('h3')).toHaveText('<b>Shop</b>');
  await expect(home(page).locator('.needs-you img')).toHaveCount(0);
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
});

test('the list fits a phone, and the pill the narrowest tablet, without horizontal scroll', async ({ page }) => {
  // A phone's header has no pill since TASK-164: its Projects tab lists what needs you. The pill shows from 900px.
  await page.setViewportSize({ width: 375, height: 800 });
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await serve(page, { list: ITEMS() });
  await page.goto('/#/');
  await expect(groups(page)).toHaveCount(3);
  const overflow = () => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(await overflow()).toBe(0);

  await expect(pill(page)).toBeHidden();

  await page.setViewportSize({ width: 900, height: 800 });
  await page.goto('/#/projects/p-1/spec');
  await expect(pillText(page)).toHaveText('2 other projects need you');
  await expect(pill(page)).toHaveAttribute('aria-label', '2 other projects need you');
  const [pillBox, bellBox] = [await pill(page).boundingBox(), await page.locator('kobold-lair-shell kobold-notification-bell').boundingBox()];
  expect(pillBox!.x + pillBox!.width).toBeLessThanOrEqual(bellBox!.x); // nothing drawn over the pill
  expect(await overflow()).toBe(0);
});
