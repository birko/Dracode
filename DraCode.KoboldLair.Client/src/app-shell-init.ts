/**
 * KoboldLair Application Initialization
 * Bootstraps the Birko.Web.Shell application shell
 */

// Register the Birko components (b-ribbon, b-dropdown-menu, b-tabs, …) before the shell that renders them.
import 'birko-web-components';
import { authStore, setAuth, clearAuth, getToken } from './auth-store.js';
import './module-store.js';
import './app-shell.js';
import { startRouter } from './routes.js';
import { moduleStore, loadModules } from './module-store.js';
import configService from './services/config.js';
import { ApiClient } from './services/api-client.js';
import { createRestClient, createSseClient } from './services/http.js';

// Debugging and e2e handle: the token store and the three server clients that read it (TASK-114).
(window as any).koboldLair = {
  auth: { store: authStore, setAuth, clearAuth, getToken },
  clients: { createWyvernClient: () => new ApiClient(), createRestClient, createSseClient },
};

/**
 * Initialize KoboldLair application when DOM is ready
 */
document.addEventListener('DOMContentLoaded', async () => {
  console.log('🏰 Initializing KoboldLair Dashboard...');

  // Pull host-provided config (serverUrl/authRequired) from GET /api/config BEFORE anything connects, so
  // the .NET host config is the single source of truth for the default backend address. Non-fatal.
  await configService.init();

  // Expose shell to window for debugging
  const shell = document.querySelector('#app-shell');
  (window as any).koboldLairShell = shell;

  // Modules first: the shell's ribbon and the router's routes both come from them (TASK-113).
  try {
    const modules = await loadModules();
    moduleStore.set('modules', modules);
    const router = startRouter(modules);
    (window as any).koboldLairRouter = router;
  } catch (error) {
    console.error('❌ Failed to start the router:', error);
  }

  console.log('✅ KoboldLair initialized successfully');
  console.log('💡 Available commands:');
  console.log('   koboldLairShell - Access application shell');
  console.log('   koboldLairRouter - Access router');
  console.log('   koboldLair - Token store and server clients');
});

/**
 * Export for use in other modules
 */
export { KoboldLairAppShell } from './app-shell.js';
export { authStore, setAuth, clearAuth } from './auth-store.js';
export { moduleStore, hasPermission, loadModules } from './module-store.js';
export { startRouter, getRouter } from './routes.js';
