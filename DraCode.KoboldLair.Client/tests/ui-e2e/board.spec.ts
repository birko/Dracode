import { test, expect } from './fixtures.js';
import type { Page, Request } from '@playwright/test';

// TASK-153: the workspace's Board tab — the project's tasks in four status columns, Retry on a failed card, refreshing
// on its own, and stacked compact columns when its space is narrow. TASK-183: drawn by Birko's b-kanban, the agents'
// columns locked. TASK-154: To do's priority lanes, a move between them sets the priority. REST stubbed with page.route;
// the docked Dragon's socket is held open silently.

const board = (page: Page) => page.locator('#route-outlet kobold-board-tab');
const kanban = (page: Page) => board(page).locator('b-kanban');
const column = (page: Page, id: string) => kanban(page).locator(`.column[data-column-id="${id}"]`);
const card = (page: Page, taskId: string) => kanban(page).locator(`.card[data-card-id="${taskId}"]`);
/** Each column's "title count", e.g. "To do 2". */
const heads = (page: Page) => kanban(page).locator('.column-header').evaluateAll((els) =>
  els.map((el) => `${el.querySelector('.column-title')!.textContent} ${el.querySelector('.column-count')!.textContent}`));

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

/**
 * Serve one project and a mutable task list; Retry flips a Failed task to Unassigned and a priority call sets the
 * priority, like the server does. `refusePriority` makes the priority call fail with that error.
 */
async function serve(page: Page, tasks: Task[], options: { refusePriority?: string } = {}) {
  const retries: Request[] = [];
  const priorities: { id: string; body: unknown }[] = [];
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
    if (path.endsWith('/priority')) {
      const id = path.split('/')[4];
      const body = route.request().postDataJSON() as { priority: string };
      priorities.push({ id, body });
      if (options.refusePriority) return route.fulfill({ status: 400, json: { error: options.refusePriority } });
      const task = tasks.find((t) => t.id === id)!;
      task.priority = body.priority[0].toUpperCase() + body.priority.slice(1);
      return route.fulfill({ json: { id, priority: task.priority } });
    }
    if (path.endsWith('/tasks')) return route.fulfill({ json: tasks.map((t) => ({ ...t, projectId: 'p-1', featureId: null, commitFailed: false })) });
    return route.fulfill({ json: { id: 'p-1', name: 'waver', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-21T10:00:00Z' } });
  });
  return { retries, priorities };
}

const lane = (page: Page, id: string) => column(page, 'todo').locator(`.lane[data-lane-id="${id}"]`);
const laneOfCard = (page: Page, taskId: string) =>
  card(page, taskId).evaluate((el) => el.closest('.lane')?.getAttribute('data-lane-id') ?? null);

test('four status columns, each card with its title, agent and priority, a failed card with the start of its error', async ({ page }) => {
  await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');

  await expect.poll(() => heads(page)).toEqual(['To do 2', 'In progress 2', 'Blocked·Failed 2', 'Done 1']);
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

test('the agents move the cards between columns: only To do takes a person\'s moves, and only between its lanes', async ({ page }) => {
  const { priorities } = await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');
  await expect(kanban(page).locator('.card')).toHaveCount(7);

  await expect(kanban(page).locator('.column.locked')).toHaveCount(3);
  await expect(column(page, 'todo')).not.toHaveClass(/locked/);
  for (const id of ['progress', 'blocked', 'done']) {
    expect(new Set(await column(page, id).locator('.card').evaluateAll((els) => els.map((el) => el.getAttribute('draggable'))))).toEqual(new Set(['false']));
  }

  // A drag out of To do, or out of a locked column, leaves the card where it was and changes nothing.
  await card(page, 't-1').dragTo(column(page, 'done'));
  await card(page, 't-3').dragTo(lane(page, 'low'));
  await expect(column(page, 'todo').locator('.card[data-card-id="t-1"]')).toHaveCount(1);
  await expect(column(page, 'progress').locator('.card[data-card-id="t-3"]')).toHaveCount(1);
  await expect(column(page, 'done').locator('.card')).toHaveCount(1);
  expect(priorities).toEqual([]);
});

test('To do is split into priority lanes; dragging a card to another lane sets its priority and it stays there', async ({ page }) => {
  const tasks = TASKS();
  const { priorities } = await serve(page, tasks);
  await page.goto('/#/projects/p-1/board');
  await expect(kanban(page).locator('.card')).toHaveCount(7);

  await expect(column(page, 'todo').locator('.lane-title')).toHaveText(['Critical', 'High', 'Normal', 'Low']);
  expect(await laneOfCard(page, 't-1')).toBe('critical');
  expect(await laneOfCard(page, 't-2')).toBe('low');
  await expect(lane(page, 'high').locator('.card')).toHaveCount(0); // an empty lane stays, to drop on

  await card(page, 't-2').dragTo(lane(page, 'critical').locator('.column-body'));

  await expect.poll(() => laneOfCard(page, 't-2')).toBe('critical');
  expect(priorities).toEqual([{ id: 't-2', body: { priority: 'critical' } }]);
  await expect(card(page, 't-2').locator('b-badge')).toHaveText('Critical');
  // Still there after the board refreshes from the server.
  await page.waitForTimeout(6000);
  expect(await laneOfCard(page, 't-2')).toBe('critical');
  await expect(board(page).locator('.move-error')).toBeHidden();
});

test('a keyboard user moves a card to another lane: Space picks it up, an arrow moves it, Enter drops it', async ({ page }) => {
  const { priorities } = await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');
  await expect(kanban(page).locator('.card')).toHaveCount(7);

  await card(page, 't-2').focus();
  await page.keyboard.press('Space');
  await page.keyboard.press('ArrowUp'); // Low → Normal: To do keeps no order inside a lane, so the arrows go lane to lane
  await page.keyboard.press('Enter');

  await expect.poll(() => laneOfCard(page, 't-2')).toBe('normal');
  expect(priorities).toEqual([{ id: 't-2', body: { priority: 'normal' } }]);
});

test('a refused priority change puts the card back in its lane and says why', async ({ page }) => {
  const tasks = TASKS();
  const { priorities } = await serve(page, tasks, { refusePriority: 'task is Done; its priority can no longer change' });
  await page.goto('/#/projects/p-1/board');
  await expect(kanban(page).locator('.card')).toHaveCount(7);

  await card(page, 't-2').dragTo(lane(page, 'high').locator('.column-body'));

  await expect(board(page).locator('.move-error')).toContainText('Could not move “Write the greet module” to High');
  await expect(board(page).locator('.move-error')).toContainText('task is Done; its priority can no longer change');
  expect(await laneOfCard(page, 't-2')).toBe('low');
  expect(priorities).toHaveLength(1);
  expect(tasks.find((t) => t.id === 't-2')!.priority).toBe('Low');
});

test('Retry on a failed card resets it and the card moves to To do', async ({ page }) => {
  const { retries } = await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');

  await card(page, 't-5').locator('b-button.retry').click();
  await expect(column(page, 'todo').locator('.card[data-card-id="t-5"]')).toHaveCount(1);
  await expect(column(page, 'blocked').locator('.card[data-card-id="t-5"]')).toHaveCount(0);
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
  await expect(column(page, 'progress').locator('.card[data-card-id="t-3"]')).toHaveCount(1);

  Object.assign(tasks.find((t) => t.id === 't-3')!, { status: 'Done' });
  Object.assign(tasks.find((t) => t.id === 't-1')!, { status: 'Working', assignedAgent: 'csharp' });

  await expect(column(page, 'done').locator('.card[data-card-id="t-3"]')).toHaveCount(1, { timeout: 12_000 });
  await expect(column(page, 'progress').locator('.card[data-card-id="t-1"]')).toHaveCount(1);
  await expect(card(page, 't-1')).toContainText('csharp');
});

test('a project with no tasks says where they come from', async ({ page }) => {
  await serve(page, []);
  await page.goto('/#/projects/p-1/board');
  await expect(board(page)).toContainText('No tasks yet');
});

test('in narrow space the columns stack and the cards turn compact', async ({ page }) => {
  const tasks = TASKS();
  await serve(page, tasks);
  await page.goto('/#/projects/p-1/board');
  await expect(kanban(page).locator('.card')).toHaveCount(7);
  expect(await card(page, 't-1').locator('.title').evaluate((el) => getComputedStyle(el).webkitLineClamp)).toBe('2');

  const lefts = () => kanban(page).locator('.column').evaluateAll((els) => els.map((el) => Math.round(el.getBoundingClientRect().left)));
  expect(new Set(await lefts()).size).toBe(4); // side by side

  // Dragon expanded (TASK-152) narrows the board's space; the board follows its own width, not the window's.
  await board(page).evaluate((el) => { (el as HTMLElement).style.width = '420px'; });
  await expect.poll(async () => new Set(await lefts()).size).toBe(1);
  await expect(kanban(page)).toHaveAttribute('stacked', '');
  const titleLines = await card(page, 't-1').locator('.title').evaluate((el) => getComputedStyle(el).webkitLineClamp);
  expect(titleLines).toBe('1');

  // A refresh that moves a card keeps the board stacked.
  tasks.find((t) => t.id === 't-3')!.status = 'Done';
  await expect(column(page, 'done').locator('.card[data-card-id="t-3"]')).toHaveCount(1, { timeout: 12_000 });
  await expect(kanban(page)).toHaveAttribute('stacked', '');
  expect(new Set(await lefts()).size).toBe(1);
});

test('below the breakpoint the columns stack with no horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await serve(page, TASKS());
  await page.goto('/#/projects/p-1/board');
  await expect(kanban(page).locator('.card')).toHaveCount(7);

  const layout = await page.evaluate(() => {
    const content = document.querySelector('kobold-lair-shell')!.shadowRoot!.querySelector('.app-content') as HTMLElement;
    return {
      page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      content: content.scrollWidth - content.clientWidth,
    };
  });
  expect(layout).toEqual({ page: 0, content: 0 });
  const lefts = await kanban(page).locator('.column').evaluateAll((els) => els.map((el) => Math.round(el.getBoundingClientRect().left)));
  expect(new Set(lefts).size).toBe(1);
});
