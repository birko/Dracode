import { build } from 'esbuild';
import { createHash } from 'crypto';
import { readFileSync, existsSync } from 'fs';
import { dirname, resolve } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

const minify = process.argv.includes('--minify');

// Locate the Birko\Web source bucket WITHOUT a machine-specific path: prefer
// the BIRKO_SRC env var (used by CI/Docker), otherwise walk up from this file to
// find the Birko/Web checkout. Safe to commit — no absolute dev-box path is
// baked in; works at any depth and on any machine that keeps the bucket layout.
function resolveBirkoSrc() {
  const env = process.env.BIRKO_SRC;
  if (env) return env.replace(/[\\/]+$/, '').replaceAll('\\', '/');
  for (let d = __dirname; d !== dirname(d); d = dirname(d)) {
    const candidate = resolve(d, 'Birko/Web');
    if (existsSync(resolve(candidate, 'Birko.Web.Core'))) return candidate.replaceAll('\\', '/');
  }
  throw new Error('Cannot locate Birko\\Web. Set the BIRKO_SRC env var to its path.');
}
const BIRKO_SRC = resolveBirkoSrc();

const aliases = {
  // Birko.Web.Core
  'birko-web-core':          `${BIRKO_SRC}/Birko.Web.Core/src/index.ts`,
  'birko-web-core/base':     `${BIRKO_SRC}/Birko.Web.Core/src/base/index.ts`,
  'birko-web-core/state':    `${BIRKO_SRC}/Birko.Web.Core/src/state/index.ts`,
  'birko-web-core/http':     `${BIRKO_SRC}/Birko.Web.Core/src/http/index.ts`,
  'birko-web-core/router':   `${BIRKO_SRC}/Birko.Web.Core/src/router/index.ts`,
  'birko-web-core/i18n':     `${BIRKO_SRC}/Birko.Web.Core/src/i18n/index.ts`,
  'birko-web-core/offline':  `${BIRKO_SRC}/Birko.Web.Core/src/offline/index.ts`,
  // Birko.Web.Components
  'birko-web-components':          `${BIRKO_SRC}/Birko.Web.Components/src/index.ts`,
  'birko-web-components/inputs':   `${BIRKO_SRC}/Birko.Web.Components/src/inputs/index.ts`,
  'birko-web-components/layout':   `${BIRKO_SRC}/Birko.Web.Components/src/layout/index.ts`,
  'birko-web-components/data':     `${BIRKO_SRC}/Birko.Web.Components/src/data/index.ts`,
  'birko-web-components/feedback': `${BIRKO_SRC}/Birko.Web.Components/src/feedback/index.ts`,
  'birko-web-components/nav':      `${BIRKO_SRC}/Birko.Web.Components/src/nav/index.ts`,
  'birko-web-components/command':  `${BIRKO_SRC}/Birko.Web.Components/src/command/index.ts`,
  'birko-web-components/css':      `${BIRKO_SRC}/Birko.Web.Components/css/tokens.css`,
  'birko-web-components/reset':    `${BIRKO_SRC}/Birko.Web.Components/css/reset.css`,
  // Birko.Web.Shell
  'birko-web-shell':                `${BIRKO_SRC}/Birko.Web.Shell/src/index.ts`,
  'birko-web-shell/shell':          `${BIRKO_SRC}/Birko.Web.Shell/src/shell/index.ts`,
  'birko-web-shell/auth':           `${BIRKO_SRC}/Birko.Web.Shell/src/auth/index.ts`,
  'birko-web-shell/modules':        `${BIRKO_SRC}/Birko.Web.Shell/src/modules/index.ts`,
  'birko-web-shell/tenants':        `${BIRKO_SRC}/Birko.Web.Shell/src/tenants/index.ts`,
  'birko-web-shell/notifications':  `${BIRKO_SRC}/Birko.Web.Shell/src/notifications/index.ts`,
  'birko-web-shell/connection':     `${BIRKO_SRC}/Birko.Web.Shell/src/connection/index.ts`,
  'birko-web-shell/commands':       `${BIRKO_SRC}/Birko.Web.Shell/src/commands/index.ts`,
  'birko-web-shell/feedback':       `${BIRKO_SRC}/Birko.Web.Shell/src/feedback/index.ts`,
};

// Main app bundle
await build({
  entryPoints: ['src/app-shell-init.ts'],
  bundle: true,
  outfile: 'wwwroot/dist/app.js',
  format: 'esm',
  target: 'es2022',
  sourcemap: true,
  minify,
  alias: aliases,
  loader: { '.ts': 'ts', '.css': 'text' },
});

// Generate content hash
const appHash = createHash('md5')
  .update(readFileSync('wwwroot/dist/app.js'))
  .digest('hex')
  .slice(0, 8);

console.log(`✓ Built${minify ? ' (minified)' : ''} [${appHash}]`);
