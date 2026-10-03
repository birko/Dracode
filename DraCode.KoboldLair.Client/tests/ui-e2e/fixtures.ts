// The consumer owns the single @playwright/test instance and injects it into the shared toolkit.
// Specs import `test`/`expect` from here (NOT from birko-web-testing) so there is exactly one
// @playwright/test in the process — required for Playwright's test registry / fixtures to work.
import { test as base, expect } from '@playwright/test';
import { withBirkoFixtures } from 'birko-web-testing/playwright';

export const test = withBirkoFixtures(base);
export { expect };
