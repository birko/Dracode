/**
 * Settings → API keys (TASK-162, FEATURE-026 D6): the signed-in user's service-account keys, on Birko.Web.Shell's list
 * page over `/api/v1/keys`. A key is for scripts and the CLI: it acts as `service:<name>`, with view_own,
 * manage_projects and execute_agents narrowed to what its creator has. Creating one shows its client id and secret once,
 * with Copy buttons; they are never shown again. "Revoke" disables it (after a confirm), and its next token request is
 * refused. Keys cannot be edited or deleted, only revoked.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { ApiClient } from 'birko-web-core/http';
import type { RowAction, TableColumn } from 'birko-web-components/data';
import type { FormSchema } from 'birko-web-components/inputs';
import type { BModal, BConfirmDialog } from 'birko-web-components/layout';
import { toast } from 'birko-web-components/feedback';
import { BaseListPage } from 'birko-web-shell/pages';
import { createRestClient } from '../services/http.js';

export interface ApiKeyView extends Record<string, unknown> {
  clientId: string;
  name: string;
  ownerId: string;
  scopes: string[];
  createdAt: string;
  disabled: boolean;
  /** Only in the answer to the create call. */
  clientSecret?: string;
}

export class ApiKeysPage extends BaseListPage<ApiKeyView> {
  protected endpoint = 'keys';
  protected override idField = 'clientId';
  protected override flatArray = true;
  protected override editEnabled = false;
  protected override deleteEnabled = false;
  private readonly _api = createRestClient();

  protected override get entityLabel(): string { return 'API key'; }
  protected override get pageTitle(): string { return 'API keys'; }
  protected override set pageTitle(_v: string) { /* fixed */ }
  protected get api(): ApiClient { return this._api; }

  static override get styles(): string {
    return BaseListPage.styles + `
      .secret-intro { margin: 0 0 var(--b-space-md, 0.75rem); color: var(--b-text-secondary); }
      .secret-row { display: flex; align-items: center; gap: var(--b-space-sm, 0.5rem); margin-bottom: var(--b-space-sm, 0.5rem); min-width: 0; }
      .secret-row label { flex: 0 0 6rem; font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .secret-row code {
        flex: 1; min-width: 0; padding: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem); overflow-wrap: anywhere;
        font-family: var(--b-font-mono, monospace); font-size: var(--b-text-sm, 0.8125rem);
        background: var(--b-bg-secondary, var(--b-bg)); border: 1px solid var(--b-border); border-radius: var(--b-radius, 0.375rem);
      }
      .secret-usage { margin-top: var(--b-space-md, 0.75rem); font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
    `;
  }

  protected get columns(): TableColumn[] {
    return [
      { key: 'name', label: 'Name', sortable: true },
      { key: 'clientId', label: 'Client id' },
      { key: 'scopes', label: 'Can', render: (v: unknown) => escapeHtml((v as string[] ?? []).join(', ')) },
      { key: 'createdAt', label: 'Created', render: (v: unknown) => escapeHtml(new Date(String(v)).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })) },
      { key: 'disabled', label: 'Status', render: (v: unknown) => (v ? 'Revoked' : 'Active') },
    ];
  }

  protected get formSchema(): FormSchema {
    return {
      name: 'root',
      validateOn: 'submit',
      children: [
        { name: 'name', type: 'text', label: 'Name', required: true, placeholder: 'e.g. my-ci-runner',
          description: 'Unique, no spaces. The key acts as service:<name> and owns the projects it creates.' },
      ],
    } as FormSchema;
  }

  protected override mapFromForm(data: Record<string, unknown>): Record<string, unknown> | null {
    return { name: String(data.name ?? '').trim() };
  }

  /** The answer to the create call is the only time the secret exists outside the server's hash: show it now. */
  protected override async afterSave(saved: ApiKeyView): Promise<void> {
    if (!saved?.clientSecret) return;
    const body = this.$('.secret-body');
    if (body) body.innerHTML = `
      <p class="secret-intro">Copy the secret now: it is shown this once, and cannot be shown again.</p>
      <div class="secret-row"><label>Client id</label><code class="client-id">${escapeHtml(saved.clientId)}</code>
        <b-button size="sm" variant="secondary" data-copy="${escapeHtml(saved.clientId)}">Copy</b-button></div>
      <div class="secret-row"><label>Secret</label><code class="client-secret">${escapeHtml(saved.clientSecret)}</code>
        <b-button size="sm" variant="primary" data-copy="${escapeHtml(saved.clientSecret)}">Copy</b-button></div>
      <p class="secret-usage">Exchange them for a token: POST /token with grant_type=client_credentials, client_id and client_secret.</p>`;
    this.$<BModal>('#secret-modal')?.open();
  }

  protected override renderContent(): string {
    return super.renderContent() + `
      <b-modal id="secret-modal" title="Your new API key"><div class="secret-body"></div></b-modal>
      <b-confirm-dialog id="revoke-confirm" title="Revoke this key?" confirm-text="Revoke" variant="danger"
        message="Scripts and the CLI using it stop working at their next token request. This cannot be undone."></b-confirm-dialog>`;
  }

  protected override onUpdated(): void {
    super.onUpdated();
    const body = this.$('.secret-body');
    if (body) this.listen(body, 'click', (e) => {
      const btn = e.composedPath().find((n): n is HTMLElement => n instanceof HTMLElement && n.dataset?.copy !== undefined);
      if (btn) void this.copy(btn.dataset.copy!);
    });
    const secretModal = this.$('#secret-modal');
    // Closing the dialog forgets the secret: it is not kept on the page
    if (secretModal) this.listen(secretModal, 'close', () => { const b = this.$('.secret-body'); if (b) b.innerHTML = ''; });
  }

  private async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      toast.success('Copied.');
    } catch {
      toast.error('Could not copy: select the text and copy it by hand.');
    }
  }

  protected override get extraRowActions(): RowAction[] {
    return [{ id: 'revoke', label: 'Revoke', variant: 'danger' }];
  }

  protected override onRowAction(action: string, _id: string, row: ApiKeyView): void {
    if (action !== 'revoke') return;
    if (row.disabled) return void toast.info(`${row.name} is already revoked.`);
    void this.revoke(row);
  }

  private async revoke(key: ApiKeyView): Promise<void> {
    if (!(await this.$<BConfirmDialog>('#revoke-confirm')?.show())) return;
    const resp = await this.api.post(`keys/${encodeURIComponent(key.clientId)}/revoke`);
    if (!resp.ok) return void toast.error(`Could not revoke ${key.name} (${resp.status}).`);
    toast.success(`${key.name} is revoked.`);
    await this.reload();
  }
}

define('kobold-api-keys-page', ApiKeysPage);
