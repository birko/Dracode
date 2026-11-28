import { test, expect } from './fixtures.js';
import type { Page, Route } from '@playwright/test';

// TASK-211: the workspace's Settings tab switches the project's lifecycle layer (FEATURE-078 D5) through
// PATCH /api/v1/projects/{id}/lifecycle; a refusal puts the switch back and says why; a phone does not offer the tab.
// REST stubbed; the docked Dragon's socket held open.

type Served = { patches: Array<{ enabled: boolean }> };

async function serve(page: Page, lifecycleEnabled: boolean, patchStatus = 200): Promise<Served> {
  const served: Served = { patches: [] };
  let enabled = lifecycleEnabled;
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/(projects|tasks)(\/|\?|$)/, (route: Route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path.endsWith('/lifecycle') && request.method() === 'PATCH') {
      const body = request.postDataJSON() as { enabled: boolean };
      served.patches.push(body);
      if (patchStatus !== 200) return route.fulfill({ status: patchStatus, json: { error: 'no' } });
      enabled = body.enabled;
      return route.fulfill({ json: { projectId: 'p-1', lifecycleEnabled: enabled } });
    }
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'u-1', status: 'InProgress', executionState: 'Running',
      lifecycleEnabled: enabled, createdAt: '2026-11-20T10:00:00Z' } });
  });
  return served;
}

const tab = (page: Page) => page.locator('#route-outlet kobold-settings-tab');
const toggle = (page: Page) => tab(page).locator('b-switch.lifecycle');

test('the Settings tab shows the lifecycle layer and switches it on and off', async ({ page }) => {
  const served = await serve(page, false);
  await page.goto('/#/projects/p-1/settings');

  await expect(toggle(page)).not.toHaveAttribute('checked', /.*/);
  await expect(tab(page).locator('.state')).toHaveText('Off');

  await toggle(page).locator('input').check({ force: true });
  await expect(tab(page).locator('.state')).toHaveText('On');
  expect(served.patches).toEqual([{ enabled: true }]);

  await toggle(page).locator('input').uncheck({ force: true });
  await expect(tab(page).locator('.state')).toHaveText('Off');
  expect(served.patches).toEqual([{ enabled: true }, { enabled: false }]);
});

test('a refused change puts the switch back and says so', async ({ page }) => {
  await serve(page, true, 500);
  await page.goto('/#/projects/p-1/settings');
  await expect(tab(page).locator('.state')).toHaveText('On');

  await toggle(page).locator('input').uncheck({ force: true });

  await expect(tab(page).locator('.error')).toContainText('500');
  await expect(tab(page).locator('.state')).toHaveText('On');
  await expect(toggle(page)).toHaveAttribute('checked', /.*/);
});

test('a phone does not offer the Settings tab', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await serve(page, true);
  await page.goto('/#/projects/p-1/settings');

  await expect(page.locator('#route-outlet .desktop-only')).toContainText('Settings is on a computer');
  await expect(tab(page)).toHaveCount(0);
});
