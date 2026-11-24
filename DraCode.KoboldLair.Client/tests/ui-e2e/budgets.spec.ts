import { test, expect } from './fixtures.js';
import type { Page } from '@playwright/test';

// TASK-163: Settings → Budgets, for an admin. Each budget (daily, monthly, per project) beside this period's spend, in
// the warning colour past the threshold and the danger colour past the budget; a form saves all four over
// PUT /api/v1/budgets, and the server's refusal is shown as it is. REST stubbed.

const BUDGETS = () => ({
  dailyBudgetUsd: 10, monthlyBudgetUsd: 200, projectBudgetUsd: 5, warningThresholdPercent: 80, editable: true,
  spend: { todayUsd: 8.5, thisMonthUsd: 41.5, topProject: { projectId: 'p-1', projectName: 'shop', spentUsd: 6 } },
});

interface Caps { canManageProviders: boolean; canManageBudgets: boolean }

async function serve(page: Page, caps: Caps, budgets: any = BUDGETS()) {
  const puts: any[] = [];
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.route(/\/api\/v1\/settings\/capabilities/, (route) => route.fulfill({
    json: { ...caps, providersEnabled: false, providersHint: 'off', keysEnabled: false, keysHint: 'off' },
  }));
  await page.route(/\/api\/v1\/budgets$/, (route) => {
    const req = route.request();
    if (req.method() === 'PUT') {
      const body = JSON.parse(req.postData()!);
      puts.push(body);
      if (body.warningThresholdPercent > 100) return route.fulfill({ status: 400, json: { error: 'The warning threshold is between 1 and 100 percent.' } });
      Object.assign(budgets, body);
      return route.fulfill({ json: budgets });
    }
    return route.fulfill({ json: budgets });
  });
  return puts;
}

const tab = (page: Page) => page.locator('#route-outlet kobold-settings-view kobold-budgets-tab');
const row = (page: Page, kind: string) => tab(page).locator(`.budget.${kind}`);
const form = (page: Page) => tab(page).locator('#budgetForm');
/** The form's values as numbers (b-form's decimal fields hold the typed text). */
const values = (page: Page) => form(page).evaluate((f: any) =>
  Object.fromEntries(Object.entries(f.getValues()).map(([k, v]) => [k, Number(v)])));

test('an admin sees each budget beside its spend, coloured by how much is used', async ({ page }) => {
  await serve(page, { canManageProviders: true, canManageBudgets: true });
  await page.goto('/#/settings/budgets');

  await expect(row(page, 'daily').locator('.figures')).toHaveText('$8.50 of $10.00 today (85%)');
  await expect(row(page, 'daily').locator('b-progress')).toHaveAttribute('variant', 'warning'); // past the 80% threshold
  await expect(row(page, 'monthly').locator('.figures')).toHaveText('$41.50 of $200 this month (21%)');
  await expect(row(page, 'monthly').locator('b-progress')).toHaveAttribute('variant', 'success');
  await expect(row(page, 'project').locator('.figures')).toHaveText('$6.00 of $5.00 by shop, the most of any project (120%)');
  await expect(row(page, 'project').locator('b-progress')).toHaveAttribute('variant', 'danger');
  await expect(row(page, 'project')).toContainText('Over budget');
  await expect.poll(() => values(page))
    .toMatchObject({ dailyBudgetUsd: 10, monthlyBudgetUsd: 200, projectBudgetUsd: 5, warningThresholdPercent: 80 });
});

test('a budget of 0 reads as none', async ({ page }) => {
  await serve(page, { canManageProviders: true, canManageBudgets: true }, { ...BUDGETS(), dailyBudgetUsd: 0 });
  await page.goto('/#/settings/budgets');
  await expect(row(page, 'daily').locator('.figures')).toHaveText('No daily budget. $8.50 spent today.');
  await expect(row(page, 'daily').locator('b-progress')).toHaveCount(0);
});

test('Save sends all four values and shows the budgets the server answers with', async ({ page }) => {
  const puts = await serve(page, { canManageProviders: true, canManageBudgets: true });
  await page.goto('/#/settings/budgets');
  await expect.poll(async () => (await values(page)).dailyBudgetUsd).toBe(10);

  await form(page).evaluate((f: any) => f.setValues({ dailyBudgetUsd: 20, warningThresholdPercent: 90 }));
  await tab(page).locator('#saveBudgets').click();

  await expect(page.locator('b-toast-item[variant="success"]')).toContainText('Budgets saved');
  expect(puts).toEqual([{ dailyBudgetUsd: 20, monthlyBudgetUsd: 200, projectBudgetUsd: 5, warningThresholdPercent: 90 }]);
  await expect(row(page, 'daily').locator('.figures')).toHaveText('$8.50 of $20.00 today (43%)');
  await expect(row(page, 'daily').locator('b-progress')).toHaveAttribute('variant', 'success');
});

test('a refused change shows the server\'s reason and keeps the budgets as they were', async ({ page }) => {
  await serve(page, { canManageProviders: true, canManageBudgets: true });
  await page.goto('/#/settings/budgets');
  await expect.poll(async () => (await values(page)).dailyBudgetUsd).toBe(10);

  await form(page).evaluate((f: any) => f.setValues({ warningThresholdPercent: 150 }));
  await tab(page).locator('#saveBudgets').click();

  await expect(page.locator('b-toast-item[variant="error"]')).toContainText('between 1 and 100 percent');
  await expect(row(page, 'daily').locator('.figures')).toHaveText('$8.50 of $10.00 today (85%)');
});

test('where the server cannot save budgets the tab shows them and says why there is no form', async ({ page }) => {
  await serve(page, { canManageProviders: true, canManageBudgets: true }, { ...BUDGETS(), editable: false });
  await page.goto('/#/settings/budgets');
  await expect(row(page, 'daily').locator('.figures')).toHaveText('$8.50 of $10.00 today (85%)');
  await expect(form(page)).toHaveCount(0);
  await expect(tab(page).locator('.read-only')).toContainText('cannot be saved on this server');
});

test('only an admin gets the Budgets tab', async ({ page }) => {
  await serve(page, { canManageProviders: false, canManageBudgets: false });
  await page.goto('/#/settings/budgets');
  await expect(page.locator('#route-outlet kobold-settings-view b-tabs')).toHaveCount(1);
  await expect(tab(page)).toHaveCount(0);
  await expect(page.locator('#route-outlet kobold-settings-view').getByText('Budgets', { exact: true })).toHaveCount(0);
});
