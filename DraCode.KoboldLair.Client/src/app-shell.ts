/**
 * KoboldLair application frame (TASK-145, FEATURE-026 D10): a header over the route outlet, on Birko.Web.Shell's
 * BCoreAppShell. The header holds the product name, then a slot each for the project picker, the notification bell
 * and the user menu; their contents are their own tasks (TASK-146, TASK-159, TASK-056). The bell is TASK-159's; the
 * picker is still empty. The user menu (TASK-056) shows the signed-in user with Settings and Sign out, and nothing
 * when nobody is signed in.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { MenuItem } from 'birko-web-shell/shell';
import { decodeJwtPayload } from 'birko-web-shell/auth';
import { BCoreAppShell, registerThemes, BUILTIN_THEMES } from 'birko-web-shell/shell';
import { authStore } from './auth-store.js';
import { signOut } from './services/auth-session.js';
import './components/notification-bell.js';

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

  /** The name from the token (GitHub's profile name, which anyone can set), escaped: the shell puts it into HTML. */
  protected getUserName() {
    if (!authStore.get('token')) return '';
    return escapeHtml(this.rawUserName());
  }

  protected getUserInitials() {
    return escapeHtml(this.rawUserName().slice(0, 2).toUpperCase());
  }

  /** The store's name, else the token's own `name` or `sub` (a token stored without its claims read). */
  private rawUserName(): string {
    const token = authStore.get('token') as string | null;
    const claims = token ? decodeJwtPayload(token) : null;
    return String(authStore.get('userName') ?? claims?.name ?? claims?.sub ?? authStore.get('userId') ?? '');
  }

  protected getUserMenuItems(): MenuItem[] {
    return [
      { id: 'settings', label: this.t('nav.settings'), icon: '&#9881;' },
      { id: 'signout', label: this.t('auth.signOut'), icon: '&#8594;', variant: 'danger', divider: true },
    ];
  }

  protected t(key: string, _params?: Record<string, string>) {
    // No i18n yet: English for the keys the Birko shell renders; anything else shows as its key.
    return SHELL_TEXT[key] ?? key;
  }

  protected onSignOut() {
    signOut();
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
          <div class="header-slot" data-header-slot="notifications"><kobold-notification-bell></kobold-notification-bell></div>
          ${this.renderThemeDropdown()}
          <div class="header-slot" data-header-slot="user-menu">${this.renderUserDropdown()}</div>
        </div>
      </header>
    `;
  }

  protected onMount() {
    super.onMount();
    this._unsubs.push(authStore.onChange('userName', () => this.update()));
    this._unsubs.push(authStore.onChange('token', () => this.update()));
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
