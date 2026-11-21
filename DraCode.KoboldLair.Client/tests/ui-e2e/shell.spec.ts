import { test, expect } from './fixtures.js';

// TASK-072: the served page boots the TypeScript shell (src/app-shell.ts) instead of the legacy vanilla-JS page, and
// hash navigation renders the src/views/* views into #route-outlet. TASK-145 replaced the ribbon with a header.

function collectErrors(page: import('@playwright/test').Page): string[] {
  const errors: string[] = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(e.message));
  return errors;
}

test('the served page mounts the TypeScript shell, not the legacy page', async ({ page }) => {
  const errors = collectErrors(page);
  await page.goto('/#/');

  await expect(page.locator('kobold-lair-shell')).toHaveCount(1);
  // The shell rendered its header (TASK-145 replaced the ribbon frame).
  await expect.poll(() => page.evaluate(() =>
    !!document.querySelector('kobold-lair-shell')?.shadowRoot?.querySelector('header.app-header .brand-name'))).toBe(true);
  await expect(page.locator('#route-outlet kobold-home-view')).toHaveCount(1);
  // The header shows English text, not i18n keys (there is no i18n yet).
  const shellText = await page.evaluate(() => document.querySelector('kobold-lair-shell')?.shadowRoot?.textContent ?? '');
  expect(shellText).toContain('KoboldLair');
  expect(shellText).not.toMatch(/\b(modules|status|bws|nav|auth)\.[a-z]/);
  // The legacy page's own header and router are gone.
  await expect(page.locator('.app-header .nav-item')).toHaveCount(0);

  expect(errors).toEqual([]);
});

test('hash navigation swaps the view in the route outlet', async ({ page }) => {
  const errors = collectErrors(page);
  await page.goto('/#/');
  await expect(page.locator('#route-outlet kobold-home-view')).toHaveCount(1);

  await page.evaluate(() => { window.location.hash = '#/settings'; });
  await expect(page.locator('#route-outlet kobold-settings-view')).toHaveCount(1);
  await expect(page.locator('#route-outlet kobold-home-view')).toHaveCount(0);

  expect(errors).toEqual([]);
});

// Every view renders and settles. Each view's onUpdated() used to call onMount(), which reloaded and re-rendered, so the
// first render recursed until "Maximum call stack size exceeded" — a loop the generic smoke settled too early to see.
const VIEWS: Array<[route: string, element: string]> = [
  ['#/', 'kobold-home-view'],
  ['#/settings', 'kobold-settings-view'],
];

for (const [route, element] of VIEWS) {
  test(`${route} renders ${element} and settles without errors`, async ({ page }) => {
    const errors = collectErrors(page);
    await page.goto(`/${route}`);
    await expect(page.locator(`#route-outlet ${element}`)).toHaveCount(1);
    // Rendered content, not an empty element stuck in a render loop.
    await expect.poll(() => page.evaluate((tag) =>
      (document.querySelector(`#route-outlet ${tag}`)?.shadowRoot?.textContent ?? '').trim().length, element)).toBeGreaterThan(0);
    await page.waitForTimeout(1000);
    expect(errors).toEqual([]);
  });
}
