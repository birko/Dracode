/**
 * KoboldLair Application Initialization
 * Bootstraps the Birko.Web.Shell application shell
 */

import './auth-store.js';
import './module-store.js';
import './app-shell.js';
import { initRouter } from './router.js';

/**
 * Initialize KoboldLair application when DOM is ready
 */
document.addEventListener('DOMContentLoaded', async () => {
  console.log('🏰 Initializing KoboldLair Dashboard...');

  // Expose shell to window for debugging
  const shell = document.querySelector('#app-shell');
  (window as any).koboldLairShell = shell;

  // Initialize router
  try {
    const router = initRouter('route-outlet');
    (window as any).koboldLairRouter = router;

    console.log('✅ Router initialized with routes:', router.getCurrentRoute());
  } catch (error) {
    console.error('❌ Failed to initialize router:', error);
  }

  console.log('✅ KoboldLair initialized successfully');
  console.log('💡 Available commands:');
  console.log('   koboldLairShell - Access application shell');
  console.log('   koboldLairRouter - Access router');
});

/**
 * Export for use in other modules
 */
export { KoboldLairAppShell } from './app-shell.js';
export { authStore, setAuth, clearAuth } from './auth-store.js';
export { moduleStore, hasPermission, loadModules } from './module-store.js';
export { initRouter, getRouter } from './router.js';
