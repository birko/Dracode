import { test, expect } from './fixtures.js';
import type { Page, Request } from '@playwright/test';

// TASK-159: the header bell (unread count + menu) and the project cards' unread badges, fed by GET /api/v1/notifications
// and POST /api/v1/notifications/{id}/read (stubbed with page.route). The docked Dragon's socket is held open silently.

const bell = (page: Page) => page.locator('kobold-lair-shell kobold-notification-bell');
const bellButton = (page: Page) => bell(page).locator('button.bell');
const count = (page: Page) => bell(page).locator('.count');
const rows = (page: Page) => bell(page).locator('b-dropdown-menu .item');

interface Note { id: string; projectId: string; projectName: string; type: string; message: string; createdAt: string; read: boolean }

const NOTES = (): Note[] => [
  { id: 'n-1', projectId: 'p-1', projectName: 'waver', type: 'feature_branch_ready', message: 'Greeting is ready to merge', createdAt: new Date(Date.now() - 5 * 60_000).toISOString(), read: false },
  { id: 'n-2', projectId: 'p-2', projectName: 'ledger-api', type: 'escalation', message: 'Task stalled: retrying with a revised plan', createdAt: new Date(Date.now() - 60 * 60_000).toISOString(), read: false },
  { id: 'n-3', projectId: 'p-1', projectName: 'waver', type: 'project_complete', message: 'All features complete', createdAt: new Date(Date.now() - 3 * 3600_000).toISOString(), read: false },
  { id: 'n-4', projectId: 'p-1', projectName: 'waver', type: 'escalation', message: 'An older one, already read', createdAt: new Date(Date.now() - 26 * 3600_000).toISOString(), read: true },
];

const PROJECTS = [
  { id: 'p-1', name: 'waver', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z', tasks: { total: 2, done: 1, inProgress: 0, failed: 0 } },
  { id: 'p-2', name: 'ledger-api', ownerId: 'u-1', status: 'Analyzed', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z', tasks: { total: 0, done: 0, inProgress: 0, failed: 0 } },
];

async function serve(page: Page, notes: Note[], { signedIn = true } = {}) {
  const lists: Request[] = [];
  const reads: string[] = [];
  if (signedIn) await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/(projects|notifications|tasks)(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/notifications')) {
      lists.push(route.request());
      const sorted = [...notes].sort((a, b) => Number(a.read) - Number(b.read) || b.createdAt.localeCompare(a.createdAt));
      return route.fulfill({ json: sorted.map((n) => ({ ...n, metadata: {}, readAt: n.read ? n.createdAt : null })) });
    }
    if (path.endsWith('/read')) {
      const id = path.split('/')[4];
      const note = notes.find((n) => n.id === id);
      if (!note) return route.fulfill({ status: 404, body: '' });
      note.read = true;
      reads.push(id);
      return route.fulfill({ json: { ...note, metadata: {} } });
    }
    if (path.endsWith('/api/v1/projects')) return route.fulfill({ json: PROJECTS });
    if (path.endsWith('/tasks')) return route.fulfill({ json: [] });
    const id = path.split('/')[4];
    const project = PROJECTS.find((p) => p.id === id);
    return project ? route.fulfill({ json: project }) : route.fulfill({ status: 404, body: '' });
  });
  return { lists, reads };
}

test('the bell shows the unread count and lists the notifications, unread first', async ({ page }) => {
  const { lists } = await serve(page, NOTES());
  await page.goto('/#/');

  await expect(count(page)).toHaveText('3');
  await expect(bellButton(page)).toHaveAttribute('aria-label', 'Notifications, 3 unread');
  await bellButton(page).click();
  await expect(rows(page)).toHaveCount(4);
  await expect(rows(page).nth(0)).toContainText('waver');
  await expect(rows(page).nth(0)).toContainText('Greeting is ready to merge');
  await expect(rows(page).nth(1)).toContainText('ledger-api');
  await expect(rows(page).nth(3)).toContainText('An older one, already read');
  await expect(bell(page).locator('b-dropdown-menu .heading')).toHaveText(['Unread', 'Earlier']);

  expect(new URL(lists[0].url()).searchParams.get('scope')).toBe('all');
  expect(lists[0].headers()['authorization']).toBe('Bearer test-token');
});

test('opening a notification marks it read and opens its project', async ({ page }) => {
  const { reads } = await serve(page, NOTES());
  await page.goto('/#/');
  await expect(count(page)).toHaveText('3');

  await bellButton(page).click();
  await rows(page).filter({ hasText: 'Task stalled' }).click();

  await expect(page).toHaveURL(/#\/projects\/p-2\/board$/);
  expect(reads).toEqual(['n-2']);
  await expect(count(page)).toHaveText('2');
});

test('the project cards show their unread count, and reading one lowers it', async ({ page }) => {
  await serve(page, NOTES());
  await page.goto('/#/');

  const card = (id: string) => page.locator(`#route-outlet kobold-home-view .project-card[data-project-id="${id}"]`);
  await expect(card('p-1').locator('b-badge.unread')).toHaveText('2 new');
  await expect(card('p-2').locator('b-badge.unread')).toHaveText('1 new');

  await bellButton(page).click();
  await rows(page).filter({ hasText: 'Greeting is ready to merge' }).click();
  await page.goto('/#/');
  await expect(card('p-1').locator('b-badge.unread')).toHaveText('1 new');
});

test('a new notification reaches the bell on its own', async ({ page }) => {
  const notes = NOTES();
  await serve(page, notes);
  await page.goto('/#/');
  await expect(count(page)).toHaveText('3');

  notes.push({ id: 'n-5', projectId: 'p-2', projectName: 'ledger-api', type: 'escalation', message: 'A new one', createdAt: new Date().toISOString(), read: false });
  await expect(count(page)).toHaveText('4', { timeout: 20_000 });
});

test('with nothing unread the bell shows no count, and an empty list says so', async ({ page }) => {
  await serve(page, []);
  await page.goto('/#/');

  await expect(bellButton(page)).toHaveAttribute('aria-label', 'Notifications');
  await expect(count(page)).toHaveCount(0);
  await bellButton(page).click();
  await expect(rows(page)).toHaveText(['No notifications']);
});

test('signed out there is no bell and nothing is requested', async ({ page }) => {
  const { lists } = await serve(page, NOTES(), { signedIn: false });
  await page.goto('/#/');

  await expect(page.locator('#route-outlet kobold-home-view')).toContainText('Sign in');
  await expect(bellButton(page)).toHaveCount(0);
  expect(lists).toHaveLength(0);
});

test('markup in a notification is shown as text', async ({ page }) => {
  await serve(page, [{ ...NOTES()[0], message: '<img src=x onerror="window.__pwned=1"> done', projectName: '<b>evil</b>' }]);
  await page.goto('/#/');
  await bellButton(page).click();

  await expect(rows(page).first()).toContainText('<img src=x onerror="window.__pwned=1"> done');
  await expect(rows(page).first()).toContainText('<b>evil</b>');
  await expect(rows(page).first().locator('img')).toHaveCount(0);
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
});

test('at phone width the bell gives way to the Notifications tab, whose list wraps with no horizontal scroll', async ({ page }) => {
  // TASK-164 (D21): below 900px the notifications are a tab of their own; the bell's menu is checked at 900px.
  await page.setViewportSize({ width: 375, height: 800 });
  await serve(page, [...NOTES(), { ...NOTES()[1], id: 'n-9', message: 'A very long notification message '.repeat(8) }]);
  await page.goto('/#/');
  await expect(bell(page)).toBeHidden();
  await page.locator('kobold-lair-shell nav.phone-tabs [data-surface="notifications"]').click();
  await expect(page.locator('#route-outlet kobold-notifications-page .notification')).toHaveCount(5);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);

  await page.setViewportSize({ width: 900, height: 800 });
  await page.goto('/#/');
  await bellButton(page).click();
  await expect(rows(page)).toHaveCount(5);

  const box = await bell(page).locator('b-dropdown-menu').evaluate((el) => {
    const r = el.shadowRoot!.querySelector('.menu')!.getBoundingClientRect();
    return { left: Math.round(r.left), right: Math.round(r.right) };
  });
  expect(box.left).toBeGreaterThanOrEqual(0);
  expect(box.right).toBeLessThanOrEqual(900);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
});
