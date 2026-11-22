import { test, expect } from './fixtures.js';
import type { Page, Route } from '@playwright/test';

// TASK-158: the workspace's Stats tab — b-stat tiles (tasks, done, success rate, average task, spend, tokens), spend
// against the project budget, and b-chart charts of tasks by status and agent type and cost by day and model; a project
// with nothing yet renders cleanly. REST stubbed; the docked Dragon's socket held open.

const FULL = {
  projectIds: ['p-1'],
  tasks: {
    total: 5,
    byStatus: { Unassigned: 1, NotInitialized: 0, Working: 0, Done: 3, Failed: 1, BlockedByFailure: 0, AwaitingHumanDecision: 0 },
    byAgent: [{ agentType: 'csharp', total: 2, done: 2, failed: 0 }, { agentType: 'typescript', total: 2, done: 1, failed: 1 }, { agentType: 'unassigned', total: 1, done: 0, failed: 0 }],
    successRate: 75, averageDurationSeconds: 120, timedTasks: 2,
  },
  cost: {
    totalUsd: 1, totalTokens: 6000, requests: 3,
    byModel: [{ provider: 'claude', model: 'sonnet', requests: 1, tokens: 2000, costUsd: 0.6 }, { provider: 'openai', model: 'gpt-4o', requests: 2, tokens: 4000, costUsd: 0.4 }],
    byDay: [{ date: '2026-11-20', requests: 2, tokens: 4000, costUsd: 0.4 }, { date: '2026-11-21', requests: 1, tokens: 2000, costUsd: 0.6 }],
  },
  budget: { projectBudgetUsd: 2, spentUsd: 1, percentUsed: 50, warningThresholdPercent: 80 },
};
const EMPTY = {
  projectIds: ['p-1'],
  tasks: { total: 0, byStatus: { Unassigned: 0, Done: 0, Failed: 0 }, byAgent: [], successRate: null, averageDurationSeconds: null, timedTasks: 0 },
  cost: { totalUsd: 0, totalTokens: 0, requests: 0, byModel: [], byDay: [] },
  budget: { projectBudgetUsd: null, spentUsd: 0, percentUsed: null, warningThresholdPercent: 80 },
};

async function serve(page: Page, stats: { body: object }): Promise<{ requests: () => number }> {
  let requests = 0;
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/(projects|tasks)(\/|\?|$)/, (route: Route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/stats')) { requests++; return route.fulfill({ json: stats.body }); }
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  });
  return { requests: () => requests };
}

const tab = (page: Page) => page.locator('#route-outlet kobold-stats-tab');
const tile = (page: Page, name: string) => tab(page).locator(`b-stat.${name}`);

test('the Stats tab shows the tiles, the budget and the four charts', async ({ page }) => {
  await serve(page, { body: FULL });
  await page.goto('/#/projects/p-1/stats');

  await expect(tile(page, 'tasks')).toHaveAttribute('value', '5');
  await expect(tile(page, 'done')).toHaveAttribute('value', '3');
  await expect(tile(page, 'success')).toHaveAttribute('value', '75%');
  await expect(tile(page, 'success')).toHaveAttribute('delta', '4 finished');
  await expect(tile(page, 'duration')).toHaveAttribute('value', '2 min 0 s');
  await expect(tile(page, 'spend')).toHaveAttribute('value', '$1.00');
  await expect(tile(page, 'tokens')).toHaveAttribute('value', '6,000');
  await expect(tile(page, 'tokens')).toHaveAttribute('delta', '3 calls');
  await expect(tab(page).locator('.budget-text')).toHaveText('$1.00 of $2.00 (50%)');
  await expect(tab(page).locator('.budget b-progress')).toHaveAttribute('variant', 'success');

  for (const chart of ['by-status', 'by-agent', 'by-day', 'by-model']) {
    const el = tab(page).locator(`b-chart.${chart}-chart`);
    await expect(el).toHaveCount(1);
    await expect.poll(() => el.evaluate((c) => c.shadowRoot?.querySelectorAll('rect').length ?? 0), { message: chart }).toBeGreaterThan(0);
  }
  await expect(tab(page).locator('.panel .empty')).toHaveCount(0);
});

test('a project with nothing yet renders dashes and says so in place of the charts', async ({ page }) => {
  await serve(page, { body: EMPTY });
  await page.goto('/#/projects/p-1/stats');

  await expect(tile(page, 'tasks')).toHaveAttribute('value', '0');
  await expect(tile(page, 'success')).toHaveAttribute('value', '—');
  await expect(tile(page, 'duration')).toHaveAttribute('value', '—');
  await expect(tile(page, 'spend')).toHaveAttribute('value', '$0');
  await expect(tab(page).locator('b-chart')).toHaveCount(0);
  await expect(tab(page).locator('.panel .empty')).toHaveText(['No tasks yet.', 'No tasks yet.', 'No LLM usage recorded yet.', 'No LLM usage recorded yet.']);
  await expect(tab(page).locator('.budget')).toContainText('No project budget is set');
});

test('calls recorded at no cost point to the pricing setting; a budget near its end warns', async ({ page }) => {
  await serve(page, { body: { ...FULL, cost: { ...FULL.cost, totalUsd: 0, byDay: FULL.cost.byDay.map((d) => ({ ...d, costUsd: 0 })) },
    budget: { projectBudgetUsd: 1, spentUsd: 0.9, percentUsed: 90, warningThresholdPercent: 80 } } });
  await page.goto('/#/projects/p-1/stats');
  await expect(tab(page).locator('.by-day .note')).toContainText('cost $0: prices are set in CostTracking.Pricing');
  await expect(tab(page).locator('.by-day h3')).toHaveText('Tokens by day'); // a flat $0 chart says nothing; tokens do
  await expect(tab(page).locator('.by-model h3')).toHaveText('Tokens by provider and model');
  await expect(tab(page).locator('.budget b-progress')).toHaveAttribute('variant', 'warning');
});

test('Refresh reads the numbers again', async ({ page }) => {
  const stats = { body: EMPTY as object };
  const server = await serve(page, stats);
  await page.goto('/#/projects/p-1/stats');
  await expect(tile(page, 'tasks')).toHaveAttribute('value', '0');
  stats.body = FULL;
  await tab(page).locator('b-button.refresh').click();
  await expect(tile(page, 'tasks')).toHaveAttribute('value', '5');
  expect(server.requests()).toBe(2);
});

test('below the breakpoint the tab fits without horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await serve(page, { body: FULL });
  await page.goto('/#/projects/p-1/stats');
  await expect(tab(page).locator('b-chart')).toHaveCount(4);
  await page.waitForTimeout(300);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
});
