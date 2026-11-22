import { test, expect } from './fixtures.js';
import type { Page, Request } from '@playwright/test';

// TASK-155: the workspace's Runs tab — this project's runs and the user's ad-hoc runs (marked), newest first, with mode,
// status, start time and step progress; refreshing on its own. TASK-184: the pipeline's own Kobold runs, told apart.
// REST stubbed; the docked Dragon's socket held open.

const minutesAgo = (m: number) => new Date(Date.now() - m * 60000).toISOString();
interface Run { runId: string; mode: string; projectId: string | null; taskId: string | null; prompt: string | null; status: string;
  startedAt: string; completedAt: string | null; completedSteps: number; totalSteps: number; summary: string | null }
const RUNS = (): Run[] => [
  { runId: 'r-3', mode: 'adhoc', projectId: null, taskId: null, prompt: 'Rename the README', status: 'running', startedAt: minutesAgo(2),
    completedAt: null, completedSteps: 1, totalSteps: 4, summary: null },
  { runId: 'r-2', mode: 'project', projectId: 'p-1', taskId: 't-1', prompt: null, status: 'failed', startedAt: minutesAgo(30),
    completedAt: minutesAgo(25), completedSteps: 2, totalSteps: 5, summary: 'tsc failed: Cannot find name' },
  { runId: 'r-1', mode: 'project', projectId: 'p-1', taskId: 't-gone', prompt: null, status: 'completed', startedAt: minutesAgo(90),
    completedAt: minutesAgo(80), completedSteps: 3, totalSteps: 3, summary: 'Done' },
];

async function serve(page: Page, runs: { list: Run[] }): Promise<Request[]> {
  const runRequests: Request[] = [];
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/runs(\/|\?|$)/, (route) => { runRequests.push(route.request()); return route.fulfill({ json: runs.list }); });
  await page.route(/\/api\/v1\/(projects|tasks)(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/tasks')) {
      return route.fulfill({ json: [{ id: 't-1', task: '## Build the API\nDetails', projectId: 'p-1', status: 'Failed', priority: 'High',
        assignedAgent: 'csharp', featureId: null, errorMessage: 'x', commitFailed: false }] });
    }
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  });
  return runRequests;
}

const tab = (page: Page) => page.locator('#route-outlet kobold-runs-tab');
const run = (page: Page, id: string) => tab(page).locator(`.run[data-run-id="${id}"]`);

test('the Runs tab lists the project\'s runs and the ad-hoc ones, newest first, with mode, status, start and steps', async ({ page }) => {
  const requests = await serve(page, { list: RUNS() });
  await page.goto('/#/projects/p-1/runs');

  await expect(tab(page).locator('.run')).toHaveCount(3);
  await expect(tab(page).locator('.what')).toHaveText(['Rename the README', 'Build the API', 'Task t-gone']);
  await expect(run(page, 'r-3').locator('b-badge.adhoc')).toHaveText('Ad-hoc');
  await expect(run(page, 'r-2').locator('b-badge.mode')).toHaveText('Started by hand');
  await expect(run(page, 'r-2').locator('b-badge.adhoc')).toHaveCount(0);
  await expect(tab(page).locator('.status')).toHaveText(['Running', 'Failed', 'Completed']);
  await expect(run(page, 'r-3').locator('.started')).toHaveText('started 2 min ago');
  await expect(run(page, 'r-3').locator('.steps')).toContainText('1 of 4 steps');
  await expect(run(page, 'r-2').locator('.summary')).toHaveText('tsc failed: Cannot find name');
  await expect(run(page, 'r-1').locator('.summary')).toHaveCount(0); // only a failed run says why

  const query = new URL(requests[0].url()).searchParams;
  expect(query.get('projectId')).toBe('p-1');
  expect(query.get('adhoc')).toBe('true');
});

test('the pipeline\'s own Kobold runs are listed, named by their task and marked as the pipeline\'s', async ({ page }) => {
  await serve(page, { list: [
    { runId: 'r-9', mode: 'pipeline', projectId: 'p-1', taskId: 't-1', prompt: null, status: 'running', startedAt: minutesAgo(1),
      completedAt: null, completedSteps: 2, totalSteps: 6, summary: null },
    ...RUNS(),
  ] });
  await page.goto('/#/projects/p-1/runs');
  await expect(run(page, 'r-9').locator('.what')).toHaveText('Build the API');
  await expect(run(page, 'r-9').locator('b-badge.mode')).toHaveText('Pipeline');
  await expect(run(page, 'r-9').locator('b-badge.adhoc')).toHaveCount(0);
  await expect(run(page, 'r-9').locator('.steps')).toContainText('2 of 6 steps');
  await expect(tab(page).locator('b-badge.mode')).toHaveText(['Pipeline', 'Started by hand', 'Started by hand']);
});

test('the Runs tab is in the workspace\'s tabs, after Spec', async ({ page }) => {
  await serve(page, { list: [] });
  await page.goto('/#/projects/p-1/board');
  const tabs = page.locator('#route-outlet kobold-project-workspace b-tabs [role="tab"]');
  await expect(tabs).toHaveText(['Board', 'Spec', 'Runs', 'Git']);
  await tabs.filter({ hasText: 'Runs' }).click();
  await expect(page).toHaveURL(/#\/projects\/p-1\/runs$/);
  await expect(tab(page)).toContainText('No runs yet');
});

test('the list refreshes on its own as a run moves on', async ({ page }) => {
  const runs = { list: RUNS() };
  await serve(page, runs);
  await page.goto('/#/projects/p-1/runs');
  await expect(run(page, 'r-3').locator('.status')).toHaveText('Running');

  runs.list = runs.list.map((r) => (r.runId === 'r-3' ? { ...r, status: 'completed', completedSteps: 4 } : r));
  await expect(run(page, 'r-3').locator('.status')).toHaveText('Completed', { timeout: 12_000 });
  await expect(run(page, 'r-3').locator('.steps')).toContainText('4 of 4 steps');
});

test('run text is shown as text', async ({ page }) => {
  await serve(page, { list: [{ ...RUNS()[0], prompt: '<img src=x onerror="window.__pwned=1">' }] });
  await page.goto('/#/projects/p-1/runs');
  await expect(tab(page).locator('.what')).toHaveText('<img src=x onerror="window.__pwned=1">');
  await expect(tab(page).locator('img')).toHaveCount(0);
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
});

test('below the breakpoint the runs fit without horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await serve(page, { list: [{ ...RUNS()[0], prompt: 'A very long ad-hoc request that goes on and on about renaming every file in the repository' }, ...RUNS().slice(1)] });
  await page.goto('/#/projects/p-1/runs');
  await expect(tab(page).locator('.run')).toHaveCount(3);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
});
