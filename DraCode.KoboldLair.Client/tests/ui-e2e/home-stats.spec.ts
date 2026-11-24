import { test, expect } from './fixtures.js';
import type { Page, Route } from '@playwright/test';

// TASK-148: the homepage's numbers across the caller's projects, from GET /api/v1/stats — b-stat tiles (active projects,
// tasks done this week, success rate, spend this month) and two b-chart charts (cost over the last 30 days, tasks by
// status per project). No data reads as zeros and flat charts, never an error. REST stubbed; the docked Dragon's socket
// held open.

const PROJECTS = [
  { id: 'p-1', name: 'shop', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-18T10:00:00Z', tasks: { total: 4, done: 2, inProgress: 1, failed: 1 } },
  { id: 'p-2', name: 'blog', ownerId: 'u-1', status: 'Completed', executionState: 'Running', createdAt: '2026-11-19T10:00:00Z', tasks: { total: 2, done: 1, inProgress: 0, failed: 0 } },
];

const days = (cost: (i: number) => number) => Array.from({ length: 30 }, (_, i) => {
  const date = new Date(Date.UTC(2026, 10, 24) - (29 - i) * 86_400_000).toISOString().slice(0, 10);
  const usd = cost(i);
  return { date, requests: usd > 0 ? 1 : 0, tokens: usd > 0 ? 1000 : 0, costUsd: usd };
});

const STATUS = (s: Partial<Record<string, number>>) => ({ Unassigned: 0, NotInitialized: 0, Working: 0, Done: 0, Failed: 0, BlockedByFailure: 0, AwaitingHumanDecision: 0, ...s });

const FULL = {
  projectIds: ['p-1', 'p-2'],
  projects: { total: 2, active: 1 },
  tasks: { total: 6, byStatus: STATUS({ Unassigned: 1, Working: 1, Done: 3, Failed: 1 }), successRate: 75, doneThisWeek: 3, failedThisWeek: 1 },
  cost: { thisMonthUsd: 41.5, thisMonthTokens: 120000, thisMonthRequests: 30, monthlyBudgetUsd: 500, byDay: days((i) => (i % 3 === 0 ? 1.25 : 0)) },
  byProject: [
    { projectId: 'p-1', name: 'shop', total: 4, byStatus: STATUS({ Unassigned: 1, Working: 1, Done: 1, Failed: 1 }) },
    { projectId: 'p-2', name: 'blog', total: 2, byStatus: STATUS({ Done: 2 }) },
  ],
};
const EMPTY = {
  projectIds: ['p-1'],
  projects: { total: 1, active: 1 },
  tasks: { total: 0, byStatus: STATUS({}), successRate: null, doneThisWeek: 0, failedThisWeek: 0 },
  cost: { thisMonthUsd: 0, thisMonthTokens: 0, thisMonthRequests: 0, monthlyBudgetUsd: null, byDay: days(() => 0) },
  byProject: [{ projectId: 'p-1', name: 'shop', total: 0, byStatus: STATUS({}) }],
};

async function serve(page: Page, stats: { status?: number; body: object }, projects: unknown[] = PROJECTS): Promise<string[]> {
  const asked: string[] = [];
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route('**/api/v1/projects**', (route: Route) => route.fulfill({ json: projects }));
  await page.route('**/api/v1/needs-you**', (route: Route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/notifications**', (route: Route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/stats**', (route: Route) => {
    asked.push(route.request().url());
    return route.fulfill({ status: stats.status ?? 200, json: stats.body });
  });
  return asked;
}

const home = (page: Page) => page.locator('#route-outlet kobold-home-view');
const tile = (page: Page, name: string) => home(page).locator(`.overview b-stat.${name}`);
const chart = (page: Page, name: string) => home(page).locator(`.overview b-chart.${name}-chart`);
const bars = (page: Page, name: string) => chart(page, name).evaluate((c) => c.shadowRoot?.querySelectorAll('rect, path, polyline').length ?? 0);

test('the homepage shows the tiles and the two charts over the caller\'s projects', async ({ page }) => {
  const asked = await serve(page, { body: FULL });
  await page.goto('/#/');

  await expect(tile(page, 'active')).toHaveAttribute('value', '1');
  await expect(tile(page, 'active')).toHaveAttribute('delta', 'of 2');
  await expect(tile(page, 'done-week')).toHaveAttribute('value', '3');
  await expect(tile(page, 'done-week')).toHaveAttribute('delta', '1 failed');
  await expect(tile(page, 'success')).toHaveAttribute('value', '75%');
  await expect(tile(page, 'spend')).toHaveAttribute('value', '$41.50');
  await expect(tile(page, 'spend')).toHaveAttribute('delta', 'of $500 budget');
  expect(new URL(asked[0]).searchParams.get('scope')).toBe('all'); // as the cards ask: an admin sees every project

  for (const name of ['by-day', 'by-project']) {
    await expect(chart(page, name)).toHaveCount(1);
    await expect.poll(() => bars(page, name), { message: name }).toBeGreaterThan(0);
  }
  await expect(home(page).locator('.overview .by-project h3')).toHaveText('Tasks by status per project');
  await expect(home(page).locator('.overview .note')).toHaveCount(0);
  await expect(home(page).locator('.project-card')).toHaveCount(2); // the cards are still there
});

test('with nothing yet the tiles read zero and the charts are empty, not errors', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await serve(page, { body: EMPTY }, [PROJECTS[0]]);
  await page.goto('/#/');

  await expect(tile(page, 'active')).toHaveAttribute('value', '1');
  await expect(tile(page, 'done-week')).toHaveAttribute('value', '0');
  await expect(tile(page, 'success')).toHaveAttribute('value', '—');
  await expect(tile(page, 'spend')).toHaveAttribute('value', '$0');
  await expect(chart(page, 'by-day')).toHaveCount(1);
  await expect(chart(page, 'by-project')).toHaveCount(1);
  await expect(home(page).locator('.overview .by-day .note')).toHaveText('No LLM usage in the last 30 days.');
  await expect(home(page).locator('.overview .by-project .note')).toHaveText('No tasks yet.');
  expect(errors).toEqual([]);
});

test('calls recorded at no cost chart tokens and point to the pricing setting', async ({ page }) => {
  await serve(page, { body: { ...FULL, cost: { ...FULL.cost, thisMonthUsd: 0, byDay: FULL.cost.byDay.map((d) => ({ ...d, costUsd: 0 })) } } });
  await page.goto('/#/');
  await expect(home(page).locator('.overview .by-day h3')).toHaveText('Tokens, last 30 days');
  await expect(home(page).locator('.overview .by-day .note')).toContainText('prices are set in CostTracking.Pricing');
});

test('numbers that fail to load leave the projects in place', async ({ page }) => {
  await serve(page, { status: 500, body: {} });
  await page.goto('/#/');
  await expect(home(page).locator('.project-card')).toHaveCount(2);
  await expect(home(page).locator('.overview')).toContainText('Could not load the numbers');
});

test('below the breakpoint the homepage numbers fit without horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await serve(page, { body: FULL });
  await page.goto('/#/');
  await expect(chart(page, 'by-project')).toHaveCount(1);
  await page.waitForTimeout(300);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
});
