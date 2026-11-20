import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-113: routing on Birko.Web.Core's Router, with routes from the module manifests and an auth guard.

const outlet = (page: Page, tag: string) => page.locator(`#route-outlet ${tag}`);

/** Serve /api/config as a host whose server requires sign-in. */
async function hostRequiresAuth(page: Page) {
  await page.route('**/api/config', (route) => route.fulfill({
    json: { serverUrl: 'ws://localhost:57087', authRequired: true, endpoints: { wyvern: '/wyvern', dragon: '/dragon' } },
  }));
}

test.describe('module sub-routes open their screens', () => {
  test('#/settings/providers and #/settings/general open the settings view on their tab', async ({ page }) => {
    await page.goto('/#/settings/providers');
    await expect(outlet(page, 'kobold-settings-view[tab="providers"]')).toHaveCount(1);

    await page.evaluate(() => { window.location.hash = '#/settings/general'; });
    await expect(outlet(page, 'kobold-settings-view[tab="general"]')).toHaveCount(1);
  });

  test('#/dragon/specifications opens the Dragon view', async ({ page }) => {
    await page.goto('/#/dragon/specifications');
    await expect(outlet(page, 'kobold-dragon-view')).toHaveCount(1);
  });
});

test('an unknown route shows the not-found view, not the homepage', async ({ page }) => {
  await page.goto('/#/no-such-screen');
  await expect(outlet(page, 'kobold-not-found-view')).toHaveCount(1);
  await expect(outlet(page, 'kobold-home-view')).toHaveCount(0);
});

test('#/login has a screen', async ({ page }) => {
  await page.goto('/#/login');
  await expect(outlet(page, 'kobold-login-view')).toHaveCount(1);
});

test.describe('auth guard', () => {
  test('with auth off (the host default) protected routes are open', async ({ page }) => {
    const config = await (await page.request.get('/api/config')).json();
    expect(config.authRequired).toBe(false);

    await page.goto('/#/settings');
    await expect(outlet(page, 'kobold-settings-view')).toHaveCount(1);
  });

  test('with auth required a signed-out user is sent to #/login', async ({ page }) => {
    await hostRequiresAuth(page);
    await page.goto('/#/settings');

    await expect(outlet(page, 'kobold-login-view')).toHaveCount(1);
    expect(new URL(page.url()).hash).toBe('#/login');
    await expect(outlet(page, 'kobold-settings-view')).toHaveCount(0);
  });

  test('with auth required a signed-in user reaches the screen', async ({ page }) => {
    await hostRequiresAuth(page);
    await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
    await page.goto('/#/settings');

    await expect(outlet(page, 'kobold-settings-view')).toHaveCount(1);
  });
});
