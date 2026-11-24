import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-152: Dragon widens with focus — ~360px, ~60% of the workspace while focus is inside it (a click or Alt+Shift+D),
// back when focus leaves, ~150 ms (instant with reduced motion); a "Keep expanded" pin remembered across reloads.
// Below the breakpoint the dock sits under the tabs and nothing changes. REST stubbed; a fake /dragon answers
// select_project so the chat is ready.

const workspace = (page: Page) => page.locator('#route-outlet kobold-project-workspace');
const dockBox = (page: Page) => workspace(page).locator('aside.dock');
const dock = (page: Page) => page.locator('#route-outlet kobold-dragon-dock');
const input = (page: Page) => dock(page).locator('b-chat textarea');
const pin = (page: Page) => dock(page).locator('button.pin');
const width = (page: Page) => dockBox(page).evaluate((el) => Math.round(el.getBoundingClientRect().width));
const areaWidth = (page: Page) => workspace(page).locator('.workspace').evaluate((el) => el.getBoundingClientRect().width);

async function serve(page: Page): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/projects(\/|\?|$)/, (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/specification')) return route.fulfill({ json: { name: 'p-1', content: '# Spec\n\nA shop.', version: 1 } });
    if (path.endsWith('/features')) return route.fulfill({ json: [] });
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'u-1', status: 'New', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  });
  await page.routeWebSocket(/\/dragon(\?|$)/, (ws) => {
    ws.onMessage((raw) => {
      const msg = JSON.parse(String(raw));
      if (msg.type === 'select_project') {
        ws.send(JSON.stringify({ type: 'project_selected', sessionId: 's-1', projectId: msg.projectId, projectName: 'Shop', messageCount: 0 }));
      }
    });
  });
}

/** Open the Spec tab with Dragon ready, at a desktop width. */
async function open(page: Page): Promise<void> {
  await page.setViewportSize({ width: 1400, height: 900 });
  await serve(page);
  await page.goto('/#/projects/p-1/spec');
  await expect(input(page)).toBeEnabled();
}

const expectNarrow = (page: Page) => expect.poll(() => width(page)).toBe(360);
const expectWide = async (page: Page) => {
  await expect.poll(async () => Math.abs((await width(page)) - 0.6 * (await areaWidth(page)))).toBeLessThan(2);
};

test('focus in Dragon widens it to ~60%, focus leaving shrinks it back to 360px, and typed text is kept', async ({ page }) => {
  await open(page);
  expect(await width(page)).toBe(360);

  await input(page).click();
  await expectWide(page);
  await input(page).fill('Add a cart page');

  await workspace(page).locator('kobold-spec-tab').click();
  await expectNarrow(page);
  await expect(input(page)).toHaveValue('Add a cart page');

  // A click on a part of Dragon that takes no focus itself (its header) widens it too.
  await dock(page).locator('.dock-head').click({ position: { x: 10, y: 10 } });
  await expectWide(page);
});

test('Alt+Shift+D puts focus in Dragon\'s input and widens it; the shortcut is in the tooltip', async ({ page }) => {
  await open(page);
  await workspace(page).locator('kobold-spec-tab').click();

  await page.keyboard.press('Alt+Shift+KeyD');

  await expect(input(page)).toBeFocused();
  await expectWide(page);
  await expect(dock(page).locator('.dock-head')).toHaveAttribute('title', /Alt\+Shift\+D/);
  await expect(dockBox(page)).toHaveAttribute('aria-keyshortcuts', 'Alt+Shift+D');
});

test('nothing changes on a timer: Dragon stays wide while focus stays in it', async ({ page }) => {
  await open(page);
  await input(page).click();
  await expectWide(page);
  await page.waitForTimeout(3000);
  await expectWide(page);
});

test('the width change takes ~150 ms, and is instant with reduced motion', async ({ page }) => {
  // Since TASK-058 the columns are b-split-panel's, which animates them with --b-split-transition (asked of Birko.Web);
  // the workspace sets it.
  await open(page);
  const transition = () => workspace(page).locator('.workspace').evaluate((el) => getComputedStyle(el).getPropertyValue('--b-split-transition').trim());
  expect(await transition()).toBe('150ms ease');

  await page.emulateMedia({ reducedMotion: 'reduce' });
  expect(await transition()).toBe('0s');
  await input(page).click();
  expect(Math.abs((await width(page)) - 0.6 * (await areaWidth(page)))).toBeLessThan(2); // at once, no waiting
});

test('"Keep expanded" holds Dragon wide when focus leaves, and is remembered after a reload', async ({ page }) => {
  await open(page);
  await expect(pin(page)).toHaveAttribute('aria-pressed', 'false');

  await pin(page).click();
  await expect(pin(page)).toHaveAttribute('aria-pressed', 'true');
  await workspace(page).locator('kobold-spec-tab').click();
  await expectWide(page);

  await page.reload();
  await expect(input(page)).toBeEnabled();
  await expect(pin(page)).toHaveAttribute('aria-pressed', 'true');
  await expectWide(page);

  await pin(page).click(); // unpinned; focus is still in Dragon, so it stays wide until focus leaves
  await expectWide(page);
  await workspace(page).locator('kobold-spec-tab').click();
  await expectNarrow(page);
});

test('on a phone Dragon\'s tab is full width: focus changes nothing and there is no pin', async ({ page }) => {
  // TASK-164 (D22): below 900px Dragon is the tab bar's own tab, not a dock that widens.
  await page.setViewportSize({ width: 375, height: 800 });
  await serve(page);
  await page.goto('/#/projects/p-1/spec');
  await expect(workspace(page).locator('.project-title')).toHaveText('Shop');
  await page.locator('kobold-lair-shell nav.phone-tabs [data-surface="dragon"]').click();
  await expect(input(page)).toBeEnabled();

  const pageWidth = () => page.locator('#route-outlet kobold-dragon-page').evaluate((el) => Math.round(el.getBoundingClientRect().width));
  const dockWidth = () => dock(page).evaluate((el) => Math.round(el.getBoundingClientRect().width));
  const full = await pageWidth();
  expect(Math.abs((await dockWidth()) - full)).toBeLessThan(2);
  await input(page).click();
  expect(Math.abs((await dockWidth()) - full)).toBeLessThan(2);
  await expect(pin(page)).toBeHidden();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
});
