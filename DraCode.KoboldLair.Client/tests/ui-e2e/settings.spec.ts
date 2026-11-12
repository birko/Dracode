import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-112: the settings tabs render through b-tabs' real API (setTabs + slotted panels), and toasts go through the
// framework toast, whose b-toast-item shows its message. Both used Birko.Web components that do not exist.

const settings = (page: Page) => page.locator('#route-outlet kobold-settings-view');
const tabsHost = (page: Page) => settings(page).locator('b-tabs#settingsTabs');

async function openGeneralTab(page: Page) {
  await page.goto('/#/settings/general');
  await expect(tabsHost(page).getByRole('tab', { name: 'General' })).toHaveAttribute('aria-selected', 'true');
  // The form renders its fields from the schema the view gives it, holding the current settings.
  await expect.poll(() => settings(page).locator('#generalSettingsForm').evaluate((form: any) => form.getValues().appearance))
    .toEqual({ theme: 'auto', language: 'en' });
}

test('both settings tabs render and switch', async ({ page }) => {
  await page.goto('/#/settings');

  const providers = tabsHost(page).getByRole('tab', { name: 'Providers' });
  const general = tabsHost(page).getByRole('tab', { name: 'General' });
  await expect(providers).toBeVisible();
  await expect(general).toBeVisible();
  await expect(providers).toHaveAttribute('aria-selected', 'true');
  await expect(settings(page).locator('#saveSettings')).toBeHidden();

  await general.click();
  await expect(general).toHaveAttribute('aria-selected', 'true');
  await expect(settings(page).locator('#saveSettings')).toBeVisible();

  await providers.click();
  await expect(providers).toHaveAttribute('aria-selected', 'true');
  await expect(settings(page).locator('#saveSettings')).toBeHidden();
});

test('a success toast shows its text', async ({ page }) => {
  await openGeneralTab(page);

  await settings(page).locator('#saveSettings').click();

  await expect(page.locator('b-toast-item[variant="success"]')).toContainText('Settings saved successfully');
});

test('an error toast shows its text', async ({ page }) => {
  await openGeneralTab(page);
  // A required field left empty fails validation on save.
  await settings(page).locator('#generalSettingsForm').evaluate((form: any) =>
    form.setValues({ appearance: { theme: '' } }));

  await settings(page).locator('#saveSettings').click();

  await expect(page.locator('b-toast-item[variant="error"]')).toContainText('Theme is required');
});
