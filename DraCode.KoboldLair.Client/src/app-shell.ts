/**
 * KoboldLair Application Shell
 * Extends Birko.Web.Shell's BAppShell for KoboldLair-specific functionality
 */

import { define } from 'birko-web-core';
import { BAppShell, registerThemes, BUILTIN_THEMES } from 'birko-web-shell/shell';
import { authStore, clearAuth } from './auth-store.js';

// Offer all four built-in themes in the header switcher. NOTE: the served
// wwwroot/index.html is a standalone legacy page that does not currently mount
// this shell or load the Birko tokens, so this stays a no-op until the shell is
// wired into the served page (+ tokens.css / css/themes/*.css are linked).
registerThemes([BUILTIN_THEMES.light, BUILTIN_THEMES.dark, BUILTIN_THEMES.neon, BUILTIN_THEMES.finstat]);
import { moduleStore, buildKoboldLairRibbon, resolveModuleFromHash } from './module-store.js';

/** The strings Birko.Web.Shell's BAppShell asks t() for. */
const SHELL_TEXT: Record<string, string> = {
  'auth.signOut': 'Sign out',
  'bws.ribbon.selectModule': 'Select a module',
  'nav.notifications': 'Notifications',
  'nav.profile': 'Profile',
  'nav.settings': 'Settings',
  'status.conflicts': 'Conflicts',
  'status.offline': 'Offline',
  'status.online': 'Online',
  'status.pending': 'Pending',
  'status.reconnecting': 'Reconnecting',
};

class KoboldLairAppShell extends BAppShell {
  private _modules: any[] = [];

  // ── Required (7 methods) ──

  protected get brandName() {
    return 'KoboldLair';
  }

  protected getUserName() {
    return authStore.get('userName') ?? 'Guest';
  }

  protected getRibbonTabs() {
    return buildKoboldLairRibbon(this._modules);
  }

  protected getActiveTabId() {
    return moduleStore.get('activeModuleId') ?? 'dashboard';
  }

  protected t(key: string, _params?: Record<string, string>) {
    // No i18n yet: English for the keys the Birko shell renders; anything else shows as its key.
    return SHELL_TEXT[key] ?? key;
  }

  protected onTabChange(tabId: string) {
    console.log('🔄 Tab changed:', tabId);

    // Find the module and navigate to its first option
    const mod = this._modules.find((m: any) => m.id === tabId);
    if (mod?.options?.[0]) {
      window.location.hash = mod.options[0].route;
    }
  }

  protected onSignOut() {
    clearAuth();
    window.location.hash = '#/login';
  }

  // ── Optional overrides ──

  protected get brandHref() {
    return '#/';
  }

  protected get version() {
    return 'v1.0.0'; // TODO: Get from assembly/version
  }

  protected getUserInitials() {
    const name = this.getUserName();
    return name.substring(0, 2).toUpperCase();
  }

  protected getRoutes() {
    return {
      dashboard: '#/',
      profile: '#/profile',
      settings: '#/settings',
      login: '#/login'
    };
  }

  // Notifications (not implemented yet - hide)
  protected getUnreadCount() {
    return 0; // Hide notification bell
  }

  protected getNotificationPreviewTag() {
    return null;
  }

  protected getNotificationDrawerTag() {
    return null;
  }

  // Tenants (not applicable - single tenant)
  protected getTenants() {
    return []; // Hide tenant switcher
  }

  protected getCurrentTenant() {
    return null;
  }

  // Status bar
  protected get showStatusBar() {
    return true;
  }

  protected getConnectionState(): 'connected' | 'reconnecting' | 'offline' {
    // TODO: Integrate with WebSocket connection state
    const wsConnected = (window as any).koboldLairWebSocket?.connected;
    return wsConnected ? 'connected' : 'offline';
  }

  protected getStatusText() {
    // TODO: Show current project or task status
    const currentProject = (window as any).koboldLairCurrentProject;
    return currentProject ? `Project: ${currentProject}` : '';
  }

  // ── Lifecycle ──

  protected async onMount() {
    super.onMount();

    // app-shell-init loads the modules into the store (the router needs them too); render the ribbon from it.
    const applyModules = () => {
      this._modules = moduleStore.get('modules') ?? [];
      this.refreshRibbon();
      this.handleHashChange();
    };
    applyModules();
    this._unsubs.push(moduleStore.onChange('modules', applyModules));

    // Subscribe to auth changes
    this._unsubs.push(
      authStore.onChange('userName', () => {
        this.softUpdate(); // Update user display name
      })
    );

    // Subscribe to module changes
    this._unsubs.push(
      moduleStore.onChange('activeModuleId', () => {
        this.refreshRibbon(); // Update active tab highlight
      })
    );

    // Listen for hash changes to resolve active module
    window.addEventListener('hashchange', this.handleHashChange);

    // Initial module resolution
    this.handleHashChange();
  }

  protected onUnmount() {
    super.onUnmount();
    window.removeEventListener('hashchange', this.handleHashChange);
  }

  // ── Helper methods ──

  private handleHashChange = () => {
    const hash = window.location.hash.slice(1); // Remove '#'
    if (hash) {
      resolveModuleFromHash(hash);
    }
  };
}

// Register the custom element
define('kobold-lair-shell', KoboldLairAppShell);

export { KoboldLairAppShell };
