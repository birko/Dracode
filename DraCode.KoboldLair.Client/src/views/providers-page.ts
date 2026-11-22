/**
 * Settings → Providers (TASK-161, FEATURE-026 D20): the LLM providers the server's agents use, on Birko.Web.Shell's
 * list page (table, create/edit modal, delete confirm) over `/api/v1/providers` (admin only; mapped only when the
 * server keeps provider configuration in its database). The API key is write-only: the form sends it to
 * `PATCH providers/{name}/key` after saving, and the table shows only whether one is set. "Validate" asks the server to
 * check the configuration and make one short request through the provider, and shows its answer. "Models" opens the
 * provider's switchable model list (TASK-192): add, enable or disable, remove (`POST|DELETE providers/{name}/models`).
 */

import { define, escapeHtml } from 'birko-web-core';
import type { ApiClient } from 'birko-web-core/http';
import type { RowAction, TableColumn } from 'birko-web-components/data';
import type { BForm, FormSchema } from 'birko-web-components/inputs';
import type { BModal } from 'birko-web-components/layout';
import { toast } from 'birko-web-components/feedback';
import { BaseListPage } from 'birko-web-shell/pages';
import { createRestClient } from '../services/http.js';

export interface ProviderModel {
  modelId: string;
  displayName: string | null;
  enabled: boolean;
  sortOrder: number;
  reasoning: boolean;
  contextWindow: number;
  inputModalities: string | null;
}

export interface ProviderView extends Record<string, unknown> {
  name: string;
  displayName: string | null;
  type: string;
  enabled: boolean;
  baseUrl: string | null;
  requiresApiKey: boolean;
  defaultModel: string | null;
  compatibleAgents: string[];
  hasKey: boolean;
  models?: ProviderModel[];
}

/** The provider types the server's LLM factory knows (Birko.AI's LlmProviderFactory registrations). */
export const PROVIDER_TYPES = [
  'azureopenai', 'claude', 'deepseek', 'gemini', 'githubcopilot', 'groq', 'llamacpp', 'lmstudio', 'mistral', 'ollama',
  'openai', 'openrouter', 'sglang', 'vllm', 'zai',
];

const yes = (v: unknown): boolean => v === true || v === 'true';

export class ProvidersPage extends BaseListPage<ProviderView> {
  protected endpoint = 'providers';
  protected override idField = 'name';
  protected override flatArray = true;
  private readonly _api = createRestClient();
  /** The key typed into the form, sent after the provider itself is saved (never part of the provider). */
  private _pendingKey: string | null = null;
  /** The provider whose models the Models dialog shows. */
  private _modelsOf: ProviderView | null = null;

  static override get styles(): string {
    return BaseListPage.styles + `
      .models-table { width: 100%; border-collapse: collapse; font-size: var(--b-text-sm, 0.8125rem); margin-bottom: var(--b-space-md, 0.75rem); }
      .models-table th, .models-table td { text-align: left; padding: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem); border-bottom: 1px solid var(--b-border); color: var(--b-text); }
      .models-table th { color: var(--b-text-secondary); }
      .models-table td.row-buttons { text-align: right; white-space: nowrap; }
      .models-empty { color: var(--b-text-muted); }
      .models-add { display: flex; justify-content: flex-end; margin-top: var(--b-space-sm, 0.5rem); }
    `;
  }

  /** The table, and the Models dialog beside the page's own create/edit modal. */
  protected override renderContent(): string {
    return super.renderContent() + `
      <b-modal id="models-modal" title="Models">
        <div class="models-body"></div>
        <b-form id="modelForm"></b-form>
        <div class="models-add"><b-button id="btn-add-model" variant="secondary" size="sm">Add model</b-button></div>
      </b-modal>`;
  }

  protected override onUpdated(): void {
    super.onUpdated();
    const body = this.$('.models-body');
    if (body) this.listen(body, 'click', (e) => {
      const btn = e.composedPath().find((n): n is HTMLElement => n instanceof HTMLElement && !!n.dataset?.modelAction);
      if (btn) void this.modelAction(btn.dataset.modelAction!, btn.dataset.model!);
    });
    const add = this.$('#btn-add-model');
    if (add) this.listen(add, 'click', () => void this.addModel());
  }

  protected override get entityLabel(): string { return 'Provider'; }
  /** The page heading names the list; the modal and toasts keep the singular entity label. */
  protected override get pageTitle(): string { return 'Providers'; }
  protected override set pageTitle(_v: string) { /* fixed */ }
  protected get api(): ApiClient { return this._api; }

  protected get columns(): TableColumn[] {
    return [
      { key: 'name', label: 'Name', sortable: true },
      { key: 'displayName', label: 'Display name' },
      { key: 'type', label: 'Type', sortable: true },
      { key: 'defaultModel', label: 'Default model' },
      { key: 'enabled', label: 'Enabled', render: (v: unknown) => (v ? 'Yes' : 'No') },
      {
        key: 'hasKey', label: 'API key',
        render: (v: unknown, row: ProviderView) => (!row.requiresApiKey ? 'Not needed' : v ? 'Set' : 'Missing'),
      },
    ];
  }

  protected get formSchema(): FormSchema { return this.schema(false); }
  protected override get editFormSchema(): FormSchema { return this.schema(true); }

  private schema(isEdit: boolean): FormSchema {
    return {
      name: 'root',
      validateOn: 'submit',
      children: [
        { name: 'name', type: 'text', label: 'Name', required: true, disabled: isEdit,
          description: isEdit ? 'The name is the provider’s key and cannot change.' : 'Agents refer to the provider by this name.' },
        { name: 'displayName', type: 'text', label: 'Display name' },
        { name: 'type', type: 'select', label: 'Type', required: true, options: PROVIDER_TYPES.map((v) => ({ value: v, label: v })) },
        { name: 'baseUrl', type: 'text', label: 'Base URL', placeholder: 'https://… (needed for local and self-hosted providers)' },
        { name: 'defaultModel', type: 'text', label: 'Default model' },
        { name: 'compatibleAgents', type: 'text', label: 'Agents it serves', placeholder: 'all',
          description: 'Comma-separated agent types, or "all".' },
        { name: 'enabled', type: 'checkbox', label: 'Enabled', value: true },
        { name: 'requiresApiKey', type: 'checkbox', label: 'Needs an API key', value: true },
        { name: 'apiKey', type: 'password', label: 'API key',
          description: isEdit ? 'Leave empty to keep the key that is set.' : 'Stored encrypted; it is never shown again.' },
      ],
    } as FormSchema;
  }

  /** A new provider starts enabled and needing a key: the page clears the form on create, so the schema's defaults are lost. */
  protected override onFormReady(form: BForm, entity: ProviderView | null): void {
    if (!entity) form.setValues({ enabled: true, requiresApiKey: true, compatibleAgents: 'all' });
  }

  protected override mapToForm(item: ProviderView): Record<string, unknown> {
    return {
      name: item.name,
      displayName: item.displayName ?? '',
      type: item.type,
      baseUrl: item.baseUrl ?? '',
      defaultModel: item.defaultModel ?? '',
      compatibleAgents: (item.compatibleAgents ?? []).join(', '),
      enabled: item.enabled,
      requiresApiKey: item.requiresApiKey,
      apiKey: '',
    };
  }

  protected override mapFromForm(data: Record<string, unknown>, isEdit: boolean): Record<string, unknown> | null {
    const text = (k: string) => String(data[k] ?? '').trim();
    const key = text('apiKey');
    this._pendingKey = key || null;
    const agents = text('compatibleAgents').split(',').map((a) => a.trim()).filter(Boolean);
    return {
      name: isEdit ? this._editingId : text('name'),
      displayName: text('displayName') || null,
      type: text('type'),
      baseUrl: text('baseUrl') || null,
      defaultModel: text('defaultModel') || null,
      compatibleAgents: agents.length ? agents : ['all'],
      enabled: yes(data.enabled),
      requiresApiKey: yes(data.requiresApiKey),
    };
  }

  protected override async afterSave(saved: ProviderView, isEdit: boolean): Promise<void> {
    const key = this._pendingKey;
    this._pendingKey = null;
    if (!key) return;
    const name = isEdit ? this._editingId! : (saved?.name as string);
    const resp = await this.api.patch(`providers/${encodeURIComponent(name)}/key`, { apiKey: key });
    if (!resp.ok) toast.error(`The provider was saved, but its API key was not (${resp.status}).`);
  }

  protected override get extraRowActions(): RowAction[] {
    return [{ id: 'validate', label: 'Validate' }, { id: 'models', label: 'Models' }];
  }

  protected override onRowAction(action: string, id: string): void {
    if (action === 'validate') void this.validate(id);
    else if (action === 'models') void this.openModels(id);
  }

  /** Open the Models dialog for a provider, with its current models. */
  private async openModels(name: string): Promise<void> {
    if (!(await this.loadModels(name))) return;
    const form = this.$<BForm>('#modelForm');
    form?.setSchema({ name: 'root', children: [
      { name: 'modelId', type: 'text', label: 'Model id', required: true, placeholder: 'e.g. claude-sonnet-4-6' },
      { name: 'displayName', type: 'text', label: 'Display name' },
    ] } as FormSchema);
    form?.setValues({ modelId: '', displayName: '' });
    this.$<BModal>('#models-modal')?.open();
  }

  private async loadModels(name: string): Promise<boolean> {
    const resp = await this.api.get<ProviderView>(`providers/${encodeURIComponent(name)}`);
    if (!resp.ok || !resp.data) {
      toast.error(`Could not load ${name}'s models (${resp.status}).`);
      return false;
    }
    this._modelsOf = resp.data;
    this.$<BModal>('#models-modal')?.setAttribute('title', `Models of ${name}`);
    const models = [...(resp.data.models ?? [])].sort((a, b) => a.sortOrder - b.sortOrder || a.modelId.localeCompare(b.modelId));
    const body = this.$('.models-body');
    if (body) body.innerHTML = `<table class="models-table">
      <thead><tr><th>Model</th><th>Display name</th><th>Enabled</th><th></th></tr></thead>
      <tbody>${models.length === 0
        ? '<tr><td colspan="4" class="models-empty">No models yet: agents use the default model.</td></tr>'
        : models.map((m) => `<tr data-model-row="${escapeHtml(m.modelId)}">
            <td>${escapeHtml(m.modelId)}</td><td>${escapeHtml(m.displayName ?? '')}</td><td>${m.enabled ? 'Yes' : 'No'}</td>
            <td class="row-buttons">
              <b-button size="sm" variant="ghost" data-model-action="toggle" data-model="${escapeHtml(m.modelId)}">${m.enabled ? 'Disable' : 'Enable'}</b-button>
              <b-button size="sm" variant="ghost" data-model-action="remove" data-model="${escapeHtml(m.modelId)}">Remove</b-button>
            </td></tr>`).join('')}</tbody>
    </table>`;
    return true;
  }

  private async addModel(): Promise<void> {
    const provider = this._modelsOf;
    const form = this.$<BForm>('#modelForm');
    if (!provider || !form) return;
    const { valid, data } = form.validate();
    if (!valid) return;
    const modelId = String(data.modelId ?? '').trim();
    const resp = await this.api.post(`providers/${encodeURIComponent(provider.name)}/models`,
      { modelId, displayName: String(data.displayName ?? '').trim() || null, enabled: true });
    if (!resp.ok) return void toast.error((resp.data as { error?: string } | undefined)?.error ?? `Could not add ${modelId} (${resp.status}).`);
    form.setValues({ modelId: '', displayName: '' });
    await this.loadModels(provider.name);
  }

  private async modelAction(action: string, modelId: string): Promise<void> {
    const provider = this._modelsOf;
    const model = provider?.models?.find((m) => m.modelId === modelId);
    if (!provider || !model) return;
    const base = `providers/${encodeURIComponent(provider.name)}/models`;
    const resp = action === 'remove'
      ? await this.api.delete(`${base}/${encodeURIComponent(modelId)}`)
      : await this.api.post(base, { ...model, enabled: !model.enabled });
    if (!resp.ok) return void toast.error(`Could not change ${modelId} (${resp.status}).`);
    await this.loadModels(provider.name);
  }

  /** Ask the server to check the provider for real, and say what it found. */
  private async validate(name: string): Promise<void> {
    toast.info(`Checking ${name}…`);
    const resp = await this.api.post<{ valid: boolean; message: string }>(`providers/${encodeURIComponent(name)}/validate`);
    if (!resp.ok || !resp.data) {
      toast.error(`Could not check ${name} (${resp.status}).`);
      return;
    }
    const text = `${name}: ${resp.data.message}`;
    if (resp.data.valid) toast.success(text);
    else toast.error(text);
  }
}

define('kobold-providers-page', ProvidersPage);
