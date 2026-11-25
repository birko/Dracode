/**
 * KoboldLair application frame (TASK-145, FEATURE-026 D10): a header over the route outlet, on Birko.Web.Shell's
 * BCoreAppShell. The header holds the product name, then a slot each for the project picker (TASK-146, shown inside a
 * project) with the "N other projects need you" pill (TASK-147), the notification bell (TASK-159) and the user menu (TASK-056): the signed-in user with the shell's Settings
 * and Sign out, nothing when nobody is signed in.
 *
 * Below 900px (TASK-164, FEATURE-026 D21/D22) the footer is a bottom tab bar on Birko's `Surface` model: Projects,
 * Board and Dragon (both following the last project opened; Board waits for one) and Notifications with the unread
 * count. The header then drops the picker, the pill and the bell, and the user menu drops Settings.
 */

import { define } from 'birko-web-core';
import { decodeJwtPayload } from 'birko-web-shell/auth';
import { BCoreAppShell, registerThemes, BUILTIN_THEMES, activeSurface, type Surface } from 'birko-web-shell/shell';
import { escapeHtml } from 'birko-web-core';
import { authStore } from './auth-store.js';
import { signOut } from './services/auth-session.js';
import './components/notification-bell.js';
import './components/project-picker.js';
import './components/needs-you-pill.js';
import { DEFAULT_THEME, THEME_STORAGE_PREFIX } from './theme-settings.js';
import { isPhone, lastProject, onLastProjectChange, onLayoutModeChange, PHONE_MEDIA } from './services/layout-mode.js';
import { notificationStore } from './stores/notification-store.js';

/** The phone's tabs (D22). Board's route stands for any project screen; its link goes to the last project's board. */
const PHONE_SURFACES: readonly Surface[] = [
  { id: 'projects', route: '/', label: 'Projects' },
  { id: 'board', route: '/projects', label: 'Board' },
  { id: 'dragon', route: '/dragon', label: 'Dragon' },
  { id: 'notifications', route: '/notifications', label: 'Notifications' },
];

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

  /** Where the header's theme menu keeps the choice; the boot script in index.html reads the same key (TASK-188). */
  protected get storagePrefix() {
    return THEME_STORAGE_PREFIX;
  }

  /** A user who has chosen no theme starts on Auto: light or dark as the browser prefers, followed live (TASK-188). */
  protected get defaultTheme() {
    return DEFAULT_THEME;
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

  /** A phone does not offer Settings (D21). */
  protected getUserMenuItems() {
    const items = super.getUserMenuItems();
    return isPhone() ? items.filter((i) => i.id !== 'settings') : items;
  }

  protected renderFooter(): string {
    return `<nav class="phone-tabs" aria-label="Main">${this.renderPhoneTabs()}</nav>`;
  }

  private renderPhoneTabs(): string {
    const active = activeSurface(window.location.hash, PHONE_SURFACES)?.id;
    const project = lastProject();
    const unread = notificationStore.state.items.filter((n) => !n.read).length;
    return PHONE_SURFACES.map((s) => {
      const current = s.id === active ? ' aria-current="page"' : '';
      if (s.id === 'board' && !project) {
        return `<a class="phone-tab" data-surface="board" aria-disabled="true" title="Open a project first">Board</a>`;
      }
      const href = s.id === 'board' ? `#/projects/${encodeURIComponent(project!.id)}/board` : `#${s.route}`;
      const count = s.id === 'notifications' && unread ? `<span class="count">${unread > 99 ? '99+' : unread}</span>` : '';
      return `<a class="phone-tab" data-surface="${s.id}" href="${escapeHtml(href)}"${current}>${s.label}${count}</a>`;
    }).join('');
  }

  /** Redraw the tab bar alone: the rest of the shell, and the page in it, stay as they are. */
  private refreshPhoneTabs(): void {
    const nav = this.$('nav.phone-tabs');
    if (nav) nav.innerHTML = this.renderPhoneTabs();
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
    const onHash = () => this.refreshPhoneTabs();
    window.addEventListener('hashchange', onHash);
    this._unsubs.push(() => window.removeEventListener('hashchange', onHash));
    this._unsubs.push(onLastProjectChange(() => this.refreshPhoneTabs()));
    this._unsubs.push(notificationStore.subscribe(() => this.refreshPhoneTabs()));
    this._unsubs.push(onLayoutModeChange(() => this.refreshUserMenu()));
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
      .phone-tabs { display: none; }
      @media ${PHONE_MEDIA} {
        .picker-slot, .needs-slot, [data-header-slot="notifications"] { display: none; }
        .phone-tabs {
          display: flex; flex-shrink: 0;
          background: var(--b-bg-elevated); border-top: 1px solid var(--b-border);
          padding-bottom: env(safe-area-inset-bottom, 0);
        }
        .phone-tab {
          flex: 1 1 0; min-width: 0; min-height: 3.25rem; position: relative;
          display: flex; align-items: center; justify-content: center; gap: 0.25rem;
          color: var(--b-text-secondary); text-decoration: none;
          font-size: var(--b-text-sm, 0.8125rem); font-weight: var(--b-font-weight-medium, 500);
          -webkit-tap-highlight-color: transparent;
        }
        .phone-tab[aria-current="page"] { color: var(--b-color-primary); font-weight: var(--b-font-weight-bold, 700); }
        .phone-tab[aria-disabled="true"] { color: var(--b-text-muted); opacity: 0.6; }
        .phone-tab:focus-visible { outline: none; box-shadow: var(--b-focus-ring-inset); }
        .phone-tab .count {
          min-width: 1.1rem; height: 1.1rem; padding: 0 0.25rem; box-sizing: border-box; border-radius: 999px;
          background: var(--b-color-danger); color: var(--b-text-on-danger, #fff);
          font-size: 0.65rem; font-weight: var(--b-font-weight-bold, 700); line-height: 1.1rem; text-align: center;
        }
      }
    `;
  }
}

define('kobold-lair-shell', KoboldLairAppShell);

export { KoboldLairAppShell };
