/**
 * KoboldLair Application Shell
 * Extends Birko.Web.Shell's BAppShell for KoboldLair-specific functionality
 */

import { define } from 'birko-web-core';
import { BAppShell } from 'birko-web-shell/shell';
import { authStore, clearAuth } from './auth-store.js';
import { moduleStore, buildKoboldLairRibbon, resolveModuleFromHash, loadModules } from './module-store.js';

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
    // TODO: Implement i18n
    // For now, return key as-is or fallback
    return key || '';
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

    // Load modules
    try {
      this._modules = await loadModules();
      moduleStore.set('modules', this._modules);
      console.log('✅ Modules loaded:', this._modules.length);
      this.refreshRibbon();
    } catch (error) {
      console.error('❌ Failed to load modules:', error);
      this._modules = [];
    }

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
