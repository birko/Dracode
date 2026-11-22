/**
 * Settings → Providers (TASK-161, FEATURE-026 D20): the LLM providers the server's agents use, on Birko.Web.Shell's
 * list page (table, create/edit modal, delete confirm) over `/api/v1/providers` (admin only; mapped only when the
 * server keeps provider configuration in its database). The API key is write-only: the form sends it to
 * `PATCH providers/{name}/key` after saving, and the table shows only whether one is set. "Validate" asks the server to
 * check the configuration and make one short request through the provider, and shows its answer.
 */

import { define } from 'birko-web-core';
import type { ApiClient } from 'birko-web-core/http';
import type { RowAction, TableColumn } from 'birko-web-components/data';
import type { BForm, FormSchema } from 'birko-web-components/inputs';
import { toast } from 'birko-web-components/feedback';
import { BaseListPage } from 'birko-web-shell/pages';
import { createRestClient } from '../services/http.js';

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
    return [{ id: 'validate', label: 'Validate' }];
  }

  protected override onRowAction(action: string, id: string): void {
    if (action === 'validate') void this.validate(id);
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
