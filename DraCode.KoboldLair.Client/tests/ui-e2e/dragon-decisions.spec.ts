import { test, expect } from './fixtures.js';
import type { Page, WebSocketRoute } from '@playwright/test';

// TASK-160: decision cards in the Dragon dock. A fake /dragon server answers select_project, sends the open decisions on
// client_ready (as `decision_required` project notifications, TASK-024's shape), takes `answer_decision` and replies with
// `decision_answer_result` when the test says so. REST stubbed; the bell's notification list is served by the test.

const workspace = (page: Page) => page.locator('#route-outlet kobold-project-workspace');
const dock = (page: Page) => page.locator('#route-outlet kobold-dragon-dock');
const cards = (page: Page) => dock(page).locator('article.decision');
const card = (page: Page, id: string) => dock(page).locator(`article.decision[data-decision-id="${id}"]`);
const badge = (page: Page) => dock(page).locator('.decision-count');
const dockWidth = (page: Page) => workspace(page).locator('aside.dock').evaluate((el) => Math.round(el.getBoundingClientRect().width));
const bellCount = (page: Page) => page.locator('kobold-lair-shell kobold-notification-bell .count');

interface Decision { id: string; project: string; question: string; source: string; schema: object; context?: string; timeoutAt?: string }

const ESCALATION: Decision = {
  id: 'd-1', project: 'Shop', source: 'Escalation', question: 'The build keeps failing on the payment module. How should the agent go on?',
  schema: { kind: 'choice', options: ['Handle it automatically', 'Resume with my instructions'], allowFreeText: true },
  context: 'tsc: Cannot find module "stripe"',
};
const QUESTION: Decision = {
  id: 'd-2', project: 'Shop', source: 'AskHuman', question: 'Which currency should prices use?',
  schema: { kind: 'choice', options: ['EUR', 'USD'], allowFreeText: false },
};
const ELSEWHERE: Decision = { id: 'd-9', project: 'Blog', source: 'AskHuman', question: 'Tabs or spaces?', schema: { kind: 'text', options: [], allowFreeText: true } };

function notification(d: Decision, type = 'decision_required'): string {
  return JSON.stringify({
    type: 'project_notification', notificationType: type, sessionId: 's-1', projectName: d.project, message: d.question,
    metadata: type === 'decision_required'
      ? { requiresResponse: 'true', decisionId: d.id, taskId: 't-1', source: d.source, disposition: 'HumanDecisionRequired',
          question: d.question, context: d.context ?? '', escalationType: '', timeoutAt: d.timeoutAt ?? '', responseSchema: JSON.stringify(d.schema) }
      : { decisionId: d.id, taskId: 't-1', resolutionKind: 'Answered' },
    timestamp: '2026-11-22T10:00:00Z',
  });
}

class FakeDragon {
  ws: WebSocketRoute | null = null;
  open: Decision[] = [];
  readonly answers: Array<{ decisionId: string; answer: string }> = [];
  readonly readies: number[] = [];

  async install(page: Page): Promise<void> {
    await page.routeWebSocket(/\/dragon(\?|$)/, (ws) => {
      this.ws = ws;
      ws.onMessage((raw) => {
        const msg = JSON.parse(String(raw));
        if (msg.type === 'select_project') {
          ws.send(JSON.stringify({ type: 'project_selected', sessionId: 's-1', projectId: msg.projectId, projectName: 'Shop', messageCount: 0 }));
        } else if (msg.type === 'client_ready') {
          this.readies.push(Date.now());
          for (const d of this.open) ws.send(notification(d));
        } else if (msg.type === 'answer_decision') {
          this.answers.push({ decisionId: msg.decisionId, answer: msg.answer });
        }
      });
    });
  }

  push(text: string): void {
    this.ws!.send(text);
  }

  result(decisionId: string, result: string, message: string): void {
    this.push(JSON.stringify({ type: 'decision_answer_result', sessionId: 's-1', decisionId, result, message, timestamp: '2026-11-22T10:01:00Z' }));
  }
}

/** Serve the project and a bell list holding a notification for each open decision of `dragon`. */
async function open(page: Page, dragon: FakeDragon, tab = 'spec'): Promise<void> {
  await page.setViewportSize({ width: 1400, height: 900 });
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/(projects|tasks)(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: 'p-1', content: '# Spec\n\nA shop.', version: 1 } });
    if (path.endsWith('/features') || path.endsWith('/tasks')) return route.fulfill({ json: [] });
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  });
  await page.route(/\/api\/v1\/notifications(\/|\?|$)/, (route) => route.fulfill({
    json: dragon.open.map((d) => ({
      id: `n-${d.id}`, projectId: d.project === 'Shop' ? 'p-1' : 'p-2', projectName: d.project, type: 'decision_required',
      message: d.question, metadata: { decisionId: d.id }, createdAt: '2026-11-22T10:00:00Z', read: false, readAt: null,
    })),
  }));
  await dragon.install(page);
  await page.goto(`/#/projects/p-1/${tab}`);
  await expect(dock(page).locator('b-chat textarea')).toBeEnabled();
}

test('open decisions of this project show as cards with a count, and Dragon does not widen for them', async ({ page }) => {
  const dragon = new FakeDragon();
  dragon.open = [ESCALATION, QUESTION, ELSEWHERE];
  await open(page, dragon);

  await expect(cards(page)).toHaveCount(2); // the Blog decision belongs to another project
  await expect(badge(page)).toHaveText('2 decisions');
  await expect(card(page, 'd-1')).toContainText('Escalation');
  await expect(card(page, 'd-1')).toContainText('The build keeps failing on the payment module.');
  await expect(card(page, 'd-2')).toContainText('Question');
  await expect(card(page, 'd-1').locator('b-button')).toHaveCount(0); // closed until clicked
  expect(await dockWidth(page)).toBe(360);
  expect(dragon.readies).toHaveLength(1);
});

test('a decision arriving live adds a card without widening Dragon or moving the page', async ({ page }) => {
  const dragon = new FakeDragon();
  await open(page, dragon);
  await expect(badge(page)).toBeHidden();
  const tabTop = await workspace(page).locator('kobold-spec-tab').evaluate((el) => el.getBoundingClientRect().top);

  dragon.push(notification(QUESTION));

  await expect(cards(page)).toHaveCount(1);
  await expect(badge(page)).toHaveText('1 decision');
  expect(await dockWidth(page)).toBe(360);
  expect(await workspace(page).locator('kobold-spec-tab').evaluate((el) => el.getBoundingClientRect().top)).toBe(tabTop);
  // The same card again (live push and a later client_ready) is one card.
  dragon.push(notification(QUESTION));
  await expect(cards(page)).toHaveCount(1);
});

test('clicking a card widens Dragon and offers its options and free text; an option answers it, pending until the result', async ({ page }) => {
  const dragon = new FakeDragon();
  dragon.open = [ESCALATION];
  await open(page, dragon);
  await expect(bellCount(page)).toHaveText('1');

  await card(page, 'd-1').locator('.decision-head').click();
  await expect.poll(() => dockWidth(page)).toBeGreaterThan(600);
  await expect(card(page, 'd-1').locator('.context')).toHaveText('tsc: Cannot find module "stripe"');
  await expect(card(page, 'd-1').locator('b-button[data-answer]')).toHaveText(['Handle it automatically', 'Resume with my instructions']);
  await expect(card(page, 'd-1').locator('textarea')).toBeVisible();

  await card(page, 'd-1').locator('b-button[data-answer="Handle it automatically"]').click();
  await expect.poll(() => dragon.answers).toEqual([{ decisionId: 'd-1', answer: 'Handle it automatically' }]);
  await expect(card(page, 'd-1')).toContainText('Sending your answer');
  await expect(card(page, 'd-1').locator('b-button[data-answer]').first()).toHaveAttribute('disabled', '');

  dragon.open = []; // answered: the server no longer lists it, and its notification is read
  dragon.result('d-1', 'Resolved', 'Answered "The build keeps failing"; the task will resume with your answer.');

  await expect(cards(page)).toHaveCount(0);
  await expect(badge(page)).toBeHidden();
  await expect(dock(page).locator('b-chat .message.system .bubble').last()).toContainText('the task will resume with your answer');
  await expect(bellCount(page)).toHaveCount(0); // the bell drops it at once
  await expect.poll(() => dockWidth(page)).toBeGreaterThan(600); // focus went to the chat input, so Dragon stays wide
});

test('a free-text answer is sent with Enter; an answer the server does not take keeps the card open with the reason', async ({ page }) => {
  const dragon = new FakeDragon();
  dragon.open = [ESCALATION];
  await open(page, dragon);

  await card(page, 'd-1').locator('.decision-head').click();
  const box = card(page, 'd-1').locator('textarea');
  await box.fill('Add stripe to package.json, then rebuild');
  await box.press('Enter');
  await expect.poll(() => dragon.answers).toEqual([{ decisionId: 'd-1', answer: 'Add stripe to package.json, then rebuild' }]);

  dragon.result('d-1', 'InvalidAnswer', '"x" is not one of the options: EUR, USD. The question is still open.');
  await expect(card(page, 'd-1').locator('.note.error')).toContainText('The question is still open.');
  await expect(card(page, 'd-1').locator('textarea')).toBeEnabled();
  await expect(cards(page)).toHaveCount(1);
});

test('a question without free text offers only its options', async ({ page }) => {
  const dragon = new FakeDragon();
  dragon.open = [QUESTION];
  await open(page, dragon);
  await card(page, 'd-2').locator('.decision-head').click();
  await expect(card(page, 'd-2').locator('b-button[data-answer]')).toHaveText(['EUR', 'USD']);
  await expect(card(page, 'd-2').locator('textarea')).toHaveCount(0);
});

test('a decision answered elsewhere or timed out leaves the dock', async ({ page }) => {
  const dragon = new FakeDragon();
  dragon.open = [ESCALATION, QUESTION];
  await open(page, dragon);
  await expect(cards(page)).toHaveCount(2);

  dragon.push(notification(QUESTION, 'decision_resolved'));
  await expect(cards(page)).toHaveCount(1);
  await expect(badge(page)).toHaveText('1 decision');
});

test('a card shows its text as text', async ({ page }) => {
  const dragon = new FakeDragon();
  dragon.open = [{ ...QUESTION, question: 'Use <img src=x onerror="window.__pwned=1"> here?', schema: { kind: 'choice', options: ['<b>yes</b>'], allowFreeText: false } }];
  await open(page, dragon);
  await card(page, 'd-2').locator('.decision-head').click();
  await expect(card(page, 'd-2').locator('.question')).toHaveText('Use <img src=x onerror="window.__pwned=1"> here?');
  await expect(card(page, 'd-2').locator('b-button[data-answer]')).toHaveText('<b>yes</b>');
  await expect(card(page, 'd-2').locator('img')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __pwned?: number }).__pwned)).toBeUndefined();
});
