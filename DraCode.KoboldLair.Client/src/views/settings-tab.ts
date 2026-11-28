/**
 * The workspace's Settings tab (TASK-211): the project's own settings. For now its lifecycle layer (FEATURE-078 D5), read
 * from `GET /api/v1/projects/{id}` and switched through `PATCH /api/v1/projects/{id}/lifecycle`. A refused change puts the
 * switch back and says why. Not offered on a phone, whose workspace is read only.
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';

type State = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; enabled: boolean };

export class SettingsTab extends KoboldComponent {
  private _state: State = { kind: 'loading' };
  private _saving = false;
  private _error: string | null = null;

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); }
      .message { color: var(--b-text-secondary); }
      .panel { max-width: 44rem; padding: var(--b-space-md, 0.75rem) var(--b-space-lg, 1rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem); }
      .head { display: flex; align-items: center; justify-content: space-between; gap: var(--b-space-md, 0.75rem); }
      h3 { margin: 0; font-size: var(--b-text-base, 0.9375rem); }
      .state { font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); min-width: 2rem; text-align: right; }
      .switch { display: flex; align-items: center; gap: var(--b-space-sm, 0.5rem); }
      p.what { margin: var(--b-space-sm, 0.5rem) 0 0; font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      ul { margin: var(--b-space-xs, 0.25rem) 0 0; padding-left: 1.25rem; font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .error { margin: var(--b-space-sm, 0.5rem) 0 0; font-size: var(--b-text-sm, 0.8125rem); color: var(--b-color-danger); }
    `;
  }

  private get projectId(): string {
    return this.attr('project-id') ?? '';
  }

  protected onMount() {
    void this.load();
  }

  async load(): Promise<void> {
    try {
      const response = await createRestClient().get<{ lifecycleEnabled?: boolean }>(`/projects/${encodeURIComponent(this.projectId)}`);
      this._state = response.ok && response.data
        ? { kind: 'loaded', enabled: response.data.lifecycleEnabled === true }
        : { kind: 'error', message: `The server answered ${response.status}.` };
    } catch (error) {
      this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    if (this.isConnected) this.update();
  }

  private async setEnabled(enabled: boolean): Promise<void> {
    if (this._state.kind !== 'loaded' || this._saving) return;
    this._saving = true;
    this._error = null;
    try {
      const response = await createRestClient().patch(`/projects/${encodeURIComponent(this.projectId)}/lifecycle`, { enabled });
      if (response.ok) this._state = { kind: 'loaded', enabled };
      else this._error = `Could not change it: the server answered ${response.status}.`;
    } catch (error) {
      this._error = `Could not change it: ${error instanceof Error ? error.message : String(error)}`;
    }
    this._saving = false;
    if (this.isConnected) this.update();
  }

  render() {
    const s = this._state;
    if (s.kind === 'loading') return `<b-spinner></b-spinner>`;
    if (s.kind === 'error') return `<p class="message"><strong>Could not load the project's settings.</strong> ${escapeHtml(s.message)}</p>`;
    return `
      <section class="panel">
        <div class="head">
          <h3 id="lifecycle-title">Lifecycle layer</h3>
          <div class="switch">
            <span class="state">${s.enabled ? 'On' : 'Off'}</span>
            <b-switch class="lifecycle" name="lifecycle" aria-labelledby="lifecycle-title"${s.enabled ? ' checked' : ''}${this._saving ? ' disabled' : ''}></b-switch>
          </div>
        </div>
        <p class="what">When it is on, a finished task is held to more than its own promises:</p>
        <ul>
          <li>checker agents confirm it built what was asked, look for likely bugs and for stale comments;</li>
          <li>a task that keeps failing its checks comes to you instead of failing;</li>
          <li>repeated failures and monitor alerts become tracked fix tasks.</li>
        </ul>
        <p class="what">Questions climb the agent chain either way. Turning it off removes nothing already recorded.</p>
        ${this._error ? `<p class="error" role="alert">${escapeHtml(this._error)}</p>` : ''}
      </section>`;
  }

  protected onUpdated() {
    const toggle = this.$('b-switch.lifecycle');
    if (toggle) this.listen<CustomEvent<{ checked: boolean }>>(toggle, 'change', (e) => void this.setEnabled(e.detail.checked));
  }
}

define('kobold-settings-tab', SettingsTab);
