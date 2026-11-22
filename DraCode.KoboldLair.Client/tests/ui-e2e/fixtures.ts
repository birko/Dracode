// The consumer owns the single @playwright/test instance and injects it into the shared toolkit.
// Specs import `test`/`expect` from here (NOT from birko-web-testing) so there is exactly one
// @playwright/test in the process — required for Playwright's test registry / fixtures to work.
import { test as base, expect, type Page } from '@playwright/test';
import { withBirkoFixtures } from 'birko-web-testing/playwright';

// Every signed-in page polls the header bell's notifications (TASK-159). By default they are an empty list, so no spec
// reaches for a server it does not stub; a spec's own route for them, registered later, takes precedence.
export const test = withBirkoFixtures(base).extend<{ page: Page }>({
  page: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    await page.route(/\/api\/v1\/notifications(\/|\?|$)/, (route) => route.fulfill({ json: [] }));
    await page.route(/\/api\/v1\/needs-you(\/|\?|$)/, (route) => route.fulfill({ json: [] }));
    await use(page);
  },
});
export { expect };
