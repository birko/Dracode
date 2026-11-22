/**
 * KoboldLair application frame (TASK-145, FEATURE-026 D10): a header over the route outlet, on Birko.Web.Shell's
 * BCoreAppShell. The header holds the product name, then a slot each for the project picker (TASK-146, shown inside a
 * project) with the "N other projects need you" pill (TASK-147), the notification bell (TASK-159) and the user menu (TASK-056): the signed-in user with the shell's Settings
 * and Sign out, nothing when nobody is signed in.
 */

import { define } from 'birko-web-core';
import { decodeJwtPayload } from 'birko-web-shell/auth';
import { BCoreAppShell, registerThemes, BUILTIN_THEMES } from 'birko-web-shell/shell';
import { authStore } from './auth-store.js';
import { signOut } from './services/auth-session.js';
import './components/notification-bell.js';
import './components/project-picker.js';
import './components/needs-you-pill.js';

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

  /** The signed-in user's name, as plain text (the shell escapes it since Birko.Web v0.4.23); none when signed out. */
  protected getUserName() {
    return authStore.get('token') ? this.rawUserName() : '';
  }

  /** The store's name, else the token's own `name` or `sub` (a token stored without its claims read). */
  private rawUserName(): string {
    const token = authStore.get('token') as string | null;
    const claims = token ? decodeJwtPayload(token) : null;
    return String(authStore.get('userName') ?? claims?.name ?? claims?.sub ?? authStore.get('userId') ?? '');
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
        <div class="header-slot picker-slot" data-header-slot="project-picker"><kobold-project-picker></kobold-project-picker></div>
        <div class="header-slot needs-slot" data-header-slot="needs-you"><kobold-needs-you-pill></kobold-needs-you-pill></div>
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
      .picker-slot { flex: 0 1 16rem; padding: 0 var(--b-space-sm, 0.5rem); }
      .needs-slot { flex-shrink: 0; margin-right: var(--b-space-sm, 0.5rem); }
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
