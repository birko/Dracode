import { test, expect } from './fixtures.js';
import type { Page, WebSocketRoute } from '@playwright/test';

// TASK-151: Dragon docked in the project workspace, following the project. The /dragon socket (through the client
// host's proxy) is played by a fake server with page.routeWebSocket; the REST project endpoints are stubbed as in
// workspace.spec.ts.

const dock = (page: Page) => page.locator('#route-outlet kobold-dragon-dock');
const bubbles = (page: Page, role: 'user' | 'assistant' | 'system') => dock(page).locator(`b-chat .message.${role} .bubble`);
const input = (page: Page) => dock(page).locator('b-chat textarea');

const PROJECTS: Record<string, { name: string; conversation: Array<[role: 'user' | 'dragon', text: string]> }> = {
  'p-1': { name: 'Shop', conversation: [['user', 'Build a shop'], ['dragon', 'Which **payment** provider?']] },
  'p-2': { name: 'Blog', conversation: [['user', 'Build a blog'], ['dragon', 'Static or dynamic?']] },
};

async function serveProjects(page: Page): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/projects(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    const id = path.split('/')[4];
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: id, content: '# Spec', version: 1 } });
    if (path.endsWith('/features')) return route.fulfill({ json: [] });
    const project = PROJECTS[id];
    if (!project) return route.fulfill({ status: 404, body: '' });
    return route.fulfill({ json: { id, name: project.name, ownerId: 'u-1', status: 'New', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  });
}

/** A fake /dragon server: answers select_project with the project's conversation; records what the page sent. */
class FakeDragon {
  readonly connections: WebSocketRoute[] = [];
  readonly urls: string[] = [];
  readonly received: Array<Record<string, any>> = [];
  sessionId = 'session-1';
  /** What to do with a select_project: answer it (default), or leave it to the test. */
  onSelect: ((ws: WebSocketRoute, projectId: string) => void) | null = null;
  /** What to do on a reconnect carrying the session id: resume (default) or report it gone. */
  resume: 'resume' | 'gone' = 'resume';
  private selected: string | null = null;

  async install(page: Page): Promise<void> {
    await page.routeWebSocket(/\/dragon(\?|$)/, (ws) => {
      this.connections.push(ws);
      this.urls.push(ws.url());
      if (new URL(ws.url()).searchParams.get('sessionId')) {
        if (this.resume === 'resume') {
          this.send(ws, { type: 'session_resumed', sessionId: this.sessionId, messageCount: 2 });
          this.replay(ws, this.selected!);
        } else {
          this.sessionId = 'session-2';
          this.send(ws, { type: 'session_not_found', newSessionId: this.sessionId, reason: 'not_found' });
        }
      }
      ws.onMessage((raw) => {
        const msg = JSON.parse(String(raw));
        if (msg.type === 'ping') return;
        this.received.push(msg);
        if (msg.type === 'select_project') {
          if (this.onSelect) return this.onSelect(ws, msg.projectId);
          this.select(ws, msg.projectId);
        }
      });
    });
  }

  select(ws: WebSocketRoute, projectId: string): void {
    const project = PROJECTS[projectId];
    if (!project) {
      this.send(ws, { type: 'error', errorType: 'project_not_found', projectId, sessionId: this.sessionId, message: 'No such project.' });
      return;
    }
    this.selected = projectId;
    this.send(ws, { type: 'project_selected', sessionId: this.sessionId, projectId, projectName: project.name, messageCount: project.conversation.length });
    this.replay(ws, projectId);
  }

  private replay(ws: WebSocketRoute, projectId: string): void {
    for (const [role, text] of PROJECTS[projectId].conversation) {
      this.send(ws, role === 'user'
        ? { type: 'user_message', role: 'user', content: text, isReplay: true, sessionId: this.sessionId }
        : { type: 'dragon_message', message: text, isReplay: true, sessionId: this.sessionId });
    }
  }

  send(ws: WebSocketRoute, msg: Record<string, unknown>): void {
    ws.send(JSON.stringify({ timestamp: '2026-11-21T10:00:00Z', ...msg }));
  }

  get last(): WebSocketRoute {
    return this.connections[this.connections.length - 1];
  }

  sent(type: string): Array<Record<string, any>> {
    return this.received.filter((m) => m.type === type);
  }
}

async function open(page: Page, route = '/#/projects/p-1/spec'): Promise<FakeDragon> {
  const dragon = new FakeDragon();
  await dragon.install(page);
  await serveProjects(page);
  await page.goto(route);
  return dragon;
}

test('the workspace docks Dragon beside the tabs and shows the project’s conversation', async ({ page }) => {
  const dragon = await open(page);

  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);
  await expect(bubbles(page, 'assistant')).toHaveText(['Which payment provider?']);
  await expect(bubbles(page, 'assistant').locator('strong')).toHaveText('payment');
  expect(dragon.sent('select_project')).toEqual([{ type: 'select_project', projectId: 'p-1' }]);
  expect(new URL(dragon.urls[0]).origin.replace('ws', 'http')).toBe(new URL(page.url()).origin);

  const box = await page.evaluate(() => {
    const ws = document.querySelector('#route-outlet kobold-project-workspace')!.shadowRoot!;
    const r = (sel: string) => ws.querySelector(sel)!.getBoundingClientRect();
    return { tabsRight: r('.main').right, dockLeft: r('.dock').left, dockWidth: Math.round(r('.dock').width) };
  });
  expect(box.dockLeft).toBeGreaterThan(box.tabsRight);
  expect(box.dockWidth).toBe(360);
});

test('sending a message shows typing, what Dragon is doing, and the reply as it streams', async ({ page }) => {
  const dragon = await open(page);
  await expect(bubbles(page, 'assistant')).toHaveCount(1);

  await input(page).fill('Use Stripe');
  await input(page).press('Enter');
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop', 'Use Stripe']);
  await expect.poll(() => dragon.sent('message')).toEqual([{ type: 'message', message: 'Use Stripe' }]);

  const ws = dragon.last;
  dragon.send(ws, { type: 'dragon_typing' });
  await expect(dock(page).locator('b-chat .typing:not(.hidden)')).toHaveCount(1);
  dragon.send(ws, { type: 'dragon_thinking', agent: 'Sage', description: 'Updating the specification' });
  await expect(dock(page).locator('.status')).toHaveText('Sage: Updating the specification');

  dragon.send(ws, { type: 'dragon_stream', chunk: 'Stripe it ' });
  await expect(bubbles(page, 'assistant')).toHaveCount(2);
  await expect(bubbles(page, 'assistant').nth(1)).toHaveText('Stripe it');
  await expect(dock(page).locator('b-chat .typing:not(.hidden)')).toHaveCount(0);
  dragon.send(ws, { type: 'dragon_stream', chunk: 'is, **noted**.' });
  await expect(bubbles(page, 'assistant').nth(1)).toHaveText('Stripe it is, noted.');
  await expect(bubbles(page, 'assistant').nth(1).locator('strong')).toHaveText('noted');

  dragon.send(ws, { type: 'dragon_message', message: 'Stripe it is, **noted**. Anything else?', isStreamed: true });
  dragon.send(ws, { type: 'dragon_status', statusType: 'complete', message: 'Response complete' });
  await expect(bubbles(page, 'assistant')).toHaveCount(2);
  await expect(bubbles(page, 'assistant').nth(1)).toHaveText('Stripe it is, noted. Anything else?');
  await expect(dock(page).locator('.status')).toHaveText('');
});

test('opening another project switches Dragon to that project’s conversation on the same connection', async ({ page }) => {
  const dragon = await open(page);
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);

  // The server is still finishing the Shop answer when the switch goes out: that late reply must not land in Blog.
  dragon.onSelect = (ws, projectId) => {
    dragon.send(ws, { type: 'dragon_message', message: 'Late Shop answer' });
    dragon.select(ws, projectId);
  };
  await page.evaluate(() => { window.location.hash = '#/projects/p-2/spec'; });

  await expect(bubbles(page, 'user')).toHaveText(['Build a blog']);
  await expect(bubbles(page, 'assistant')).toHaveText(['Static or dynamic?']);
  await expect(dock(page)).not.toContainText('Late Shop answer');
  expect(dragon.connections).toHaveLength(1);
  expect(dragon.sent('select_project').map((m) => m.projectId)).toEqual(['p-1', 'p-2']);

  await page.evaluate(() => { window.location.hash = '#/projects/p-1/spec'; });
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);
});

test('after a dropped connection the dock reconnects with its session and the conversation is replayed', async ({ page }) => {
  const dragon = await open(page);
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);

  await dragon.last.close({ code: 1011, reason: 'server restarting' });
  await expect(dock(page).locator('.conn')).toHaveText('reconnecting…');

  await expect.poll(() => dragon.connections.length, { timeout: 10_000 }).toBe(2);
  expect(new URL(dragon.urls[1]).searchParams.get('sessionId')).toBe('session-1');
  await expect(dock(page).locator('.conn')).toHaveText('connected');
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);
  await expect(bubbles(page, 'assistant')).toHaveText(['Which payment provider?']);
  expect(dragon.sent('select_project')).toHaveLength(1);
});

test('when the session is gone after a reconnect, the dock opens the project again', async ({ page }) => {
  const dragon = await open(page);
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);
  dragon.resume = 'gone';

  await dragon.last.close({ code: 1011, reason: 'server restarted' });
  await expect.poll(() => dragon.sent('select_project').length, { timeout: 10_000 }).toBe(2);
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);
  await expect(bubbles(page, 'assistant')).toHaveText(['Which payment provider?']);
});

test('a project Dragon cannot open says so, and the chat stays closed', async ({ page }) => {
  const dragon = await open(page);
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);
  dragon.onSelect = (ws, projectId) => dragon.send(ws, { type: 'error', errorType: 'project_not_found', projectId, message: 'No such project.' });

  await page.evaluate(() => { window.location.hash = '#/projects/p-2/spec'; });
  await expect(input(page)).toHaveAttribute('placeholder', 'Dragon cannot open this project');
  await expect(input(page)).toBeDisabled();
  await expect(bubbles(page, 'user')).toHaveCount(0);
});

test('markup in a Dragon reply cannot run script or carry handlers', async ({ page }) => {
  const dragon = await open(page);
  await expect(bubbles(page, 'assistant')).toHaveCount(1);
  dragon.send(dragon.last, { type: 'dragon_message', message: 'See [docs](javascript:window.__pwned=1) <img src=x onerror="window.__pwned=2">' });

  await expect(bubbles(page, 'assistant')).toHaveCount(2);
  const found = await bubbles(page, 'assistant').nth(1).evaluate((el) => ({
    handlers: [el, ...el.querySelectorAll('*')].flatMap((n) => [...n.attributes].filter((a) => a.name.startsWith('on')).map((a) => a.name)),
    hrefs: [...el.querySelectorAll('a')].map((a) => a.getAttribute('href')),
  }));
  expect(found.handlers).toEqual([]);
  expect(found.hrefs.filter((h) => h && /javascript:/i.test(h))).toEqual([]);
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
});

test.describe('the old Dragon screen is gone', () => {
  for (const route of ['#/dragon', '#/dragon/specifications']) {
    test(`${route} shows the not-found view`, async ({ page }) => {
      await page.goto(`/${route}`);
      await expect(page.locator('#route-outlet kobold-not-found-view')).toHaveCount(1);
    });
  }
});

test('below the breakpoint Dragon sits under the tabs, with no horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await open(page);
  await expect(bubbles(page, 'user')).toHaveText(['Build a shop']);

  const layout = await page.evaluate(() => {
    const ws = document.querySelector('#route-outlet kobold-project-workspace')!.shadowRoot!;
    const content = document.querySelector('kobold-lair-shell')!.shadowRoot!.querySelector('.app-content') as HTMLElement;
    return {
      dockBelow: ws.querySelector('.dock')!.getBoundingClientRect().top >= ws.querySelector('.main')!.getBoundingClientRect().bottom,
      page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      content: content.scrollWidth - content.clientWidth,
    };
  });
  expect(layout).toEqual({ dockBelow: true, page: 0, content: 0 });
});
