import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-164 (FEATURE-026 D21/D22): below 900px the app is for checking and responding. A bottom tab bar offers
// Projects, Board, Dragon and Notifications; Board and Dragon follow the last project opened. The phone workspace keeps
// Board, Spec, Runs and Stats, read only: no drag, no Retry, no Git; Settings and API keys are not offered. From 900 to
// 1279px the desktop layout stays and Dragon opens as an overlay; from 1280px it is docked. REST stubbed, the Dragon
// socket held open silently.

const PHONE = { width: 390, height: 844 };
const TABLET = { width: 1024, height: 768 };
const DESKTOP = { width: 1440, height: 900 };

const PROJECT = { id: 'p-1', name: 'waver', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-18T10:00:00Z' };
const TASKS = [
  { id: 't-1', task: 'Write the greet module', status: 'Unassigned', priority: 'Low', assignedAgent: null, errorMessage: null },
  { id: 't-2', task: 'Compile the frontend', status: 'Failed', priority: 'High', assignedAgent: 'typescript', errorMessage: 'tsc failed' },
];
const NOTIFICATIONS = () => [
  { id: 'n-1', projectId: 'p-1', projectName: 'waver', type: 'feature_branch_ready', message: 'Feature Greeting is ready to merge', metadata: {}, createdAt: new Date().toISOString(), read: false, readAt: null },
  { id: 'n-2', projectId: 'p-1', projectName: 'waver', type: 'task_failed', message: 'Compile the frontend failed', metadata: {}, createdAt: new Date().toISOString(), read: true, readAt: new Date().toISOString() },
];

async function serve(page: Page) {
  const reads: string[] = [];
  const notifications = NOTIFICATIONS();
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/(projects|notifications|stats|runs)(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/read')) {
      const id = path.split('/')[4];
      reads.push(id);
      notifications.find((n) => n.id === id)!.read = true;
      return route.fulfill({ status: 204, body: '' });
    }
    if (path.endsWith('/api/v1/notifications')) return route.fulfill({ json: notifications });
    if (path.endsWith('/api/v1/projects')) return route.fulfill({ json: [{ ...PROJECT, tasks: { total: 2, done: 0, failed: 1 } }] });
    if (path.endsWith('/tasks')) return route.fulfill({ json: TASKS });
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: 'waver', content: '# Waver', version: 1, contentHash: 'x' } });
    if (path.endsWith('/features')) return route.fulfill({ json: [] });
    if (path.includes('/stats') || path.includes('/runs')) return route.fulfill({ status: 404, body: '' });
    return route.fulfill({ json: PROJECT });
  });
  return reads;
}

const tabBar = (page: Page) => page.locator('kobold-lair-shell nav.phone-tabs');
const tabLink = (page: Page, id: string) => tabBar(page).locator(`[data-surface="${id}"]`);
const workspace = (page: Page) => page.locator('#route-outlet kobold-project-workspace');
const visibleDock = (page: Page) => page.locator('#route-outlet kobold-dragon-dock:visible');

test.describe('on a phone', () => {
  test.use({ viewport: PHONE });

  test('a bottom tab bar offers Projects, Board, Dragon and Notifications; Board waits for a project', async ({ page }) => {
    await serve(page);
    await page.goto('/#/');

    await expect(tabBar(page)).toBeVisible();
    await expect(tabBar(page).locator('[data-surface]')).toHaveText(['Projects', 'Board', 'Dragon', /^Notifications/]);
    await expect(tabLink(page, 'projects')).toHaveAttribute('aria-current', 'page');
    await expect(tabLink(page, 'board')).toHaveAttribute('aria-disabled', 'true');
    // Dragon and the notifications are tabs of their own: neither the home Dragon nor the header bell is shown
    await expect(page.locator('#route-outlet kobold-home-view .project-card')).toHaveCount(1);
    await expect(visibleDock(page)).toHaveCount(0);
    await expect(page.locator('kobold-lair-shell kobold-notification-bell')).toBeHidden();
    await expect(tabLink(page, 'notifications').locator('.count')).toHaveText('1');
  });

  test('the workspace keeps Board, Spec, Runs and Stats, the board read only, and Board follows the project', async ({ page }) => {
    await serve(page);
    await page.goto('/#/projects/p-1/board');

    const tabs = workspace(page).locator('b-tabs');
    await expect.poll(() => tabs.locator('[role="tab"]').allTextContents()).toEqual(['Board', 'Spec', 'Runs', 'Stats']);
    const kanban = workspace(page).locator('kobold-board-tab b-kanban');
    await expect(kanban.locator('.card')).toHaveCount(2);
    expect(new Set(await kanban.locator('.card').evaluateAll((els) => els.map((el) => el.getAttribute('draggable'))))).toEqual(new Set(['false']));
    await expect(kanban.locator('b-button.retry')).toHaveCount(0);
    await expect(workspace(page).locator('kobold-board-tab .read-only-note')).toBeVisible();
    await expect(visibleDock(page)).toHaveCount(0);

    await expect(tabLink(page, 'board')).toHaveAttribute('aria-current', 'page');
    await page.goto('/#/');
    await expect(tabLink(page, 'board')).toHaveAttribute('href', '#/projects/p-1/board');
  });

  test('Git is not reachable: a link to it says it is on a computer', async ({ page }) => {
    await serve(page);
    await page.goto('/#/projects/p-1/git');
    await expect(workspace(page).locator('.desktop-only')).toContainText('on a computer');
    await expect(workspace(page).locator('kobold-git-tab')).toHaveCount(0);
  });

  test('Dragon\'s tab shows the last project\'s Dragon full screen, or the home Dragon before any project', async ({ page }) => {
    await serve(page);
    await page.goto('/#/');
    await tabLink(page, 'dragon').click();
    await expect(page).toHaveURL(/#\/dragon$/);
    await expect(visibleDock(page)).toHaveCount(1);
    expect(await visibleDock(page).getAttribute('project-id')).toBeNull();

    await page.goto('/#/projects/p-1/board');
    await expect(workspace(page).locator('.project-title')).toHaveText('waver');
    await tabLink(page, 'dragon').click();
    await expect(visibleDock(page)).toHaveAttribute('project-id', 'p-1');
    await expect(tabLink(page, 'dragon')).toHaveAttribute('aria-current', 'page');
    const box = (await visibleDock(page).boundingBox())!;
    expect(box.width).toBeGreaterThan(PHONE.width - 40);
    expect(box.height).toBeGreaterThan(500);
  });

  test('the Notifications tab lists them, unread first; opening one marks it read and opens its project', async ({ page }) => {
    const reads = await serve(page);
    await page.goto('/#/');
    await tabLink(page, 'notifications').click();
    await expect(page).toHaveURL(/#\/notifications$/);

    const rows = page.locator('#route-outlet kobold-notifications-page .notification');
    await expect(rows).toHaveCount(2);
    await expect(rows.first()).toContainText('Feature Greeting is ready to merge');
    await expect(rows.first()).toHaveClass(/unread/);

    await rows.first().click();
    await expect(page).toHaveURL(/#\/projects\/p-1\/board$/);
    expect(reads).toEqual(['n-1']);
  });

  test('Settings and API keys are not offered', async ({ page }) => {
    await serve(page);
    await page.goto('/#/');
    const items = await page.locator('kobold-lair-shell').evaluate((shell: any) => shell.getUserMenuItems().map((i: { id: string }) => i.id));
    expect(items).not.toContain('settings');

    await page.goto('/#/settings/keys');
    await expect(page.locator('#route-outlet kobold-settings-view .desktop-only')).toContainText('on a computer');
    await expect(page.locator('#route-outlet kobold-settings-view b-tabs')).toHaveCount(0);
  });

  // TASK-199: Birko's focus ring is a line and a glow. Drawn with `inset var(--b-focus-ring)` only the line was inside
  // the tab and the glow spilled onto its neighbours; --b-focus-ring-inset (Birko TASK-986) puts both inside.
  test('a tab focused from the keyboard draws its whole focus ring inside itself', async ({ page }) => {
    await serve(page);
    await page.goto('/#/');
    await expect(tabBar(page)).toBeVisible();

    for (const id of ['projects', 'dragon']) {
      await tabLink(page, id).focus();
      await page.keyboard.press('Shift+Tab');
      await page.keyboard.press('Tab');
      await expect(tabLink(page, id)).toBeFocused();
      const shadows = await tabLink(page, id).evaluate((el) =>
        getComputedStyle(el).boxShadow.split(/,(?![^(]*\))/).map((s) => s.trim()));
      expect(shadows.length, `${id}: ${shadows.join(' | ')}`).toBeGreaterThan(1);
      for (const shadow of shadows) expect(shadow, id).toContain('inset');
    }
  });
});

test.describe('on a tablet', () => {
  test.use({ viewport: TABLET });

  test('the desktop layout stays and Dragon opens as an overlay over it, and closes again', async ({ page }) => {
    await serve(page);
    await page.goto('/#/projects/p-1/board');
    await expect(workspace(page).locator('.project-title')).toHaveText('waver');

    await expect(tabBar(page)).toBeHidden();
    await expect.poll(() => workspace(page).locator('b-tabs [role="tab"]').allTextContents()).toContain('Git');
    await expect(visibleDock(page)).toHaveCount(0);

    await workspace(page).locator('.dragon-toggle').click();
    await expect(visibleDock(page)).toHaveCount(1);
    const box = (await visibleDock(page).boundingBox())!;
    expect(box.x).toBeGreaterThan(TABLET.width / 3); // over the right of the page, not docked in a column
    await expect(workspace(page).locator('kobold-board-tab')).toBeVisible(); // the board stays under it

    await workspace(page).locator('.dock-close').click();
    await expect(visibleDock(page)).toHaveCount(0);

    await workspace(page).locator('.dragon-toggle').click();
    await expect(visibleDock(page)).toHaveCount(1);
    await page.keyboard.press('Escape');
    await expect(visibleDock(page)).toHaveCount(0);
  });

  test('the homepage\'s Dragon opens the same way', async ({ page }) => {
    await serve(page);
    await page.goto('/#/');
    await expect(page.locator('#route-outlet kobold-home-view .project-card')).toHaveCount(1);
    await expect(visibleDock(page)).toHaveCount(0);
    await page.locator('#route-outlet kobold-home-view .dragon-toggle').click();
    await expect(visibleDock(page)).toHaveCount(1);
  });
});

test.describe('on a desktop', () => {
  test.use({ viewport: DESKTOP });

  test('Dragon stays docked, with no tab bar and no overlay button', async ({ page }) => {
    await serve(page);
    await page.goto('/#/projects/p-1/board');
    await expect(workspace(page).locator('.project-title')).toHaveText('waver');
    await expect(visibleDock(page)).toHaveCount(1);
    await expect(workspace(page).locator('.dragon-toggle')).toBeHidden();
    await expect(tabBar(page)).toBeHidden();
  });
});
