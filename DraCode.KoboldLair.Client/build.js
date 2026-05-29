import { build } from 'esbuild';
import { createHash } from 'crypto';
import { readFileSync, writeFileSync, existsSync } from 'fs';
import { join } from 'path';

const minify = process.argv.includes('--minify');

const aliases = {
  // Birko.Web.Core
  'birko-web-core':          'C:/Source/Birko.Web.Core/src/index.ts',
  'birko-web-core/base':     'C:/Source/Birko.Web.Core/src/base/index.ts',
  'birko-web-core/state':    'C:/Source/Birko.Web.Core/src/state/index.ts',
  'birko-web-core/http':     'C:/Source/Birko.Web.Core/src/http/index.ts',
  'birko-web-core/router':   'C:/Source/Birko.Web.Core/src/router/index.ts',
  'birko-web-core/i18n':     'C:/Source/Birko.Web.Core/src/i18n/index.ts',
  'birko-web-core/offline':  'C:/Source/Birko.Web.Core/src/offline/index.ts',
  // Birko.Web.Components
  'birko-web-components':          'C:/Source/Birko.Web.Components/src/index.ts',
  'birko-web-components/inputs':   'C:/Source/Birko.Web.Components/src/inputs/index.ts',
  'birko-web-components/layout':   'C:/Source/Birko.Web.Components/src/layout/index.ts',
  'birko-web-components/data':     'C:/Source/Birko.Web.Components/src/data/index.ts',
  'birko-web-components/feedback': 'C:/Source/Birko.Web.Components/src/feedback/index.ts',
  'birko-web-components/nav':      'C:/Source/Birko.Web.Components/src/nav/index.ts',
  'birko-web-components/command':  'C:/Source/Birko.Web.Components/src/command/index.ts',
  'birko-web-components/css':      'C:/Source/Birko.Web.Components/css/tokens.css',
  'birko-web-components/reset':    'C:/Source/Birko.Web.Components/css/reset.css',
  // Birko.Web.Shell
  'birko-web-shell':                'C:/Source/Birko.Web.Shell/src/index.ts',
  'birko-web-shell/shell':          'C:/Source/Birko.Web.Shell/src/shell/index.ts',
  'birko-web-shell/auth':           'C:/Source/Birko.Web.Shell/src/auth/index.ts',
  'birko-web-shell/modules':        'C:/Source/Birko.Web.Shell/src/modules/index.ts',
  'birko-web-shell/tenants':        'C:/Source/Birko.Web.Shell/src/tenants/index.ts',
  'birko-web-shell/notifications':  'C:/Source/Birko.Web.Shell/src/notifications/index.ts',
  'birko-web-shell/connection':     'C:/Source/Birko.Web.Shell/src/connection/index.ts',
  'birko-web-shell/commands':       'C:/Source/Birko.Web.Shell/src/commands/index.ts',
  'birko-web-shell/feedback':       'C:/Source/Birko.Web.Shell/src/feedback/index.ts',
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
