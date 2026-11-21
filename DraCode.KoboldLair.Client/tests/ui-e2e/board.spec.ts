import { test, expect } from './fixtures.js';
import type { Page, Request } from '@playwright/test';

// TASK-153: the workspace's Board tab — the project's tasks in four status columns, Retry on a failed card, refreshing
// on its own, and a compact list when its space is narrow. REST stubbed with page.route; the docked Dragon's socket is
// held open silently.

const board = (page: Page) => page.locator('#route-outlet kobold-board-tab');
const column = (page: Page, id: string) => board(page).locator(`.column[data-column="${id}"]`);
const card = (page: Page, taskId: string) => board(page).locator(`.card[data-task-id="${taskId}"]`);

interface Task { id: string; task: string; status: string; priority: string; assignedAgent: string | null; errorMessage: string | null }

const TASKS = (): Task[] => [
  { id: 't-1', task: '## Set up the project\nCreate the solution.', status: 'Unassigned', priority: 'Critical', assignedAgent: null, errorMessage: null },
  { id: 't-2', task: 'Write the greet module', status: 'Unassigned', priority: 'Low', assignedAgent: null, errorMessage: null },
  { id: 't-3', task: 'Build the API layer', status: 'Working', priority: 'High', assignedAgent: 'csharp', errorMessage: null },
  { id: 't-4', task: 'Add the database', status: 'NotInitialized', priority: 'Normal', assignedAgent: 'csharp', errorMessage: null },
  {
    id: 't-5', task: 'Compile the frontend', status: 'Failed', priority: 'High', assignedAgent: 'typescript',
    errorMessage: `tsc failed: ${'error TS2304: Cannot find name. '.repeat(10)}`,
  },
  { id: 't-6', task: 'Wire the frontend to the API', status: 'BlockedByFailure', priority: 'Normal', assignedAgent: null, errorMessage: null },
  { id: 't-7', task: 'Write the README', status: 'Done', priority: 'Low', assignedAgent: 'documentation', errorMessage: null },
];

/** Serve one project and a mutable task list; Retry flips a Failed task to Unassigned like the server does. */
async function serve(page: Page, tasks: Task[]) {
  const retries: Request[] = [];
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/(projects|tasks)(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/retry')) {
      retries.push(route.request());
      const id = path.split('/')[4];
      const task = tasks.find((t) => t.id === id);
      if (!task || task.status !== 'Failed') return route.fulfill({ status: 400, json: { error: `task is ${task?.status}; only Failed tasks can be retried` } });
      task.status = 'Unassigned';
      task.errorMessage = null;
      return route.fulfill({ json: { id, status: 'Unassigned' } });
    }
    if (path.endsWith('/tasks')) return route.fulfill({ json: tasks.map((t) => ({ ...t, projectId: 'p-1', featureId: null, commitFailed: false })) });
    return route.fulfill({ json: { id: 'p-1', name: 'waver', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-21T10:00:00Z' } });
  });
  return { retries };
}

test('four status columns, each card with its title, agent and priority, a failed card with the start of its error', async ({ page }) => {
  await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');

  await expect(board(page).locator('.column-head')).toHaveText(['To do 2', 'In progress 2', 'Blocked·Failed 2', 'Done 1']);
  await expect(column(page, 'todo').locator('.title')).toHaveText(['Set up the project', 'Write the greet module']); // Critical first
  await expect(column(page, 'progress').locator('.card')).toHaveCount(2);
  await expect(column(page, 'blocked').locator('.card')).toHaveCount(2);
  await expect(column(page, 'done').locator('.title')).toHaveText(['Write the README']);

  await expect(card(page, 't-3')).toContainText('csharp');
  await expect(card(page, 't-3').locator('b-badge')).toHaveText('High');
  await expect(card(page, 't-1')).toContainText('no agent yet');
  await expect(card(page, 't-6')).toContainText('Blocked by a failed task');

  const error = card(page, 't-5').locator('.error');
  await expect(error).toContainText('tsc failed: error TS2304');
  expect((await error.innerText()).endsWith('…')).toBe(true);
  expect((await error.innerText()).length).toBeLessThanOrEqual(161);
  await expect(card(page, 't-5').locator('b-button.retry')).toHaveCount(1);
  await expect(card(page, 't-6').locator('b-button.retry')).toHaveCount(0); // only Failed cards retry
});

test('Retry on a failed card resets it and the card moves to To do', async ({ page }) => {
  const { retries } = await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');

  await card(page, 't-5').locator('b-button.retry').click();
  await expect(column(page, 'todo').locator('.card[data-task-id="t-5"]')).toHaveCount(1);
  await expect(column(page, 'blocked').locator('.card[data-task-id="t-5"]')).toHaveCount(0);
  await expect(card(page, 't-5').locator('.error')).toHaveCount(0);
  expect(retries).toHaveLength(1);
  expect(retries[0].method()).toBe('POST');
  expect(retries[0].headers()['authorization']).toBe('Bearer test-token');
});

test('a refused Retry says why on the card and leaves it where it was', async ({ page }) => {
  const tasks = TASKS();
  await serve(page, tasks);
  await page.goto('/#/projects/p-1/board');
  await expect(card(page, 't-5')).toHaveCount(1);
  tasks.find((t) => t.id === 't-5')!.status = 'Working'; // the server moved on; the board has not refreshed yet

  await card(page, 't-5').locator('b-button.retry').click();
  await expect(card(page, 't-5').locator('.retry-error')).toContainText('only Failed tasks can be retried');
});

test('the board refreshes on its own as the agents move the cards', async ({ page }) => {
  const tasks = TASKS();
  await serve(page, tasks);
  await page.goto('/#/projects/p-1/board');
  await expect(column(page, 'progress').locator('.card[data-task-id="t-3"]')).toHaveCount(1);

  Object.assign(tasks.find((t) => t.id === 't-3')!, { status: 'Done' });
  Object.assign(tasks.find((t) => t.id === 't-1')!, { status: 'Working', assignedAgent: 'csharp' });

  await expect(column(page, 'done').locator('.card[data-task-id="t-3"]')).toHaveCount(1, { timeout: 12_000 });
  await expect(column(page, 'progress').locator('.card[data-task-id="t-1"]')).toHaveCount(1);
  await expect(card(page, 't-1')).toContainText('csharp');
});

test('a project with no tasks says where they come from', async ({ page }) => {
  await serve(page, []);
  await page.goto('/#/projects/p-1/board');
  await expect(board(page)).toContainText('No tasks yet');
});

test('in narrow space the board becomes a compact list', async ({ page }) => {
  await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');
  await expect(board(page).locator('.card')).toHaveCount(7);

  const lefts = () => board(page).locator('.column').evaluateAll((els) => els.map((el) => Math.round(el.getBoundingClientRect().left)));
  expect(new Set(await lefts()).size).toBe(4); // side by side

  // Dragon expanded (TASK-152) narrows the board's space; the board follows its own width, not the window's.
  await board(page).evaluate((el) => { (el as HTMLElement).style.width = '420px'; });
  await expect.poll(async () => new Set(await lefts()).size).toBe(1);
  const titleLines = await card(page, 't-1').locator('.title').evaluate((el) => getComputedStyle(el).webkitLineClamp);
  expect(titleLines).toBe('1');
});

test('below the breakpoint the board is a compact list with no horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');
  await expect(board(page).locator('.card')).toHaveCount(7);

  const layout = await page.evaluate(() => {
    const content = document.querySelector('kobold-lair-shell')!.shadowRoot!.querySelector('.app-content') as HTMLElement;
    return {
      page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      content: content.scrollWidth - content.clientWidth,
    };
  });
  expect(layout).toEqual({ page: 0, content: 0 });
  const lefts = await board(page).locator('.column').evaluateAll((els) => els.map((el) => Math.round(el.getBoundingClientRect().left)));
  expect(new Set(lefts).size).toBe(1);
});
