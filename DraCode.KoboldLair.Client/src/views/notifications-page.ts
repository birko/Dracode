/**
 * The caller's notifications full screen (`#/notifications`), the phone's Notifications tab (TASK-164, FEATURE-026
 * D21): the header bell's list (TASK-159), from the same `notificationStore`, unread first. Opening one marks it read
 * and opens its project's board.
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { when } from '../components/notification-bell.js';
import { notificationStore, type NotificationState } from '../stores/notification-store.js';
import { getToken } from '../auth-store.js';

export class NotificationsPage extends KoboldComponent {
  private _state: NotificationState = notificationStore.state;
  private _unsubscribe: (() => void) | null = null;

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); }
      h1 { margin: 0 0 var(--b-space-lg, 1rem); font-size: var(--b-text-2xl, 1.5rem); }
      .list { display: flex; flex-direction: column; gap: var(--b-space-sm, 0.5rem); margin: 0; padding: 0; list-style: none; }
      .notification {
        display: flex; flex-direction: column; gap: 0.125rem; width: 100%; box-sizing: border-box; min-height: 2.75rem;
        padding: var(--b-space-md, 0.75rem); text-align: left; font: inherit; color: inherit; cursor: pointer;
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
      }
      .notification.unread { border-left: 3px solid var(--b-color-primary); }
      .notification:not(.unread) { opacity: 0.75; }
      .notification:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .project { font-weight: var(--b-font-weight-bold, 700); }
      .message-text { overflow-wrap: anywhere; }
      .time { font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .empty { color: var(--b-text-secondary); }
      .heading { margin: var(--b-space-md, 0.75rem) 0 var(--b-space-xs, 0.25rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
    `;
  }

  protected onMount() {
    this._unsubscribe = notificationStore.subscribe((state) => {
      this._state = state;
      this.update();
    });
  }

  protected onUnmount() {
    this._unsubscribe?.();
    this._unsubscribe = null;
  }

  render() {
    if (!getToken()) return `<h1>Notifications</h1><p class="empty">Sign in to see your notifications.</p>`;
    if (!this._state.loaded) return `<h1>Notifications</h1><b-spinner></b-spinner>`;
    const items = [...this._state.items].sort((a, b) => Number(a.read) - Number(b.read));
    if (items.length === 0) return `<h1>Notifications</h1><p class="empty">No notifications.</p>`;
    const unread = items.filter((n) => !n.read);
    const read = items.filter((n) => n.read);
    const row = (n: (typeof items)[number]) => `<li><button type="button" class="notification${n.read ? '' : ' unread'}"
        data-notification-id="${escapeHtml(n.id)}">
        <span class="project">${escapeHtml(n.projectName)}</span>
        <span class="message-text">${escapeHtml(n.message)}</span>
        <span class="time">${escapeHtml(when(n.createdAt))}</span>
      </button></li>`;
    return `<h1>Notifications</h1>
      ${unread.length ? `<p class="heading">Unread</p><ul class="list">${unread.map(row).join('')}</ul>` : ''}
      ${read.length ? `<p class="heading">Earlier</p><ul class="list">${read.map(row).join('')}</ul>` : ''}`;
  }

  protected onUpdated() {
    for (const button of this.$$('button.notification')) {
      this.listen(button, 'click', () => void this.open(button.dataset.notificationId!));
    }
  }

  /** Mark it read and open its project's board. */
  private async open(id: string): Promise<void> {
    const n = this._state.items.find((x) => x.id === id);
    if (!n) return;
    if (!n.read) await notificationStore.markRead(id);
    window.location.hash = `#/projects/${encodeURIComponent(n.projectId)}/board`;
  }
}

define('kobold-notifications-page', NotificationsPage);
