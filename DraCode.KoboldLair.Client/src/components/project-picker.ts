/**
 * The header's project picker (TASK-146, FEATURE-026 D4/D10): inside a project (`#/projects/{id}/{tab}`) the header shows
 * the project's name as Birko's searchable `b-select`, listing the caller's projects grouped under their status. Typing
 * filters (case- and accent-insensitive); choosing one opens it on the same tab. Anywhere else it renders nothing.
 */

import { define } from 'birko-web-core';
import type { BSelect } from 'birko-web-components/inputs';
import { KoboldComponent } from './kobold-component.js';
import { createRestClient } from '../services/http.js';
import { getToken } from '../auth-store.js';
import type { ProjectSummary } from '../views/home-view.js';

/** A project route: `#/projects/{id}` with an optional tab. */
function projectRoute(hash: string): { id: string; tab: string | null } | null {
  const match = /^#\/projects\/([^/?#]+)(?:\/([^/?#]+))?/.exec(hash);
  return match ? { id: decodeURIComponent(match[1]), tab: match[2] ? decodeURIComponent(match[2]) : null } : null;
}

export class ProjectPicker extends KoboldComponent {
  private _projects: ProjectSummary[] = [];
  private _route = projectRoute(window.location.hash);
  private readonly _onHash = () => this.follow();

  static get styles() {
    return `
      :host { display: block; min-width: 0; width: 16rem; max-width: 100%; }
      b-select { display: block; min-width: 0; }
    `;
  }

  protected onMount() {
    window.addEventListener('hashchange', this._onHash);
    void this.load();
  }

  protected onUnmount() {
    window.removeEventListener('hashchange', this._onHash);
  }

  render() {
    if (!this._route || !getToken()) return '';
    return `<b-select searchable bare no-clear label="Project" label-no-matches="No project matches"></b-select>`;
  }

  protected onUpdated() {
    const select = this.$('b-select') as BSelect | null;
    if (!select) return;
    this.fill(select);
    this.listen<CustomEvent<{ value: string }>>(select, 'change', (e) => this.open(e.detail.value));
  }

  /** The route changed: show or hide the picker, and reload when the project is one the list does not have yet. */
  private follow(): void {
    const before = this._route;
    this._route = projectRoute(window.location.hash);
    if (!!before !== !!this._route) this.update();
    else if (this._route) {
      const select = this.$('b-select') as BSelect | null;
      if (select) this.fill(select);
    }
    if (this._route && !this._projects.some((p) => p.id === this._route!.id)) void this.load();
  }

  /** The caller's projects (an admin's `scope=all` widens it, as on the homepage). */
  private async load(): Promise<void> {
    if (!this._route || !getToken()) return;
    try {
      const response = await createRestClient().get<ProjectSummary[]>('/projects', { scope: 'all' });
      if (response.ok && Array.isArray(response.data)) this._projects = response.data;
    } catch {
      // The picker keeps what it had; the workspace says when the server is unreachable.
    }
    if (!this.isConnected) return;
    const select = this.$('b-select') as BSelect | null;
    if (select) this.fill(select);
    else this.update();
  }

  private fill(select: BSelect): void {
    const sorted = [...this._projects].sort((a, b) => a.status.localeCompare(b.status) || a.name.localeCompare(b.name));
    select.setOptions(sorted.map((p) => ({ value: p.id, label: p.name, group: p.status })));
    if (this._route) select.setAttribute('value', this._route.id);
  }

  /** Open the chosen project on the tab the user is on. */
  private open(id: string): void {
    if (!id || !this._route || id === this._route.id) return;
    const tab = this._route.tab ? `/${encodeURIComponent(this._route.tab)}` : '';
    window.location.hash = `#/projects/${encodeURIComponent(id)}${tab}`;
  }
}

define('kobold-project-picker', ProjectPicker);
