/**
 * The caller's project notifications for the header bell and the project badges (TASK-159, FEATURE-026 D17): the same
 * records Dragon receives, read from `GET /api/v1/notifications` (`scope=all`, which only an admin's token widens).
 * Nothing pushes new ones to a page that has no Dragon socket open, so the store polls while it has listeners and the
 * page is visible, and once more when the page comes back into view. Marking one read updates the list at once.
 */

import { createRestClient } from '../services/http.js';
import { getToken } from '../auth-store.js';

export interface NotificationView {
  id: string;
  projectId: string;
  projectName: string;
  type: string;
  message: string;
  metadata: Record<string, string>;
  createdAt: string;
  read: boolean;
  readAt: string | null;
}

export interface NotificationState {
  items: NotificationView[];
  loaded: boolean;
}

type Listener = (state: NotificationState) => void;

/** How often the notifications are reloaded while something shows them and the page is visible. */
export const NOTIFICATION_REFRESH_MS = 15000;

class NotificationStore {
  private _state: NotificationState = { items: [], loaded: false };
  private readonly _listeners = new Set<Listener>();
  private _timer: ReturnType<typeof setTimeout> | null = null;
  private readonly _onVisibility = () => { if (document.visibilityState === 'visible' && this._listeners.size) void this.refresh(); };

  get state(): NotificationState {
    return this._state;
  }

  get unreadCount(): number {
    return this._state.items.filter((n) => !n.read).length;
  }

  /** Unread notifications per project id. */
  unreadByProject(): Map<string, number> {
    const counts = new Map<string, number>();
    for (const n of this._state.items) if (!n.read) counts.set(n.projectId, (counts.get(n.projectId) ?? 0) + 1);
    return counts;
  }

  /** Listen for changes; the first listener starts the polling and the last one to leave stops it. */
  subscribe(listener: Listener): () => void {
    this._listeners.add(listener);
    listener(this._state);
    if (this._listeners.size === 1) {
      document.addEventListener('visibilitychange', this._onVisibility);
      void this.refresh();
    }
    return () => {
      this._listeners.delete(listener);
      if (this._listeners.size === 0) this.stop();
    };
  }

  async refresh(): Promise<void> {
    if (this._timer) clearTimeout(this._timer);
    this._timer = null;
    if (!getToken()) {
      this.set({ items: [], loaded: true });
      return;
    }
    try {
      const response = await createRestClient().get<NotificationView[]>('/notifications', { scope: 'all' });
      if (response.ok && Array.isArray(response.data)) this.set({ items: response.data, loaded: true });
    } catch {
      // Kept as it was; the next round tries again.
    }
    if (this._listeners.size && document.visibilityState === 'visible') {
      this._timer = setTimeout(() => void this.refresh(), NOTIFICATION_REFRESH_MS);
    }
  }

  /** Mark one read; true when the server took it. */
  async markRead(id: string): Promise<boolean> {
    try {
      const response = await createRestClient().post(`/notifications/${encodeURIComponent(id)}/read`);
      if (!response.ok) return false;
      this.set({ items: this._state.items.map((n) => (n.id === id ? { ...n, read: true, readAt: new Date().toISOString() } : n)) });
      return true;
    } catch {
      return false;
    }
  }

  private stop(): void {
    document.removeEventListener('visibilitychange', this._onVisibility);
    if (this._timer) clearTimeout(this._timer);
    this._timer = null;
  }

  private set(patch: Partial<NotificationState>): void {
    const next = { ...this._state, ...patch };
    if (JSON.stringify(next) === JSON.stringify(this._state)) return;
    this._state = next;
    for (const listener of this._listeners) listener(this._state);
  }
}

/** The page's notifications. */
export const notificationStore = new NotificationStore();
export default notificationStore;
