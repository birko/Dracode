/**
 * KoboldLair Client - Main Entry Point
 *
 * This file re-exports all public APIs for the KoboldLair client.
 * Applications can import from this file:
 *
 * ```typescript
 * import { KoboldLairAppShell, authStore, moduleStore, initRouter } from 'koboldlair-client';
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
  loadModules,
  buildKoboldLairRibbon,
  simpleResolver
} from './module-store.js';

// Router
export { initRouter, getRouter } from './router.js';
export type { KoboldRouter } from './router.js';

// Views
export { DashboardView } from './views/dashboard-view.js';
export { DragonView } from './views/dragon-view.js';
export { ProjectsView } from './views/projects-view.js';
export { HierarchyView } from './views/hierarchy-view.js';
export { MetricsView } from './views/metrics-view.js';
export { CompareView } from './views/compare-view.js';
export { SettingsView } from './views/settings-view.js';
export { ProjectConfigView } from './views/project-config-view.js';
export { ImpactView } from './views/impact-view.js';

// Types
export type { ModuleManifest, ModuleOption, ModuleStatus } from 'birko-web-shell/modules';
export type { AuthState } from 'birko-web-shell/auth';

// Services
export { ApiClient, configService, toastService } from './services/index.js';

// Stores
export { notificationStore } from './stores/index.js';

// Components
export { ServerSelector } from './components/index.js';
