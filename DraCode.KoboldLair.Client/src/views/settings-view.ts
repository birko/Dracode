/**
 * Settings (TASK-161, FEATURE-026 D20), at `#/settings`: tabs for what the signed-in user may set, from
 * `GET /api/v1/settings/capabilities`. Providers (admins with `manage_config`) is the Shell list page over
 * `/api/v1/providers`; when the server keeps no provider configuration in its database the tab says how to turn it
 * on. API keys (TASK-162) and budgets (TASK-163) join as tabs of their own. The earlier settings view (on `/wyvern`
 * commands, with a General tab that saved nothing) is gone.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BTabs } from 'birko-web-components/layout';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { getToken } from '../auth-store.js';
import './providers-page.js';

export interface SettingsCapabilities {
  canManageProviders: boolean;
  providersEnabled: boolean;
  providersHint: string | null;
}

type State =
  | { kind: 'loading' }
  | { kind: 'signed-out' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; caps: SettingsCapabilities };

export class SettingsView extends KoboldComponent {
  private _state: State = { kind: 'loading' };

  static get styles() {
    return `
      :host { display: block; min-width: 0; }
      h1 { margin: 0 0 var(--b-space-lg, 1rem); font-size: var(--b-text-2xl, 1.5rem); color: var(--b-text); }
      .message {
        padding: var(--b-space-xl, 1.5rem); background: var(--b-bg-elevated); border: 1px solid var(--b-border);
        border-radius: var(--b-radius-lg, 0.5rem); color: var(--b-text-secondary); overflow-wrap: anywhere;
      }
      .message strong { display: block; margin-bottom: var(--b-space-xs, 0.25rem); color: var(--b-text); }
      .panel { padding-top: var(--b-space-md, 0.75rem); }
      code { font-family: var(--b-font-mono, monospace); font-size: 0.9em; }
    `;
  }

  protected async onMount() {
    if (!getToken()) {
      this._state = { kind: 'signed-out' };
      this.update();
      return;
    }
    try {
      const resp = await createRestClient().get<SettingsCapabilities>('/settings/capabilities');
      this._state = resp.ok && resp.data
        ? { kind: 'loaded', caps: resp.data }
        : { kind: 'error', message: `The server answered ${resp.status}.` };
    } catch (error) {
      this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    this.update();
  }

  render() {
    return `<h1>Settings</h1>${this.renderBody()}`;
  }

  private renderBody(): string {
    const s = this._state;
    switch (s.kind) {
      case 'loading':
        return '<b-spinner></b-spinner>';
      case 'signed-out':
        return '<div class="message"><strong>Sign in to change settings.</strong>Settings come from the server’s API, which needs a signed-in user.</div>';
      case 'error':
        return `<div class="message"><strong>Could not load your settings.</strong>${escapeHtml(s.message)}</div>`;
      case 'loaded':
        if (!s.caps.canManageProviders) {
          return '<div class="message"><strong>Nothing to set here for your account yet.</strong>Provider settings are for administrators.</div>';
        }
        return `<b-tabs id="settingsTabs">
          <div slot="providers" class="panel">${s.caps.providersEnabled
            ? '<kobold-providers-page></kobold-providers-page>'
            : `<div class="message providers-off"><strong>Provider management is off on this server.</strong>${escapeHtml(s.caps.providersHint ?? '')}</div>`}</div>
        </b-tabs>`;
    }
  }

  protected onUpdated() {
    const tabs = this.$<BTabs>('#settingsTabs');
    // b-tabs takes its labels from setTabs() and its panels from children slotted by tab id (TASK-112)
    if (tabs) tabs.setTabs([{ id: 'providers', label: 'Providers' }], 'providers');
  }
}

define('kobold-settings-view', SettingsView);
