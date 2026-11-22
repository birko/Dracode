/**
 * What waits on the user (TASK-147, FEATURE-026 D10), for the homepage's "Needs you" list and the header's pill:
 * `GET /api/v1/needs-you` (`scope=all`, which only an admin's token widens) — open decisions, failed and blocked tasks,
 * feature branches ready to merge. Like the bell's store it polls while something listens and the page is visible,
 * and once more on return; an answered decision card asks for a refresh at once.
 */

import { createRestClient } from '../services/http.js';
import { authStore, getToken } from '../auth-store.js';

export type NeedsYouKind = 'decision' | 'failed_task' | 'blocked_task' | 'merge_ready';

export interface NeedsYouItem {
  id: string;
  kind: NeedsYouKind | string;
  projectId: string;
  projectName: string;
  title: string;
  createdAt: string;
}

export interface NeedsYouState {
  items: NeedsYouItem[];
  loaded: boolean;
}

type Listener = (state: NeedsYouState) => void;

/** How often the list is reloaded while something shows it and the page is visible. */
export const NEEDS_YOU_REFRESH_MS = 15000;

class NeedsYouStore {
  private _state: NeedsYouState = { items: [], loaded: false };
  private readonly _listeners = new Set<Listener>();
  private _timer: ReturnType<typeof setTimeout> | null = null;
  private readonly _onVisibility = () => { if (document.visibilityState === 'visible' && this._listeners.size) void this.refresh(); };

  constructor() {
    // Signed out (TASK-056): nothing of the last user's list stays for the next one to see.
    authStore.onChange('isAuthenticated', (signedIn) => { if (!signedIn) this.set({ items: [], loaded: false }); });
  }

  get state(): NeedsYouState {
    return this._state;
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
      if (this._listeners.size === 0) {
        document.removeEventListener('visibilitychange', this._onVisibility);
        if (this._timer) clearTimeout(this._timer);
        this._timer = null;
      }
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
      const response = await createRestClient().get<NeedsYouItem[]>('/needs-you', { scope: 'all' });
      if (response.ok && Array.isArray(response.data)) this.set({ items: response.data, loaded: true });
    } catch {
      // Kept as it was; the next round tries again.
    }
    if (this._listeners.size && document.visibilityState === 'visible') {
      this._timer = setTimeout(() => void this.refresh(), NEEDS_YOU_REFRESH_MS);
    }
  }

  private set(patch: Partial<NeedsYouState>): void {
    const next = { ...this._state, ...patch };
    if (JSON.stringify(next) === JSON.stringify(this._state)) return;
    this._state = next;
    for (const listener of this._listeners) listener(this._state);
  }
}

/** The page's needs-you list. */
export const needsYouStore = new NeedsYouStore();
