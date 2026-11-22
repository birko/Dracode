import { test, expect } from './fixtures.js';
import type { Page, Route } from '@playwright/test';

// TASK-057: a run's live view, opened from the Runs tab — the plan as a checklist with progress, and the run's tool
// calls, reflections and ending as they stream in (GET /api/v1/runs/{id}/events, stubbed as text/event-stream bodies).
// A finished run shows how it ended without a stream; a dropped stream reconnects without repeating anything.

const minutesAgo = (m: number) => new Date(Date.now() - m * 60000).toISOString();
interface Run { runId: string; mode: string; projectId: string | null; taskId: string | null; prompt: string | null; status: string;
  startedAt: string; completedAt: string | null; completedSteps: number; totalSteps: number; summary: string | null }
const LIVE: Run = { runId: 'r-1', mode: 'pipeline', projectId: 'p-1', taskId: 't-1', prompt: null, status: 'running',
  startedAt: minutesAgo(1), completedAt: null, completedSteps: 1, totalSteps: 3, summary: null };
const PLAN = (second = 'InProgress', third = 'Pending') => ({
  taskId: 't-1', projectId: 'p-1', taskDescription: 'Build the API', status: 'InProgress', totalSteps: 3,
  completedSteps: [second, third].filter((s) => s === 'Completed').length + 1, createdAt: minutesAgo(2),
  steps: [
    { index: 1, title: 'Create the controller', description: '', status: 'Completed' },
    { index: 2, title: 'Add the routes', description: '', status: second },
    { index: 3, title: 'Write the tests', description: '', status: third },
  ],
});

/** An SSE body: one frame per event, as the server writes them. */
const sse = (frames: Array<Record<string, unknown>>) =>
  frames.map((f) => `id: ${f.seq}\nevent: ${f.type}\ndata: ${JSON.stringify({ runId: 'r-1', ...f })}\n\n`).join('');

interface Server { run: Run; plan: object; streams: string[]; eventRequests: number; statusRequests: number }

async function serve(page: Page, server: Server): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/(runs|plans|projects|tasks)(\/|\?|$)/, (route: Route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (path.endsWith('/events')) {
      expect(url.searchParams.get('token')).toBe('test-token');
      const body = server.streams[Math.min(server.eventRequests, server.streams.length - 1)];
      server.eventRequests++;
      return route.fulfill({ status: 200, headers: { 'content-type': 'text/event-stream', 'cache-control': 'no-cache' }, body });
    }
    if (path === '/api/v1/runs') return route.fulfill({ json: [server.run] });
    if (path.startsWith('/api/v1/runs/')) { server.statusRequests++; return route.fulfill({ json: server.run }); }
    if (path.startsWith('/api/v1/plans/')) return route.fulfill({ json: server.plan });
    if (path.endsWith('/tasks')) {
      return route.fulfill({ json: [{ id: 't-1', task: 'Build the API', projectId: 'p-1', status: 'Working', priority: 'High',
        assignedAgent: 'csharp', featureId: null, errorMessage: null, commitFailed: false }] });
    }
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  });
}

const view = (page: Page) => page.locator('#route-outlet kobold-run-view');
const steps = (page: Page) => view(page).locator('.step');
const feed = (page: Page) => view(page).locator('.feed .item');

async function openRun(page: Page): Promise<void> {
  await page.goto('/#/projects/p-1/runs');
  await page.locator('#route-outlet kobold-runs-tab .run[data-run-id="r-1"]').click();
  await expect(view(page)).toHaveCount(1);
}

test('a live run shows its plan as a checklist and its tool calls, reflections, steps and ending as they come', async ({ page }) => {
  const server: Server = { run: { ...LIVE }, plan: PLAN(), eventRequests: 0, statusRequests: 0, streams: [sse([
    { seq: 1, type: 'kobold_run_started' },
    { seq: 2, type: 'kobold_tool_call', phase: 'started', toolName: 'write_file', inputJson: '{"path":"routes.cs","note":"\\u0060x\\u0060 \\u0027y\\u0027"}' },
    { seq: 3, type: 'kobold_tool_call', phase: 'result', toolName: 'write_file', resultPreview: 'wrote 42 lines' },
    { seq: 4, type: 'kobold_reflect', reflection: { stepIndex: 2, progressPercent: 80, confidencePercent: 70, decision: 0, blockers: null } },
    { seq: 5, type: 'kobold_stream', stepIndex: 2, stepStatus: 'Completed', completedSteps: 2, totalSteps: 3 },
    { seq: 6, type: 'kobold_complete', finalStatus: 'Done', completedSteps: 2, totalSteps: 3 },
  ])] };
  await serve(page, server);
  await openRun(page);

  await expect(view(page).locator('.what')).toHaveText('Build the API');
  await expect(steps(page).locator('span:last-child')).toHaveText(['Create the controller', 'Add the routes', 'Write the tests']);
  await expect(view(page).locator('.ended')).toHaveText('The run completed.');
  await expect(steps(page).nth(1)).toHaveAttribute('data-status', 'Completed'); // updated by the stream
  await expect(view(page).locator('.progress')).toContainText('2 of 3 steps');
  await expect(feed(page)).toHaveText([
    /Called write_file\s*\{"path":"routes.cs","note":"`x` 'y'"\}/, // the server's \u escapes read as characters
    /write_file returned\s*wrote 42 lines/,
    'Reflection on step 2: 80% done, 70% confident — continue',
    'Step 2: Completed',
    'The run completed.',
  ]);
  await expect(view(page).locator('b-badge.status')).toHaveText('completed');

  await page.waitForTimeout(2500); // the stream ended with the run: no reconnect to an ended run
  expect(server.eventRequests).toBe(1);
});

test('a run that has already ended shows how it ended at once, without a stream', async ({ page }) => {
  const server: Server = { run: { ...LIVE, status: 'failed', completedAt: minutesAgo(0), summary: 'tsc failed: Cannot find name' },
    plan: PLAN('Failed', 'Skipped'), eventRequests: 0, statusRequests: 0, streams: [''] };
  await serve(page, server);
  await openRun(page);

  await expect(view(page).locator('.ended')).toHaveText('The run failed: tsc failed: Cannot find name');
  await expect(steps(page).nth(1)).toHaveAttribute('data-status', 'Failed');
  await expect(steps(page).nth(2)).toHaveAttribute('data-status', 'Skipped');
  await page.waitForTimeout(1000);
  expect(server.eventRequests).toBe(0);
});

test('a dropped stream reconnects without repeating what was shown, and reads the plan again for the gap', async ({ page }) => {
  const server: Server = { run: { ...LIVE }, plan: PLAN(), eventRequests: 0, statusRequests: 0, streams: [
    sse([ // the connection drops after these (no last frame)
      { seq: 1, type: 'kobold_run_started' },
      { seq: 2, type: 'kobold_tool_call', phase: 'started', toolName: 'write_file', inputJson: '{}' },
    ]),
    sse([ // after the reconnect: one frame shown already, then the rest
      { seq: 2, type: 'kobold_tool_call', phase: 'started', toolName: 'write_file', inputJson: '{}' },
      { seq: 5, type: 'kobold_stream', stepIndex: 3, stepStatus: 'Completed', completedSteps: 3, totalSteps: 3 },
      { seq: 6, type: 'kobold_complete', finalStatus: 'Done', completedSteps: 3, totalSteps: 3 },
    ]),
  ] };
  await serve(page, server);
  await openRun(page);
  await expect(feed(page).first()).toContainText('Called write_file');
  server.plan = PLAN('Completed'); // step 2 completed while the stream was down (its frame, seq 3–4, was missed)

  await expect(view(page).locator('.ended')).toHaveText('The run completed.', { timeout: 10_000 });
  expect(server.eventRequests).toBe(2);
  await expect(feed(page).filter({ hasText: 'Called write_file' })).toHaveCount(1); // not repeated
  await expect(steps(page).nth(1)).toHaveAttribute('data-status', 'Completed'); // from the plan read again
  await expect(steps(page).nth(2)).toHaveAttribute('data-status', 'Completed'); // from the stream
  expect(server.statusRequests).toBeGreaterThanOrEqual(2);
});

test('"All runs" goes back to the list', async ({ page }) => {
  const server: Server = { run: { ...LIVE, status: 'completed' }, plan: PLAN('Completed', 'Completed'), eventRequests: 0, statusRequests: 0, streams: [''] };
  await serve(page, server);
  await openRun(page);
  await view(page).locator('.back').click();
  await expect(view(page)).toHaveCount(0);
  await expect(page.locator('#route-outlet kobold-runs-tab .run')).toHaveCount(1);
});

test('an ad-hoc run has no plan, and what streams in is shown as text', async ({ page }) => {
  const adhoc: Run = { ...LIVE, mode: 'adhoc', projectId: null, taskId: null, prompt: 'Rename <b>the</b> README' };
  const server: Server = { run: adhoc, plan: {}, eventRequests: 0, statusRequests: 0, streams: [sse([
    { seq: 1, type: 'kobold_tool_call', phase: 'started', toolName: 'run_command', inputJson: '<img src=x onerror="window.__pwned=1">' },
    { seq: 2, type: 'error', message: 'git is not installed' },
  ])] };
  await serve(page, server);
  await openRun(page);

  await expect(view(page).locator('.what')).toHaveText('Rename <b>the</b> README');
  await expect(view(page)).toContainText('An ad-hoc run has no plan.');
  await expect(view(page).locator('.ended')).toHaveText('The run failed: git is not installed');
  await expect(feed(page).first().locator('.detail')).toHaveText('<img src=x onerror="window.__pwned=1">');
  await expect(view(page).locator('img')).toHaveCount(0);
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
});

test('below the breakpoint the run view fits without horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  const server: Server = { run: { ...LIVE, status: 'completed' }, plan: PLAN('Completed', 'Completed'), eventRequests: 0, statusRequests: 0, streams: [''] };
  await serve(page, server);
  await openRun(page);
  await expect(view(page).locator('.ended')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
});
