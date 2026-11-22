import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-146: inside a project the header shows its name as a searchable picker of the caller's projects, grouped under
// their status; choosing one opens it on the same tab; on the homepage there is no picker. REST stubbed; the docked
// Dragon's socket is held open silently.

const PROJECTS = [
  { id: 'p-1', name: 'Shop', status: 'InProgress' },
  { id: 'p-2', name: 'Blog', status: 'New' },
  { id: 'p-3', name: 'Príťahy tracker', status: 'InProgress' },
  { id: 'p-4', name: 'Wiki', status: 'Completed' },
];

const picker = (page: Page) => page.locator('kobold-lair-shell kobold-project-picker');
const box = (page: Page) => picker(page).locator('b-select input.combo-input');
const options = (page: Page) => picker(page).locator('b-select [role="option"][data-value]');
const workspaceTitle = (page: Page) => page.locator('#route-outlet kobold-project-workspace .project-title');

async function serve(page: Page): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/(projects|tasks)(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    const full = (p: typeof PROJECTS[number]) => ({ ...p, ownerId: 'u-1', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z', tasks: null });
    if (path === '/api/v1/projects') return route.fulfill({ json: PROJECTS.map(full) });
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: 'x', content: '# Spec', version: 1 } });
    if (path.endsWith('/features') || path.endsWith('/tasks')) return route.fulfill({ json: [] });
    const project = PROJECTS.find((p) => p.id === path.split('/')[4]);
    return project ? route.fulfill({ json: full(project) }) : route.fulfill({ status: 404, body: '' });
  });
}

test('inside a project the header shows its name as a picker of the projects, grouped under their status', async ({ page }) => {
  await serve(page);
  await page.goto('/#/projects/p-1/spec');
  await expect(workspaceTitle(page)).toHaveText('Shop');
  await expect(box(page)).toHaveValue('Shop');

  await box(page).click();
  await expect(options(page)).toHaveText(['Wiki', 'Príťahy tracker', 'Shop', 'Blog']);
  for (const status of ['Completed', 'InProgress', 'New']) {
    await expect(picker(page).locator('b-select [role="group"]').filter({ hasText: status })).toHaveCount(1);
  }
  await expect(picker(page).locator('b-select [role="group"]').filter({ hasText: 'InProgress' }).locator('[role="option"]'))
    .toHaveText(['Príťahy tracker', 'Shop']);
});

test('typing filters the list, without caring for accents; choosing a project opens it on the same tab', async ({ page }) => {
  await serve(page);
  await page.goto('/#/projects/p-1/spec');
  await expect(box(page)).toHaveValue('Shop');

  await box(page).click();
  await box(page).fill('pritahy');
  await expect(options(page)).toHaveText(['Príťahy tracker']);
  await box(page).fill('blo');
  await expect(options(page)).toHaveText(['Blog']);
  await options(page).first().click();

  await expect(page).toHaveURL(/#\/projects\/p-2\/spec$/);
  await expect(workspaceTitle(page)).toHaveText('Blog');
  await expect(box(page)).toHaveValue('Blog');
});

test('the tab the user is on is kept: from the Board to the Board', async ({ page }) => {
  await serve(page);
  await page.goto('/#/projects/p-2/board');
  await expect(box(page)).toHaveValue('Blog');
  await box(page).click();
  await options(page).filter({ hasText: 'Wiki' }).click();
  await expect(page).toHaveURL(/#\/projects\/p-4\/board$/);
  await expect(page.locator('#route-outlet kobold-board-tab')).toHaveCount(1);
});

test('Escape leaves the current project as it was', async ({ page }) => {
  await serve(page);
  await page.goto('/#/projects/p-1/spec');
  await box(page).click();
  await box(page).fill('wik');
  await box(page).press('Escape');
  await expect(box(page)).toHaveValue('Shop');
  await expect(page).toHaveURL(/#\/projects\/p-1\/spec$/);
});

test('on the homepage there is no picker, and it appears once a project is opened', async ({ page }) => {
  await serve(page);
  await page.goto('/#/');
  await expect(page.locator('#route-outlet kobold-home-view .project-card')).toHaveCount(4);
  await expect(picker(page).locator('b-select')).toHaveCount(0);

  await page.locator('#route-outlet kobold-home-view .project-card').filter({ hasText: 'Shop' }).click();
  await expect(box(page)).toHaveValue('Shop');
  await page.goBack();
  await expect(picker(page).locator('b-select')).toHaveCount(0);
});

test('below the breakpoint the header with the picker has no horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await serve(page);
  await page.goto('/#/projects/p-3/spec');
  await expect(box(page)).toHaveValue('Príťahy tracker');
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBe(0);
  const pickerBox = await picker(page).boundingBox();
  expect(pickerBox!.x + pickerBox!.width).toBeLessThanOrEqual(375);
  expect(pickerBox!.width).toBeGreaterThan(80); // still wide enough to read a name
});
