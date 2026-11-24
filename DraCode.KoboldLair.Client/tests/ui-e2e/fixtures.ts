// The consumer owns the single @playwright/test instance and injects it into the shared toolkit.
// Specs import `test`/`expect` from here (NOT from birko-web-testing) so there is exactly one
// @playwright/test in the process — required for Playwright's test registry / fixtures to work.
import { test as base, expect, type Page } from '@playwright/test';
import { withBirkoFixtures } from 'birko-web-testing/playwright';

// Every signed-in page polls the header bell's notifications (TASK-159), and the workspace reads the person's layouts
// (TASK-058). By default they are empty, so no spec reaches for a server it does not stub; a spec's own route for them, registered later, takes precedence.
export const test = withBirkoFixtures(base).extend<{ page: Page }>({
  page: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    await page.route(/\/api\/v1\/notifications(\/|\?|$)/, (route) => route.fulfill({ json: [] }));
    await page.route(/\/api\/v1\/needs-you(\/|\?|$)/, (route) => route.fulfill({ json: [] }));
    // Workspace layouts (TASK-058): the current one kept in memory for the page, so a reload gets it back; none saved by
    // name. layouts.spec.ts serves its own.
    let current: unknown = null;
    await page.route(/\/api\/v1\/layouts(\/|\?|$)/, (route) => {
      const req = route.request();
      if (/\/layouts\/current$/.test(new URL(req.url()).pathname)) {
        if (req.method() === 'PUT') { current = req.postDataJSON(); return route.fulfill({ status: 204, body: '' }); }
        return route.fulfill({ json: { layout: current } });
      }
      return route.fulfill(req.method() === 'GET' ? { json: [] } : { status: 404, body: '' });
    });
    await use(page);
  },
});
export { expect };
