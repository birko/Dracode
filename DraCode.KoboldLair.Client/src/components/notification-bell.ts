/**
 * The header bell (TASK-159, FEATURE-026 D17): the unread count over a bell, and a menu (Birko's `b-dropdown-menu`) of
 * the caller's project notifications, unread first. Opening one marks it read and opens its project. It shows the page's
 * `notificationStore`; with nobody signed in it renders nothing.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BDropdownMenu, DropdownItem } from 'birko-web-components/layout';
import { KoboldComponent } from './kobold-component.js';
import { notificationStore, type NotificationState, type NotificationView } from '../stores/notification-store.js';
import { authStore, getToken } from '../auth-store.js';

/** How many notifications the menu lists. */
const LISTED = 20;
const MESSAGE_PREVIEW = 140;
const NONE = '__none';

export function when(iso: string): string {
  const t = Date.parse(iso);
  if (!t) return '';
  const minutes = Math.round((Date.now() - t) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return new Date(t).toLocaleDateString();
}

/** A menu row's HTML (b-dropdown-menu labels are HTML): every value escaped, the row's width capped for a phone. */
function rowHtml(n: NotificationView): string {
  const message = n.message.length > MESSAGE_PREVIEW ? `${n.message.slice(0, MESSAGE_PREVIEW)}…` : n.message;
  return `<span class="notification-row" data-notification-id="${escapeHtml(n.id)}" data-read="${n.read}"
      style="display:block; max-width:min(22rem, calc(100vw - 9rem)); white-space:normal; line-height:1.35;${n.read ? ' opacity:0.7;' : ''}">
    <strong style="display:block;">${escapeHtml(n.projectName)}</strong>
    <span style="display:block;">${escapeHtml(message)}</span>
    <small style="display:block; opacity:0.7;">${escapeHtml(when(n.createdAt))}</small>
  </span>`;
}

export class NotificationBell extends KoboldComponent {
  private _unsubscribe: (() => void) | null = null;
  private _state: NotificationState = notificationStore.state;

  static get styles() {
    return `
      :host { display: inline-flex; align-items: center; }
      .bell {
        position: relative; display: inline-flex; align-items: center; justify-content: center;
        width: 2.25rem; height: 2.25rem; border: none; border-radius: var(--b-radius, 0.375rem);
        background: none; color: var(--b-text-secondary); cursor: pointer; font-size: 1.1rem;
      }
      .bell:hover { background: var(--b-bg-tertiary, var(--b-bg-secondary)); color: var(--b-text); }
      .bell:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .count {
        position: absolute; top: 0; right: 0; min-width: 0.95rem; height: 0.95rem; padding: 0 0.2rem; box-sizing: border-box;
        border-radius: 999px; background: var(--b-color-danger); color: var(--b-text-on-danger, #fff);
        box-shadow: 0 0 0 2px var(--b-bg-elevated, var(--b-bg));
        font-size: 0.6rem; font-weight: var(--b-font-weight-bold, 700); line-height: 0.95rem; text-align: center;
      }
    `;
  }

  private _unsubscribeAuth: (() => void) | null = null;

  protected onMount() {
    this.follow();
    // Signing in or out (TASK-056) starts or stops the list; a refreshed token changes nothing here.
    this._unsubscribeAuth = authStore.onChange('isAuthenticated', () => {
      this.follow();
      this.update();
    });
  }

  protected onUnmount() {
    this._unsubscribe?.();
    this._unsubscribe = null;
    this._unsubscribeAuth?.();
    this._unsubscribeAuth = null;
  }

  /** Listen to the store while someone is signed in. */
  private follow(): void {
    if (getToken() && !this._unsubscribe) {
      this._unsubscribe = notificationStore.subscribe((state) => {
        this._state = state;
        this.update();
      });
    } else if (!getToken() && this._unsubscribe) {
      this._unsubscribe();
      this._unsubscribe = null;
    }
  }

  render() {
    if (!getToken()) return '';
    const unread = this._state.items.filter((n) => !n.read).length;
    const label = unread ? `Notifications, ${unread} unread` : 'Notifications';
    return `
      <b-dropdown-menu id="notifications-menu" align="right">
        <button class="bell" slot="trigger" aria-label="${escapeHtml(label)}" title="${escapeHtml(label)}">
          <span aria-hidden="true">🔔</span>${unread ? `<span class="count">${unread > 99 ? '99+' : unread}</span>` : ''}
        </button>
      </b-dropdown-menu>
    `;
  }

  protected onUpdated() {
    const menu = this.$('b-dropdown-menu') as BDropdownMenu | null;
    if (!menu) return;
    menu.setItems(this.items());
    this.listen<CustomEvent<{ id: string }>>(menu, 'select', (e) => void this.open(e.detail.id));
  }

  private items(): DropdownItem[] {
    const listed = this._state.items.slice(0, LISTED);
    if (listed.length === 0) {
      return [{ id: NONE, label: this._state.loaded ? 'No notifications' : 'Loading…' }];
    }
    let shownRead = false;
    return listed.map((n, i) => {
      const item: DropdownItem = { id: n.id, label: rowHtml(n) };
      if (i === 0 && !n.read) item.heading = 'Unread';
      if (n.read && !shownRead) {
        shownRead = true;
        item.heading = 'Earlier';
        if (i > 0) item.divider = true;
      }
      return item;
    });
  }

  /** Open a notification: mark it read and go to its project. */
  private async open(id: string): Promise<void> {
    const n = this._state.items.find((x) => x.id === id);
    if (!n) return;
    if (!n.read) await notificationStore.markRead(id);
    window.location.hash = `#/projects/${encodeURIComponent(n.projectId)}`;
  }
}

define('kobold-notification-bell', NotificationBell);
