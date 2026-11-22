/**
 * Settings → Agents (TASK-192, FEATURE-026 D20): which provider the agents use, over `/api/v1/providers` (admin only).
 * The default provider (`PUT providers/default`); the provider and model of each orchestrator — Dragon, Wyvern, Wyrm
 * and the Kobolds (`PUT providers/settings/agent`); and per-Kobold-type overrides (`PUT providers/settings/kobold`, a
 * null provider removing one). The server checks each choice (the provider exists, is enabled and serves that agent)
 * and its refusal is shown as it is.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BForm, FormSchema } from 'birko-web-components/inputs';
import { toast } from 'birko-web-components/feedback';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import type { ProviderView } from './providers-page.js';

interface UserSettings {
  dragonProvider: string | null; dragonModel: string | null;
  wyvernProvider: string | null; wyvernModel: string | null;
  wyrmProvider: string | null; wyrmModel: string | null;
  koboldProvider: string | null; koboldModel: string | null;
  koboldAgentTypeSettings: { agentType: string; provider: string | null; model: string | null }[];
}

export const ORCHESTRATORS = [
  { id: 'dragon', label: 'Dragon' },
  { id: 'wyvern', label: 'Wyvern' },
  { id: 'wyrm', label: 'Wyrm' },
  { id: 'kobold', label: 'Kobolds' },
] as const;

type Loaded = { providers: ProviderView[]; settings: UserSettings; defaultProvider: string; agentTypes: string[] };

export class AgentProvidersTab extends KoboldComponent {
  private readonly _api = createRestClient();
  private _data: Loaded | null = null;
  private _error: string | null = null;

  static get styles() {
    return `
      :host { display: block; min-width: 0; }
      section { margin: 0 0 var(--b-space-xl, 1.5rem); }
      h2 { margin: 0 0 var(--b-space-xs, 0.25rem); font-size: var(--b-text-lg, 1rem); color: var(--b-text); }
      .hint { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .actions { display: flex; justify-content: flex-end; margin-top: var(--b-space-sm, 0.5rem); }
      table { width: 100%; border-collapse: collapse; font-size: var(--b-text-sm, 0.8125rem); margin-bottom: var(--b-space-sm, 0.5rem); }
      th, td { text-align: left; padding: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem); border-bottom: 1px solid var(--b-border); color: var(--b-text); }
      th { color: var(--b-text-secondary); font-weight: var(--b-font-weight-semibold, 600); }
      td:last-child { text-align: right; }
      .empty { color: var(--b-text-muted); }
      .message { color: var(--b-text-secondary); }
    `;
  }

  protected async onMount() {
    await this.load();
  }

  /** Load again: the Settings view calls this each time the tab is opened, as providers may have changed meanwhile. */
  refresh(): Promise<void> {
    return this.load();
  }

  private async load(): Promise<void> {
    const [providers, settings, def, types] = await Promise.all([
      this._api.get<ProviderView[]>('/providers'),
      this._api.get<UserSettings>('/providers/settings'),
      this._api.get<{ defaultProvider: string }>('/providers/default'),
      this._api.get<string[]>('/providers/agent-types'),
    ]);
    if (!providers.ok || !settings.ok || !def.ok || !types.ok) {
      this._error = `The server answered ${[providers, settings, def, types].find((r) => !r.ok)!.status}.`;
    } else {
      this._error = null;
      this._data = {
        providers: providers.data ?? [], settings: settings.data!, defaultProvider: def.data?.defaultProvider ?? '',
        agentTypes: types.data ?? [],
      };
    }
    this.update();
  }

  render() {
    if (this._error) return `<p class="message">Could not load the agent settings. ${escapeHtml(this._error)}</p>`;
    if (!this._data) return '<b-spinner></b-spinner>';
    const overrides = this._data.settings.koboldAgentTypeSettings ?? [];
    return `
      <section class="default">
        <h2>Default provider</h2>
        <p class="hint">Used by any agent that has no provider of its own.</p>
        <b-form id="defaultForm"></b-form>
        <div class="actions"><b-button id="saveDefault" variant="primary" size="sm">Save default</b-button></div>
      </section>
      <section class="orchestrators">
        <h2>Orchestrators</h2>
        <p class="hint">The provider and model each layer uses. An empty model uses the provider’s default model.</p>
        <b-form id="agentsForm"></b-form>
        <div class="actions"><b-button id="saveAgents" variant="primary" size="sm">Save orchestrators</b-button></div>
      </section>
      <section class="overrides">
        <h2>Kobold overrides</h2>
        <p class="hint">A Kobold of one of these types uses its own provider instead of the Kobolds’ one.</p>
        <table>
          <thead><tr><th>Agent type</th><th>Provider</th><th>Model</th><th></th></tr></thead>
          <tbody>${overrides.length === 0
            ? '<tr><td colspan="4" class="empty">No overrides: every Kobold uses the Kobolds’ provider.</td></tr>'
            : overrides.map((o) => `<tr data-agent-type="${escapeHtml(o.agentType)}">
                <td>${escapeHtml(o.agentType)}</td><td>${escapeHtml(o.provider ?? '')}</td><td>${escapeHtml(o.model ?? '')}</td>
                <td><b-button size="sm" variant="ghost" data-remove="${escapeHtml(o.agentType)}">Remove</b-button></td>
              </tr>`).join('')}</tbody>
        </table>
        <b-form id="overrideForm"></b-form>
        <div class="actions"><b-button id="addOverride" variant="secondary" size="sm">Add override</b-button></div>
      </section>`;
  }

  /**
   * The enabled providers, and the one already chosen when it is not among them (deleted, disabled, or set before this
   * server knew it), labelled so: left out, the select would show nothing and hide that the setting is broken.
   */
  private providerOptions(current?: string | null) {
    const options = (this._data?.providers ?? []).filter((p) => p.enabled)
      .map((p) => ({ value: p.name, label: p.displayName ? `${p.displayName} (${p.name})` : p.name }));
    if (current && !options.some((o) => o.value === current)) options.push({ value: current, label: `${current} (not available)` });
    return options;
  }

  protected onUpdated() {
    const d = this._data;
    if (!d) return;
    const providers = this.providerOptions();
    const settings = d.settings as unknown as Record<string, string | null>;

    const defaultForm = this.$<BForm>('#defaultForm');
    defaultForm?.setSchema({ name: 'root', children: [
      { name: 'provider', type: 'select', label: 'Provider', options: this.providerOptions(d.defaultProvider) },
    ] } as FormSchema);
    defaultForm?.setValues({ provider: d.defaultProvider });

    const agentsForm = this.$<BForm>('#agentsForm');
    agentsForm?.setSchema({ name: 'root', children: ORCHESTRATORS.flatMap((o) => [
      { name: `${o.id}Provider`, type: 'select', label: `${o.label}: provider`, options: this.providerOptions(settings[`${o.id}Provider`]),
        clearable: true, placeholder: 'The default provider' },
      { name: `${o.id}Model`, type: 'text', label: `${o.label}: model`, placeholder: 'The provider’s default model' },
    ]) } as FormSchema);
    agentsForm?.setValues(Object.fromEntries(ORCHESTRATORS.flatMap((o) => [
      [`${o.id}Provider`, settings[`${o.id}Provider`] ?? ''],
      [`${o.id}Model`, settings[`${o.id}Model`] ?? ''],
    ])));

    const overrideForm = this.$<BForm>('#overrideForm');
    overrideForm?.setSchema({ name: 'root', children: [
      { name: 'agentType', type: 'select', label: 'Agent type', options: d.agentTypes.map((t) => ({ value: t, label: t })), searchable: true },
      { name: 'provider', type: 'select', label: 'Provider', options: providers },
      { name: 'model', type: 'text', label: 'Model', placeholder: 'The provider’s default model' },
    ] } as FormSchema);

    const on = (sel: string, fn: () => void) => { const el = this.$(sel); if (el) this.listen(el, 'click', fn); };
    on('#saveDefault', () => void this.saveDefault());
    on('#saveAgents', () => void this.saveAgents());
    on('#addOverride', () => void this.addOverride());
    const table = this.$('table');
    if (table) this.listen(table, 'click', (e) => {
      const btn = e.composedPath().find((n): n is HTMLElement => n instanceof HTMLElement && !!n.dataset?.remove);
      if (btn) void this.removeOverride(btn.dataset.remove!);
    });
  }

  /** Show the server's answer: its own words on a refusal. */
  private async report(resp: { ok: boolean; status: number; data?: unknown }, done: string): Promise<boolean> {
    if (resp.ok) {
      toast.success(done);
      return true;
    }
    const err = (resp.data as { error?: string } | undefined)?.error;
    toast.error(err ?? `The server answered ${resp.status}.`);
    return false;
  }

  private async saveDefault(): Promise<void> {
    const provider = String(this.$<BForm>('#defaultForm')?.getValues().provider ?? '');
    if (!provider) return void toast.error('Pick a provider.');
    if (await this.report(await this._api.put('/providers/default', { provider }), `Default provider set to ${provider}.`)) await this.load();
  }

  private async saveAgents(): Promise<void> {
    const d = this._data;
    const values = this.$<BForm>('#agentsForm')?.getValues() ?? {};
    if (!d) return;
    const settings = d.settings as unknown as Record<string, string | null>;
    let saved = 0;
    for (const o of ORCHESTRATORS) {
      const provider = String(values[`${o.id}Provider`] ?? '').trim();
      const model = String(values[`${o.id}Model`] ?? '').trim() || null;
      if (provider === (settings[`${o.id}Provider`] ?? '') && model === (settings[`${o.id}Model`] || null)) continue;
      if (!provider) {
        toast.error(`${o.label} needs a provider.`);
        return;
      }
      const resp = await this._api.put('/providers/settings/agent', { agentType: o.id, provider, model });
      if (!(await this.report(resp, `${o.label} now uses ${provider}${model ? ` (${model})` : ''}.`))) return;
      saved++;
    }
    if (saved === 0) toast.info('Nothing changed.');
    await this.load();
  }

  private async addOverride(): Promise<void> {
    const v = this.$<BForm>('#overrideForm')?.getValues() ?? {};
    const agentType = String(v.agentType ?? '').trim();
    const provider = String(v.provider ?? '').trim();
    const model = String(v.model ?? '').trim() || null;
    if (!agentType || !provider) return void toast.error('Pick an agent type and a provider.');
    const resp = await this._api.put('/providers/settings/kobold', { agentType, provider, model });
    if (await this.report(resp, `${agentType} Kobolds now use ${provider}.`)) await this.load();
  }

  private async removeOverride(agentType: string): Promise<void> {
    const resp = await this._api.put('/providers/settings/kobold', { agentType, provider: null, model: null });
    if (await this.report(resp, `${agentType} Kobolds use the Kobolds’ provider again.`)) await this.load();
  }
}

define('kobold-agent-providers', AgentProvidersTab);
