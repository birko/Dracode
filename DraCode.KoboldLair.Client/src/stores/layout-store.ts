/**
 * The person's workspace layouts (TASK-058), each person's own across their projects, from `/api/v1/layouts`. A layout
 * has the shape of Birko's `LayoutState` (tab orders and split sizes by `layout-key`), plus the tab it opens on and the
 * Dragon pin. The current one is read once per page and saved as the person changes it; the named ones are read when
 * the Layouts menu needs them. A server that cannot keep layouts (no SQLite, no identity) still answers reads, so the
 * workspace simply starts from its defaults.
 */

import type { SplitSizes } from 'birko-web-components/layout';
import { createRestClient } from '../services/http.js';
import { getToken } from '../auth-store.js';

export const WORKSPACE_KEY = 'workspace';

export interface WorkspaceLayout {
  tabs?: Record<string, string[]>;
  splits?: Record<string, SplitSizes>;
  /** The tab a saved layout opens on; not part of the current layout, which the URL decides. */
  openTab?: string;
  dragonPinned?: boolean;
}

export interface SavedLayout { name: string; layout: WorkspaceLayout; updatedAt: string }

export type SaveResult = { ok: true; layouts: SavedLayout[] } | { ok: false; error: string };

/** How long the current layout waits after a change before it is saved, so a drag or a run of key presses saves once. */
const SAVE_DELAY_MS = 400;

class LayoutStore {
  private _current: WorkspaceLayout = {};
  private _loading: Promise<WorkspaceLayout> | null = null;
  private _timer: ReturnType<typeof setTimeout> | null = null;

  /** The current layout as last read or changed; `{}` before {@link load}. */
  get current(): WorkspaceLayout {
    return this._current;
  }

  /** Read the current layout once per page (signed out, or when the read fails, the defaults). */
  load(): Promise<WorkspaceLayout> {
    if (!getToken()) return Promise.resolve(this._current);
    this._loading ??= (async () => {
      try {
        const response = await createRestClient().get<{ layout: WorkspaceLayout | null }>('/layouts/current');
        if (response.ok && response.data?.layout && typeof response.data.layout === 'object') {
          this._current = { ...response.data.layout, ...this._current };
        }
      } catch {
        // No layouts: the workspace keeps its defaults
      }
      return this._current;
    })();
    return this._loading;
  }

  /** Change the current layout and save it shortly (a change made before the first read wins over what it reads). */
  update(change: WorkspaceLayout): void {
    this._current = {
      ...this._current,
      ...change,
      tabs: change.tabs ? { ...this._current.tabs, ...change.tabs } : this._current.tabs,
      splits: change.splits ? { ...this._current.splits, ...change.splits } : this._current.splits,
    };
    delete this._current.openTab;
    if (!getToken()) return;
    if (this._timer) clearTimeout(this._timer);
    this._timer = setTimeout(() => {
      this._timer = null;
      void createRestClient().put('/layouts/current', this._current).catch(() => undefined);
    }, SAVE_DELAY_MS);
  }

  /** The person's named layouts, by name; null when they cannot be read. */
  async list(): Promise<SavedLayout[] | null> {
    try {
      const response = await createRestClient().get<SavedLayout[]>('/layouts');
      return response.ok && Array.isArray(response.data) ? response.data : null;
    } catch {
      return null;
    }
  }

  /** Save `layout` under `name` (replacing one of that name); the server's reason when it refuses. */
  async save(name: string, layout: WorkspaceLayout): Promise<SaveResult> {
    try {
      const response = await createRestClient().put<{ error?: string }>(`/layouts/${encodeURIComponent(name)}`, layout);
      if (!response.ok) return { ok: false, error: response.data?.error ?? `The server answered ${response.status}.` };
    } catch (error) {
      return { ok: false, error: error instanceof Error ? error.message : String(error) };
    }
    return { ok: true, layouts: (await this.list()) ?? [] };
  }

  async remove(name: string): Promise<SaveResult> {
    try {
      const response = await createRestClient().delete(`/layouts/${encodeURIComponent(name)}`);
      if (!response.ok) return { ok: false, error: response.status === 404 ? 'That layout is already gone.' : `The server answered ${response.status}.` };
    } catch (error) {
      return { ok: false, error: error instanceof Error ? error.message : String(error) };
    }
    return { ok: true, layouts: (await this.list()) ?? [] };
  }
}

export const layoutStore = new LayoutStore();
