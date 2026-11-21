/**
 * KoboldLair Client - Main Entry Point
 *
 * This file re-exports all public APIs for the KoboldLair client.
 * Applications can import from this file:
 *
 * ```typescript
 * import { KoboldLairAppShell, authStore, moduleStore, startRouter } from 'koboldlair-client';
 * ```
 */

// Application Shell
export { KoboldLairAppShell } from './app-shell.js';

// Auth Store
export {
  authStore,
  setAuth,
  clearAuth,
  setPendingChallenge,
  clearChallenge
} from './auth-store.js';

// Module Store
export {
  moduleStore,
  hasPermission,
  hasModulePermission,
  getVisibleOptions,
  resolveModuleFromHash,
  loadModules
} from './module-store.js';

// Router
export { startRouter, getRouter, buildRoutes } from './routes.js';

// Views
export { HomeView } from './views/home-view.js';
export { DragonDock } from './views/dragon-dock.js';
export { dragonConnection, DragonConnection, type DockState } from './services/dragon-connection.js';
export { ProjectWorkspace, WORKSPACE_TABS } from './views/project-workspace.js';
export { SpecTab } from './views/spec-tab.js';
export { renderMarkdown, sanitizeHtml } from './services/markdown.js';
export { SettingsView } from './views/settings-view.js';

// Types
export type { ModuleManifest, ModuleOption, ModuleStatus } from 'birko-web-shell/modules';
export type { AuthState } from 'birko-web-shell/auth';

// Services
export { ApiClient, configService } from './services/index.js';
export { toast } from 'birko-web-components';

// Stores
export { notificationStore } from './stores/index.js';

// Components
export { ServerSelector } from './components/index.js';
