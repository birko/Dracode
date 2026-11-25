import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-058: workspace layouts, each person's own across their projects (FEATURE-026). The tabs can be reordered and the
// order is kept; on a desktop a divider sets Dragon's width at rest, which focus still widens to ~60% and gives back.
// The Layouts menu saves the current layout by name, loads one (its tab order, Dragon's width, the pin and the tab it
// opens on) and deletes one. A phone shows the tabs in the saved order, with no reordering and no menu. REST stubbed,
// the layouts kept in this file's own fake server; the Dragon socket answers project_selected only.

const DESKTOP = { width: 1400, height: 900 };

interface Layout { tabs?: { workspace?: string[] }; splits?: { workspace?: { master: string; detail: string } }; openTab?: string; dragonPinned?: boolean }

interface Server {
  current: Layout | null;
  saved: Map<string, Layout>;
  puts: { path: string; body: Layout }[];
  deletes: string[];
  refuseSave?: string;
}

async function serve(page: Page, start: Partial<Server> = {}): Promise<Server> {
  const server: Server = { current: null, saved: new Map(), puts: [], deletes: [], ...start };
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/projects(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: 'p-1', content: '# Spec', version: 1 } });
    if (path.endsWith('/features')) return route.fulfill({ json: [] });
    if (path.endsWith('/tasks')) return route.fulfill({ json: [] });
    if (path.endsWith('/stats') || path.endsWith('/runs') || path.includes('/git')) return route.fulfill({ status: 404, body: '' });
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'u-1', status: 'New', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  });
  await page.route(/\/api\/v1\/layouts(\/|\?|$)/, (route) => {
    const req = route.request();
    const path = decodeURIComponent(new URL(req.url()).pathname.replace(/^.*\/api\/v1\/layouts\/?/, ''));
    if (req.method() === 'GET' && path === 'current') return route.fulfill({ json: { layout: server.current } });
    if (req.method() === 'GET' && path === '') {
      return route.fulfill({ json: [...server.saved].map(([name, layout]) => ({ name, layout, updatedAt: '2026-11-24T10:00:00Z' })) });
    }
    if (req.method() === 'PUT') {
      const body = req.postDataJSON() as Layout;
      server.puts.push({ path, body });
      if (path === 'current') { server.current = body; return route.fulfill({ status: 204, body: '' }); }
      if (server.refuseSave) return route.fulfill({ status: 409, json: { error: server.refuseSave } });
      server.saved.set(path, body);
      return route.fulfill({ json: { name: path, layout: body, updatedAt: '2026-11-24T10:00:00Z' } });
    }
    if (req.method() === 'DELETE') {
      server.deletes.push(path);
      return route.fulfill(server.saved.delete(path) ? { status: 204, body: '' } : { status: 404, body: '' });
    }
    return route.fulfill({ status: 405, body: '' });
  });
  await page.routeWebSocket(/\/dragon(\?|$)/, (ws) => {
    ws.onMessage((raw) => {
      const msg = JSON.parse(String(raw));
      if (msg.type === 'select_project') {
        ws.send(JSON.stringify({ type: 'project_selected', sessionId: 's-1', projectId: msg.projectId, projectName: 'Shop', messageCount: 0 }));
      }
    });
  });
  return server;
}

const workspace = (page: Page) => page.locator('#route-outlet kobold-project-workspace');
const tabs = (page: Page) => workspace(page).locator('b-tabs');
const tabNames = (page: Page) => tabs(page).locator('[role="tab"]').allTextContents();
const dockWidth = (page: Page) => workspace(page).locator('aside.dock').evaluate((el) => Math.round(el.getBoundingClientRect().width));
const areaWidth = (page: Page) => workspace(page).locator('.workspace').evaluate((el) => el.getBoundingClientRect().width);
const divider = (page: Page) => workspace(page).locator('b-split-panel .resizer');
const menu = (page: Page) => workspace(page).locator('b-dropdown-menu.layouts');
const menuItem = (page: Page, text: string | RegExp) => menu(page).locator('.item', { hasText: text });
const dragonInput = (page: Page) => workspace(page).locator('kobold-dragon-dock b-chat textarea');
const lastCurrent = (server: Server) => [...server.puts].reverse().find((p) => p.path === 'current')?.body;
/** Dragon's stored width in px: Birko.Web stores rem (Birko TASK-981), the workspace's own corrections px. */
async function storedDragon(page: Page, server: Server): Promise<number | null> {
  const split = lastCurrent(server)?.splits?.workspace;
  if (!split || split.master !== '1fr') return null;
  const rootPx = await page.evaluate(() => parseFloat(getComputedStyle(document.documentElement).fontSize));
  const match = /^([\d.]+)(px|rem)$/.exec(split.detail);
  return match ? Math.round(parseFloat(match[1]) * (match[2] === 'rem' ? rootPx : 1)) : null;
}

async function open(page: Page, tab = 'spec') {
  await page.goto(`/#/projects/p-1/${tab}`);
  await expect(workspace(page).locator('.project-title')).toHaveText('Shop');
  await expect(dragonInput(page)).toBeEnabled();
}

test.describe('on a desktop', () => {
  test.use({ viewport: DESKTOP });

  test('moving a tab keeps the new order, for this and every project, after a reload', async ({ page }) => {
    const server = await serve(page);
    await open(page, 'git'); // the keyboard moves the open tab
    await expect.poll(() => tabNames(page)).toEqual(['Board', 'Spec', 'Runs', 'Git', 'Stats']);
    await expect(tabs(page)).toHaveAttribute('reorderable', '');

    await tabs(page).locator('[role="tab"]', { hasText: 'Git' }).focus();
    await page.keyboard.press('Control+Shift+ArrowLeft');
    await page.keyboard.press('Control+Shift+ArrowLeft');
    await expect.poll(() => tabNames(page)).toEqual(['Board', 'Git', 'Spec', 'Runs', 'Stats']);
    await expect.poll(() => lastCurrent(server)?.tabs?.workspace).toEqual(['board', 'git', 'spec', 'runs', 'stats']);

    await page.reload();
    await expect.poll(() => tabNames(page)).toEqual(['Board', 'Git', 'Spec', 'Runs', 'Stats']);
  });

  test('the divider sets Dragon\'s width, kept after a reload', async ({ page }) => {
    const server = await serve(page);
    await open(page);
    expect(await dockWidth(page)).toBe(360);

    await divider(page).focus();
    await page.keyboard.press('Shift+ArrowLeft'); // Dragon 4rem wider
    const wider = 360 + 4 * (await page.evaluate(() => parseFloat(getComputedStyle(document.documentElement).fontSize)));
    await expect.poll(() => dockWidth(page)).toBe(wider);
    await expect.poll(() => storedDragon(page, server)).toBe(wider);

    await page.reload();
    await expect(dragonInput(page)).toBeEnabled();
    expect(await dockWidth(page)).toBe(wider);
  });

  // TASK-197: Dragon fills the space right of the divider during a mouse drag, keeps the width the drag left (after a
  // reload too), and the drop does not move anything.
  test('dragging the divider with the mouse: Dragon follows with no gap, and the drop keeps the width', async ({ page }) => {
    const server = await serve(page);
    await open(page);
    expect(await dockWidth(page)).toBe(360);
    const panelRight = await workspace(page).locator('b-split-panel').evaluate((el) => el.getBoundingClientRect().right);
    const dockRight = () => workspace(page).locator('aside.dock').evaluate((el) => el.getBoundingClientRect().right);

    const box = (await divider(page).boundingBox())!;
    const x0 = box.x + box.width / 2;
    const y = box.y + 100;
    await page.mouse.move(x0, y);
    await page.mouse.down();
    for (let i = 1; i <= 6; i++) {
      await page.mouse.move(x0 - i * 20, y);
      await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => r(null))));
      expect(Math.abs(panelRight - (await dockRight()))).toBeLessThan(1); // no strip beside Dragon
    }
    const dragged = await dockWidth(page);
    expect(dragged).toBeGreaterThan(470);

    // Every frame after the drop shows the width the drag left
    const after = workspace(page).locator('aside.dock').evaluate((el) => new Promise<number[]>((resolve) => {
      const seen: number[] = [];
      const t0 = performance.now();
      const tick = () => {
        seen.push(Math.round(el.getBoundingClientRect().width));
        if (performance.now() - t0 < 400) requestAnimationFrame(tick); else resolve([...new Set(seen)]);
      };
      requestAnimationFrame(tick);
    }));
    await page.mouse.up();
    expect(await after).toEqual([dragged]);
    await expect.poll(() => storedDragon(page, server)).toBe(dragged);

    await page.reload();
    await expect(dragonInput(page)).toBeEnabled();
    expect(await dockWidth(page)).toBe(dragged);
  });

  // TASK-198: the panel's own minimums (Birko TASK-981) stop the drag at the tabs' 320px and Dragon's 280px, so nothing
  // moves after the drop.
  for (const [label, dx, column, min] of [['the tabs\' 320px', -2000, '.main', 320], ['Dragon\'s 280px', 2000, 'aside.dock', 280]] as const) {
    test(`a drag past ${label} stops there while dragged, and nothing moves after the drop`, async ({ page }) => {
      const server = await serve(page);
      await open(page);
      const width = () => workspace(page).locator(column).evaluate((el) => Math.round(el.getBoundingClientRect().width));
      const box = (await divider(page).boundingBox())!;
      const x0 = box.x + box.width / 2;
      const y = box.y + 100;
      await page.mouse.move(x0, y);
      await page.mouse.down();
      for (let i = 1; i <= 5; i++) {
        await page.mouse.move(x0 + (dx * i) / 5, y);
        await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => r(null))));
        expect(await width()).toBeGreaterThanOrEqual(min);
      }
      expect(await width()).toBe(min);
      const dragon = await dockWidth(page);
      const after = workspace(page).locator('aside.dock').evaluate((el) => new Promise<number[]>((resolve) => {
        const seen: number[] = [];
        const t0 = performance.now();
        const tick = () => {
          seen.push(Math.round(el.getBoundingClientRect().width));
          if (performance.now() - t0 < 400) requestAnimationFrame(tick); else resolve([...new Set(seen)]);
        };
        requestAnimationFrame(tick);
      }));
      await page.mouse.up();
      expect(await after).toEqual([dragon]);
      await expect.poll(() => storedDragon(page, server)).toBe(dragon);
    });
  }

  test('focus still widens Dragon to ~60% and gives back the width you set; no divider while it is wide', async ({ page }) => {
    await serve(page, { current: { splits: { workspace: { master: '1fr', detail: '480px' } } } });
    await open(page);
    expect(await dockWidth(page)).toBe(480);
    await expect(divider(page)).toBeVisible();

    await dragonInput(page).click();
    await expect.poll(async () => Math.abs((await dockWidth(page)) - 0.6 * (await areaWidth(page)))).toBeLessThan(2);
    await expect(divider(page)).toHaveCount(0);

    await tabs(page).locator('[role="tab"]', { hasText: 'Spec' }).focus();
    await expect.poll(() => dockWidth(page)).toBe(480);
    await expect(divider(page)).toBeVisible();
  });

  test('Save current as… stores the tab order, Dragon\'s width, the pin and the open tab under a name', async ({ page }) => {
    const server = await serve(page, { current: { tabs: { workspace: ['board', 'git', 'spec', 'runs', 'stats'] }, splits: { workspace: { master: '1fr', detail: '500px' } } } });
    await open(page, 'git');

    await menu(page).locator('[slot="trigger"]').click();
    await menuItem(page, 'Save current as…').click();
    const form = workspace(page).locator('form.save-layout');
    await form.locator('input').fill('Reviewing');
    await form.locator('button[type="submit"]').click();

    await expect(page.locator('b-toast-item[variant="success"]')).toContainText('Layout “Reviewing” saved');
    expect(server.puts.find((p) => p.path === 'Reviewing')?.body).toEqual({
      tabs: { workspace: ['board', 'git', 'spec', 'runs', 'stats'] },
      splits: { workspace: { master: '1fr', detail: '500px' } },
      openTab: 'git',
      dragonPinned: false,
    });
    await expect(form).toHaveCount(0);
    await menu(page).locator('[slot="trigger"]').click();
    await expect(menuItem(page, /^\s*Reviewing/)).toHaveCount(1);
  });

  test('loading a layout applies it and makes it the current one; deleting one removes it', async ({ page }) => {
    const reviewing: Layout = {
      tabs: { workspace: ['git', 'board', 'spec', 'runs', 'stats'] }, splits: { workspace: { master: '1fr', detail: '520px' } },
      openTab: 'git', dragonPinned: true,
    };
    const server = await serve(page, { saved: new Map([['Reviewing', reviewing], ['Watching runs', { openTab: 'runs' }]]) });
    await open(page);

    await menu(page).locator('[slot="trigger"]').click();
    await menuItem(page, /^\s*Reviewing/).click();
    await expect(page).toHaveURL(/#\/projects\/p-1\/git$/);
    await expect.poll(() => tabNames(page)).toEqual(['Git', 'Board', 'Spec', 'Runs', 'Stats']);
    await expect(workspace(page).locator('kobold-dragon-dock button.pin')).toHaveAttribute('aria-pressed', 'true');
    await expect.poll(async () => Math.abs((await dockWidth(page)) - 0.6 * (await areaWidth(page)))).toBeLessThan(2); // pinned: wide
    await expect.poll(() => lastCurrent(server)).toEqual({ tabs: reviewing.tabs, splits: reviewing.splits, dragonPinned: true });

    // Unpinned, Dragon goes back to the layout's own width
    await workspace(page).locator('kobold-dragon-dock button.pin').click();
    await tabs(page).locator('[role="tab"]', { hasText: 'Board' }).focus();
    await expect.poll(() => dockWidth(page)).toBe(520);

    await menu(page).locator('[slot="trigger"]').click();
    await menuItem(page, 'Delete “Watching runs”').click();
    await expect(page.locator('b-toast-item[variant="success"]')).toContainText('Layout “Watching runs” deleted');
    expect(server.deletes).toEqual(['Watching runs']);
    await menu(page).locator('[slot="trigger"]').click();
    await expect(menuItem(page, /^\s*Watching runs/)).toHaveCount(0);
  });

  test('a refused save shows the server\'s reason', async ({ page }) => {
    await serve(page, { refuseSave: 'You have 50 saved layouts, the most kept. Delete one, or save over one of them.' });
    await open(page);
    await menu(page).locator('[slot="trigger"]').click();
    await menuItem(page, 'Save current as…').click();
    await workspace(page).locator('form.save-layout input').fill('One more');
    await workspace(page).locator('form.save-layout button[type="submit"]').click();
    await expect(page.locator('b-toast-item[variant="error"]')).toContainText('50 saved layouts');
  });
});

test.describe('on a tablet', () => {
  test.use({ viewport: { width: 1024, height: 768 } });

  test('tabs reorder and the Layouts menu is there, but no divider: Dragon is an overlay', async ({ page }) => {
    const server = await serve(page);
    await open(page, 'stats');
    await expect(menu(page)).toBeVisible();
    await expect(divider(page)).toBeHidden();
    await tabs(page).locator('[role="tab"]', { hasText: 'Stats' }).focus();
    await page.keyboard.press('Control+Shift+ArrowLeft');
    await expect.poll(() => lastCurrent(server)?.tabs?.workspace).toEqual(['board', 'spec', 'runs', 'stats', 'git']);
  });
});

test.describe('on a phone', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('the tabs follow the saved order without Git, with no reordering and no Layouts menu', async ({ page }) => {
    await serve(page, { current: { tabs: { workspace: ['stats', 'git', 'runs', 'board', 'spec'] } } });
    await page.goto('/#/projects/p-1/board');
    await expect(workspace(page).locator('.project-title')).toHaveText('Shop');
    await expect.poll(() => tabNames(page)).toEqual(['Stats', 'Runs', 'Board', 'Spec']);
    await expect(tabs(page)).not.toHaveAttribute('reorderable', '');
    await expect(menu(page)).toHaveCount(0);
  });
});
