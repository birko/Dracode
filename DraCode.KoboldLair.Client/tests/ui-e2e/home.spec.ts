import { test, expect } from './fixtures.js';
import type { Page, Request } from '@playwright/test';

// TASK-145: the header frame replaces the ribbon, and #/ is the projects homepage, fed by GET /api/v1/projects
// (stubbed with page.route: /api/v1 needs a signed-in caller and the host runs without the server).

const home = (page: Page) => page.locator('#route-outlet kobold-home-view');
const cards = (page: Page) => home(page).locator('.project-card');

const PROJECTS = [
  {
    id: 'p-1', name: 'waver', ownerId: 'u-1', status: 'InProgress', executionState: 'Running',
    createdAt: '2026-11-18T10:00:00Z', tasks: { total: 4, done: 3, inProgress: 1, failed: 0 },
  },
  {
    id: 'p-2', name: 'ledger-api', ownerId: 'u-2', status: 'Analyzed', executionState: 'Paused',
    createdAt: '2026-11-19T10:00:00Z', tasks: { total: 5, done: 1, inProgress: 0, failed: 2 },
  },
  {
    id: 'p-3', name: 'fresh-idea', ownerId: 'u-1', status: 'New', executionState: 'Running',
    createdAt: '2026-11-20T10:00:00Z', tasks: { total: 0, done: 0, inProgress: 0, failed: 0 },
  },
];

/** Sign in (a token in the auth store) and serve the project list; returns the requests the page made. */
async function serveProjects(page: Page, projects: unknown[]): Promise<Request[]> {
  const requests: Request[] = [];
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route('**/api/v1/projects**', (route) => {
    requests.push(route.request());
    return route.fulfill({ json: projects });
  });
  return requests;
}

function collectErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(e.message));
  return errors;
}

test('the header replaces the ribbon, and #/ is the projects homepage', async ({ page }) => {
  const errors = collectErrors(page);
  await serveProjects(page, PROJECTS);
  await page.goto('/#/');

  await expect(home(page)).toHaveCount(1);
  const frame = await page.evaluate(() => {
    const root = document.querySelector('kobold-lair-shell')?.shadowRoot;
    return {
      ribbon: !!root?.querySelector('b-ribbon'),
      header: !!root?.querySelector('header.app-header'),
      brand: root?.querySelector('.brand-name')?.textContent?.trim(),
      slots: ['project-picker', 'notifications', 'user-menu'].filter((s) => !!root?.querySelector(`[data-header-slot="${s}"]`)),
    };
  });
  expect(frame).toEqual({ ribbon: false, header: true, brand: 'KoboldLair', slots: ['project-picker', 'notifications', 'user-menu'] });
  expect(errors).toEqual([]);
});

test('a card per project: name, status, execution state and progress from its task counts', async ({ page }) => {
  await serveProjects(page, PROJECTS);
  await page.goto('/#/');

  await expect(cards(page)).toHaveCount(3);
  const waver = cards(page).filter({ hasText: 'waver' });
  await expect(waver).toContainText('InProgress');
  await expect(waver).toContainText('Running');
  await expect(waver).toContainText('3 of 4 tasks done');
  await expect(waver.locator('b-progress')).toHaveAttribute('value', '75');

  const ledger = cards(page).filter({ hasText: 'ledger-api' });
  await expect(ledger).toContainText('Paused');
  await expect(ledger).toContainText('1 of 5 tasks done');
  await expect(ledger).toContainText('2 failed');
  await expect(ledger.locator('b-progress')).toHaveAttribute('value', '20');

  await expect(cards(page).filter({ hasText: 'fresh-idea' })).toContainText('No tasks yet');
});

test('the homepage asks for every project, so an admin sees them all', async ({ page }) => {
  const requests = await serveProjects(page, PROJECTS);
  await page.goto('/#/');

  await expect(cards(page)).toHaveCount(3);
  expect(requests.length).toBeGreaterThan(0);
  expect(new URL(requests[0].url()).searchParams.get('scope')).toBe('all');
  expect(requests[0].headers()['authorization']).toBe('Bearer test-token');
});

test('with no projects the homepage says how to start one with Dragon', async ({ page }) => {
  await serveProjects(page, []);
  await page.goto('/#/');

  await expect(cards(page)).toHaveCount(0);
  await expect(home(page)).toContainText('No projects yet');
  await expect(home(page).locator('a[href="#/dragon"]')).toBeVisible();
});

test('signed out, the homepage asks to sign in and requests nothing', async ({ page }) => {
  let asked = false;
  await page.route('**/api/v1/projects**', (route) => { asked = true; return route.fulfill({ json: [] }); });
  await page.goto('/#/');

  await expect(home(page)).toContainText('Sign in to see your projects');
  expect(asked).toBe(false);
});

test('a failed load says so instead of showing an empty homepage', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route('**/api/v1/projects**', (route) => route.fulfill({ status: 503, json: { error: 'down' } }));
  await page.goto('/#/');

  await expect(home(page)).toContainText('Could not load your projects');
  await expect(home(page)).not.toContainText('No projects yet');
});

test('below the breakpoint the cards stack in one column, with no horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await serveProjects(page, PROJECTS);
  await page.goto('/#/');

  await expect(cards(page)).toHaveCount(3);
  const lefts = await cards(page).evaluateAll((els) => els.map((el) => Math.round(el.getBoundingClientRect().left)));
  expect(new Set(lefts).size).toBe(1);
  const overflow = await page.evaluate(() => {
    const shell = document.querySelector('kobold-lair-shell')!;
    const header = shell.shadowRoot!.querySelector('header.app-header') as HTMLElement;
    const content = shell.shadowRoot!.querySelector('.app-content') as HTMLElement;
    return {
      page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      header: header.scrollWidth - header.clientWidth,
      content: content.scrollWidth - content.clientWidth,
    };
  });
  expect(overflow).toEqual({ page: 0, header: 0, content: 0 });
});

test.describe('the ribbon views are gone', () => {
  for (const route of ['#/dashboard', '#/projects', '#/project-config', '#/hierarchy', '#/metrics', '#/compare', '#/impact']) {
    test(`${route} shows the not-found view`, async ({ page }) => {
      await page.goto(`/${route}`);
      await expect(page.locator('#route-outlet kobold-not-found-view')).toHaveCount(1);
    });
  }
});
