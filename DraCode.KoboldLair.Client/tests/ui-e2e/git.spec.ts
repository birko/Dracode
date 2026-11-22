import { test, expect } from './fixtures.js';
import type { Page, Route } from '@playwright/test';

// TASK-156: the workspace's Git tab — the branches besides main with ahead/behind and whether each merges cleanly, and
// for the branch chosen its changed files and diff, file by file. REST stubbed; the docked Dragon's socket held open.

const minutesAgo = (m: number) => new Date(Date.now() - m * 60000).toISOString();
const BRANCHES = [
  { name: 'feature/f1-login', ahead: 2, behind: 1, lastCommitHash: 'abc1234', lastCommitMessage: 'Login returns true', lastCommitDate: minutesAgo(10),
    mergesCleanly: true, conflicts: [], featureId: 'f1', featureName: 'login' },
  { name: 'feature/f2-greeting', ahead: 1, behind: 1, lastCommitHash: 'def5678', lastCommitMessage: 'Say hi', lastCommitDate: minutesAgo(30),
    mergesCleanly: false, conflicts: ['app.js'], featureId: 'f2', featureName: 'greeting' },
];
const LOGIN_DIFF = {
  branch: 'feature/f1-login', base: 'main', truncated: false,
  files: [
    { path: 'app.js', oldPath: null, status: 'Modified', added: 1, deleted: 1, binary: false, truncated: false,
      patch: 'diff --git a/app.js b/app.js\nindex 1..2 100644\n--- a/app.js\n+++ b/app.js\n@@ -2 +2 @@\n-module.exports = greeting;\n+module.exports = { greeting };' },
    { path: 'login.js', oldPath: null, status: 'Added', added: 1, deleted: 0, binary: false, truncated: false,
      patch: 'diff --git a/login.js b/login.js\nnew file mode 100644\n--- /dev/null\n+++ b/login.js\n@@ -0,0 +1 @@\n+export function login() { return true; }' },
    { path: 'logo.png', oldPath: null, status: 'Added', added: 0, deleted: 0, binary: true, truncated: false, patch: 'diff --git a/logo.png b/logo.png\nBinary files differ' },
  ],
};

interface Server { branches: object[]; repository: boolean; diffs: Record<string, object>; diffRequests: string[] }

async function serve(page: Page, server: Server): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('koboldlair_auth', JSON.stringify({ token: 'test-token' })));
  await page.routeWebSocket(/\/dragon(\?|$)/, () => {});
  await page.route(/\/api\/v1\/(projects|tasks)(\/|\?|$)/, (route: Route) => {
    const path = new URL(route.request().url()).pathname;
    const diff = /\/git\/branches\/([^/]+)\/diff$/.exec(path);
    if (diff) {
      const name = decodeURIComponent(diff[1]);
      server.diffRequests.push(name);
      return server.diffs[name] ? route.fulfill({ json: server.diffs[name] }) : route.fulfill({ status: 404, body: '' });
    }
    if (path.endsWith('/git/branches')) return route.fulfill({ json: { repository: server.repository, baseBranch: 'main', branches: server.branches } });
    return route.fulfill({ json: { id: 'p-1', name: 'Shop', ownerId: 'u-1', status: 'InProgress', executionState: 'Running', createdAt: '2026-11-20T10:00:00Z' } });
  });
}

const tab = (page: Page) => page.locator('#route-outlet kobold-git-tab');
const branch = (page: Page, name: string) => tab(page).locator(`.branch[data-branch="${name}"]`);

test('the Git tab lists the branches with ahead/behind and whether each merges cleanly', async ({ page }) => {
  await serve(page, { branches: BRANCHES, repository: true, diffs: {}, diffRequests: [] });
  await page.goto('/#/projects/p-1/git');

  await expect(tab(page).locator('.branch .name')).toHaveText(['feature/f1-login', 'feature/f2-greeting']);
  await expect(branch(page, 'feature/f1-login').locator('.feature')).toHaveText('login');
  await expect(branch(page, 'feature/f1-login').locator('.ahead')).toHaveText('2 ahead');
  await expect(branch(page, 'feature/f1-login').locator('.behind')).toHaveText('1 behind');
  await expect(branch(page, 'feature/f1-login').locator('.merge')).toHaveText('Merges cleanly');
  await expect(branch(page, 'feature/f2-greeting').locator('.merge')).toHaveText('Conflicts in 1 file');
  await expect(branch(page, 'feature/f2-greeting').locator('.merge')).toHaveAttribute('title', 'app.js');
  await expect(branch(page, 'feature/f1-login').locator('.last')).toContainText('Login returns true · 10 min ago');
  await expect(tab(page).locator('.detail')).toContainText('Choose a branch');
});

test('choosing a branch shows its changed files and the diff, file by file', async ({ page }) => {
  const server: Server = { branches: BRANCHES, repository: true, diffs: { 'feature/f1-login': LOGIN_DIFF }, diffRequests: [] };
  await serve(page, server);
  await page.goto('/#/projects/p-1/git');
  await branch(page, 'feature/f1-login').click();

  await expect(tab(page).locator('.detail h3')).toContainText('feature/f1-login — 3 files');
  expect(server.diffRequests).toEqual(['feature/f1-login']); // the slash travels encoded
  await expect(branch(page, 'feature/f1-login')).toHaveAttribute('aria-current', 'true');
  await expect(tab(page).locator('.files li .mark')).toHaveText(['M', 'A', 'A']);
  await expect(tab(page).locator('.files li a')).toHaveText(['app.js', 'login.js', 'logo.png']);
  await expect(tab(page).locator('.files li').nth(0).locator('.counts')).toHaveText('+1 −1');
  await expect(tab(page).locator('.files li').nth(2).locator('.counts')).toHaveText('binary');

  const appJs = tab(page).locator('details.file').nth(0);
  await expect(appJs.locator('summary')).toHaveText('app.js');
  await expect(appJs.locator('.line.add')).toHaveText(['+module.exports = { greeting };']);
  await expect(appJs.locator('.line.del')).toHaveText(['-module.exports = greeting;']);
  await expect(appJs.locator('.line.hunk')).toHaveText(['@@ -2 +2 @@']);
  await expect(tab(page).locator('details.file').nth(2)).toContainText('Binary file; no text diff.');

  await branch(page, 'feature/f2-greeting').click(); // its diff is gone from the server
  await expect(tab(page).locator('.detail')).toContainText('This branch is gone.');
});

test('a project with no branches besides main, or no repository, says so', async ({ page }) => {
  await serve(page, { branches: [], repository: true, diffs: {}, diffRequests: [] });
  await page.goto('/#/projects/p-1/git');
  await expect(tab(page)).toContainText('No branches besides main.');

  await page.unroute(/\/api\/v1\/(projects|tasks)(\/|\?|$)/);
  await serve(page, { branches: [], repository: false, diffs: {}, diffRequests: [] });
  await page.reload();
  await expect(tab(page)).toContainText('No git repository yet.');
});

test('Refresh reads the branches again and keeps the chosen one open', async ({ page }) => {
  const server: Server = { branches: [BRANCHES[0]], repository: true, diffs: { 'feature/f1-login': LOGIN_DIFF }, diffRequests: [] };
  await serve(page, server);
  await page.goto('/#/projects/p-1/git');
  await branch(page, 'feature/f1-login').click();
  await expect(tab(page).locator('.files li')).toHaveCount(3);

  server.branches = BRANCHES;
  await tab(page).locator('b-button.refresh').click();
  await expect(tab(page).locator('.branch')).toHaveCount(2);
  await expect(branch(page, 'feature/f1-login')).toHaveAttribute('aria-current', 'true');
  await expect(tab(page).locator('.files li')).toHaveCount(3);
});

test('diff text and names are shown as text', async ({ page }) => {
  await serve(page, { branches: [{ ...BRANCHES[0], lastCommitMessage: '<img src=x onerror="window.__pwned=1">' }], repository: true,
    diffs: { 'feature/f1-login': { ...LOGIN_DIFF, files: [{ ...LOGIN_DIFF.files[0], patch: '+<script>window.__pwned=1</script>' }] } }, diffRequests: [] });
  await page.goto('/#/projects/p-1/git');
  await expect(branch(page, 'feature/f1-login').locator('.last')).toContainText('<img src=x onerror="window.__pwned=1">');
  await branch(page, 'feature/f1-login').click();
  await expect(tab(page).locator('.line.add')).toHaveText('+<script>window.__pwned=1</script>');
  await expect(tab(page).locator('img, script')).toHaveCount(0);
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
});
