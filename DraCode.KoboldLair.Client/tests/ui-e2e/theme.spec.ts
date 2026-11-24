import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-188: the app starts on Auto, which follows the browser's light/dark preference live, and a dark browser gets
// the dark theme before the app's script runs (no light flash). A theme chosen in the header's menu sticks, whatever
// the browser says, across a reload.

const theme = (page: Page) => page.evaluate(() => document.documentElement.getAttribute('data-theme') ?? 'light');
const chosen = (page: Page) => page.evaluate(() => (document.querySelector('kobold-lair-shell') as unknown as { currentTheme: string }).currentTheme);

async function pick(page: Page, label: string) {
  await page.locator('kobold-lair-shell .theme-trigger').click();
  await page.locator('kobold-lair-shell #theme-dropdown').getByText(label, { exact: true }).click();
}

test('a dark browser gets the dark theme before the app script has run', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.route('**/dist/app.js', (route) => route.abort()); // only the boot script in <head> can set it
  await page.goto('/#/');
  expect(await theme(page)).toBe('dark');
});

test('a new user starts on Auto, which follows the browser while the page is open', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/#/');
  await expect.poll(() => chosen(page)).toBe('auto');
  expect(await theme(page)).toBe('dark');

  await page.emulateMedia({ colorScheme: 'light' });
  await expect.poll(() => theme(page)).toBe('light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect.poll(() => theme(page)).toBe('dark');
});

test('Dark from the header menu stays dark whatever the browser says, also after a reload; Auto follows again', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/#/');
  await expect.poll(() => chosen(page)).toBe('auto');

  await pick(page, 'Dark');
  await expect.poll(() => theme(page)).toBe('dark');
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.emulateMedia({ colorScheme: 'light' });
  expect(await theme(page)).toBe('dark');
  await page.reload();
  await expect.poll(() => chosen(page)).toBe('dark');
  expect(await theme(page)).toBe('dark');

  await pick(page, 'Auto');
  await expect.poll(() => theme(page)).toBe('light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect.poll(() => theme(page)).toBe('dark');
});
