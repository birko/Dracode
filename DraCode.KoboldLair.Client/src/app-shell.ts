/**
 * KoboldLair application frame (TASK-145, FEATURE-026 D10): a header over the route outlet, on Birko.Web.Shell's
 * BCoreAppShell. The header holds the product name, then a slot each for the project picker, the notification bell
 * and the user menu; their contents are their own tasks (TASK-146, TASK-159, TASK-056), so for now the picker and
 * the bell are empty and the user menu is the shell's own (sign out).
 */

import { define } from 'birko-web-core';
import { BCoreAppShell, registerThemes, BUILTIN_THEMES } from 'birko-web-shell/shell';
import { authStore, clearAuth } from './auth-store.js';

registerThemes([BUILTIN_THEMES.light, BUILTIN_THEMES.dark, BUILTIN_THEMES.neon, BUILTIN_THEMES.finstat]);

/** The strings Birko.Web.Shell's shell asks t() for. */
const SHELL_TEXT: Record<string, string> = {
  'auth.signOut': 'Sign out',
  'nav.notifications': 'Notifications',
  'nav.profile': 'Profile',
  'nav.settings': 'Settings',
};

class KoboldLairAppShell extends BCoreAppShell {
  protected get brandName() {
    return 'KoboldLair';
  }

  protected getUserName() {
    return authStore.get('userName') ?? 'Guest';
  }

  protected t(key: string, _params?: Record<string, string>) {
    // No i18n yet: English for the keys the Birko shell renders; anything else shows as its key.
    return SHELL_TEXT[key] ?? key;
  }

  protected onSignOut() {
    clearAuth();
    window.location.hash = '#/login';
  }

  protected getRoutes() {
    return {
      dashboard: '#/',
      profile: '#/profile',
      settings: '#/settings',
      login: '#/login'
    };
  }

  protected renderHeader(): string {
    return `
      <header class="app-header"${this.accentAttr(this.headerAccent)}>
        <div class="app-brand">${this.renderBrand()}</div>
        <div class="header-slot" data-header-slot="project-picker"></div>
        <div class="app-header-spacer"></div>
        <div class="app-actions">
          <div class="header-slot" data-header-slot="notifications"></div>
          ${this.renderThemeDropdown()}
          <div class="header-slot" data-header-slot="user-menu">${this.renderUserDropdown()}</div>
        </div>
      </header>
    `;
  }

  protected onMount() {
    super.onMount();
    this._unsubs.push(authStore.onChange('userName', () => this.update()));
  }

  static get styles() {
    return `
      ${super.styles}
      .header-slot { display: flex; align-items: center; min-width: 0; }
      .app-header { min-width: 0; }
      .app-brand { flex-shrink: 0; }
      @media (max-width: 56rem) {
        .app-brand { padding: 0 var(--b-space-md, 0.75rem); }
        .app-actions { padding: 0 var(--b-space-sm, 0.5rem); }
      }
    `;
  }
}

define('kobold-lair-shell', KoboldLairAppShell);

export { KoboldLairAppShell };
