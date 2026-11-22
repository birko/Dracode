import { test, expect } from './fixtures.js';
import type { Page, WebSocketRoute } from '@playwright/test';

// TASK-149: the projects homepage docks a "home Dragon" with its own conversation, for starting projects. The /dragon
// socket is played by a fake server (page.routeWebSocket): select_home is answered with home_selected and the home
// conversation replayed, select_project with the project's. Keeping the home conversation is the server's (.NET tests).

const home = (page: Page) => page.locator('#route-outlet kobold-home-view');
const dock = (page: Page) => home(page).locator('kobold-dragon-dock');
const bubbles = (page: Page, role: 'user' | 'assistant') => dock(page).locator(`b-chat .message.${role} .bubble`);
const input = (page: Page) => dock(page).locator('b-chat textarea');
const cards = (page: Page) => home(page).locator('.project-card');

const HOME: Array<[role: 'user' | 'dragon', text: string]> = [['user', 'I want a shop'], ['dragon', 'What will it **sell**?']];
const SHOP = { id: 'p-9', name: 'shop', ownerId: 'u-1', status: 'New', executionState: 'Running', createdAt: '2026-11-22T10:00:00Z' };

class FakeDragon {
  readonly received: Array<Record<string, any>> = [];
  ws: WebSocketRoute | null = null;

  async install(page: Page): Promise<void> {
    await page.routeWebSocket(/\/dragon(\?|$)/, (ws) => {
      this.ws = ws;
      ws.onMessage((raw) => {
        const msg = JSON.parse(String(raw));
        if (msg.type === 'ping') return;
        this.received.push(msg);
        if (msg.type === 'select_home') {
          this.send({ type: 'home_selected', sessionId: 's-1', messageCount: HOME.length });
          for (const [role, text] of HOME) {
            this.send(role === 'user'
              ? { type: 'user_message', role: 'user', content: text, isReplay: true, sessionId: 's-1' }
              : { type: 'dragon_message', message: text, isReplay: true, sessionId: 's-1' });
          }
        }
        if (msg.type === 'select_project') {
          this.send({ type: 'project_selected', sessionId: 's-1', projectId: msg.projectId, projectName: 'shop', messageCount: 0 });
        }
      });
    });
  }

  send(msg: Record<string, unknown>): void {
    this.ws?.send(JSON.stringify({ timestamp: '2026-11-22T10:00:00Z', ...msg }));
  }

  sent(type: string): Array<Record<string, any>> {
    return this.received.filter((m) => m.type === type);
  }
}

/** Sign in and serve the projects: the list reads `projects` at each request, so a test can add one. */
async function open(page: Page, projects: Array<Record<string, unknown>>): Promise<FakeDragon> {
  const dragon = new FakeDragon();
  await dragon.install(page);
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/projects(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    const id = path.split('/')[4];
    if (!id) return route.fulfill({ json: projects.map((p) => ({ ...p, tasks: { total: 0, done: 0, inProgress: 0, failed: 0 } })) });
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: id, content: '# Spec', version: 1 } });
    if (path.endsWith('/features')) return route.fulfill({ json: [] });
    const project = projects.find((p) => p.id === id);
    return project ? route.fulfill({ json: project }) : route.fulfill({ status: 404, body: '' });
  });
  await page.goto('/#/');
  return dragon;
}

test('the homepage docks Dragon with the home conversation, not a project’s', async ({ page }) => {
  const dragon = await open(page, []);

  await expect(bubbles(page, 'user')).toHaveText(['I want a shop']);
  await expect(bubbles(page, 'assistant')).toHaveText(['What will it sell?']);
  expect(dragon.sent('select_home')).toHaveLength(1);
  expect(dragon.sent('select_project')).toEqual([]);
  await expect(input(page)).toBeEnabled();
  await expect(input(page)).toHaveAttribute('placeholder', /start a project/i);

  await input(page).fill('Books');
  await input(page).press('Enter');
  await expect.poll(() => dragon.sent('message').map((m) => m.message)).toEqual(['Books']);
});

test('a project the home Dragon creates appears on the homepage, and opening it switches Dragon to it', async ({ page }) => {
  const projects: Array<Record<string, unknown>> = [];
  const dragon = await open(page, projects);
  await expect(bubbles(page, 'assistant')).toHaveCount(1);
  await expect(cards(page)).toHaveCount(0);

  projects.push(SHOP);
  dragon.send({ type: 'specification_created', sessionId: 's-1', projectId: SHOP.id, filename: 'specification.md' });
  await expect(cards(page)).toHaveCount(1);
  await expect(cards(page).first()).toContainText('shop');

  await cards(page).first().click();
  await expect(page).toHaveURL(/#\/projects\/p-9\//);
  await expect.poll(() => dragon.sent('select_project').map((m) => m.projectId)).toEqual(['p-9']);
  const workspaceDock = page.locator('#route-outlet kobold-project-workspace kobold-dragon-dock');
  await expect(workspaceDock.locator('b-chat .message.user .bubble')).toHaveCount(0);

  await page.goto('/#/');
  await expect.poll(() => dragon.sent('select_home').length).toBe(2);
  await expect(bubbles(page, 'user')).toHaveText(['I want a shop']);
});

test('after a reload the homepage asks for the home conversation again', async ({ page }) => {
  const dragon = await open(page, []);
  await expect(bubbles(page, 'user')).toHaveText(['I want a shop']);

  await page.reload();
  await expect(bubbles(page, 'user')).toHaveText(['I want a shop']);
  expect(dragon.sent('select_home').length).toBe(2);
});

test('at phone width the dock sits under the cards with no horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await open(page, [SHOP]);
  await expect(bubbles(page, 'user')).toHaveText(['I want a shop']);

  const layout = await page.evaluate(() => {
    const view = document.querySelector('#route-outlet kobold-home-view')!.shadowRoot!;
    const r = (sel: string) => view.querySelector(sel)!.getBoundingClientRect();
    return { cardsBottom: r('.project-card').bottom, dockTop: r('kobold-dragon-dock').top, scroll: document.documentElement.scrollWidth > window.innerWidth };
  });
  expect(layout.dockTop).toBeGreaterThanOrEqual(layout.cardsBottom);
  expect(layout.scroll).toBe(false);
});
