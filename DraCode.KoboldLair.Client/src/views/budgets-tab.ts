/**
 * Settings → Budgets (TASK-163, FEATURE-026 D20), admin only: the LLM spending budgets from `GET /api/v1/budgets`, each
 * beside this period's spend — the daily budget against today's, the monthly against this month's (UTC, as they are
 * enforced) and the per-project budget against the project that has spent most — in the warning colour past the
 * threshold and the danger colour past the budget. A form saves all four values (`PUT /api/v1/budgets`), which the
 * server enforces from the next LLM call; its refusal is shown as it is. A server that keeps no database cannot save
 * them, and the tab says so instead of offering the form.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BForm, FormSchema } from 'birko-web-components/inputs';
import { toast } from 'birko-web-components/feedback';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { money } from './stats-tab.js';

export interface Budgets {
  dailyBudgetUsd: number;
  monthlyBudgetUsd: number;
  projectBudgetUsd: number;
  warningThresholdPercent: number;
  editable: boolean;
  spend: { todayUsd: number; thisMonthUsd: number; topProject: { projectId: string; projectName: string | null; spentUsd: number } | null };
}

const FIELDS = ['dailyBudgetUsd', 'monthlyBudgetUsd', 'projectBudgetUsd', 'warningThresholdPercent'] as const;

export class BudgetsTab extends KoboldComponent {
  private readonly _api = createRestClient();
  private _budgets: Budgets | null = null;
  private _error: string | null = null;
  private _saving = false;

  static get styles() {
    return `
      :host { display: block; min-width: 0; max-width: 48rem; }
      h2 { margin: 0 0 var(--b-space-xs, 0.25rem); font-size: var(--b-text-lg, 1rem); color: var(--b-text); }
      .hint { margin: 0 0 var(--b-space-md, 0.75rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .budgets { display: flex; flex-direction: column; gap: var(--b-space-sm, 0.5rem); margin-bottom: var(--b-space-xl, 1.5rem); }
      .budget {
        padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem); background: var(--b-bg-elevated);
        border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem); font-size: var(--b-text-sm, 0.8125rem);
      }
      .line { display: flex; flex-wrap: wrap; justify-content: space-between; gap: var(--b-space-xs, 0.25rem);
        margin-bottom: var(--b-space-xs, 0.25rem); color: var(--b-text); }
      .name { font-weight: var(--b-font-weight-semibold, 600); }
      .figures { color: var(--b-text-secondary); overflow-wrap: anywhere; }
      .over { color: var(--b-color-danger); font-weight: var(--b-font-weight-semibold, 600); }
      .actions { display: flex; justify-content: flex-end; margin-top: var(--b-space-sm, 0.5rem); }
      .read-only, .message { color: var(--b-text-secondary); font-size: var(--b-text-sm, 0.8125rem); }
    `;
  }

  protected async onMount() {
    await this.load();
  }

  /** Load again: the Settings view calls this each time the tab is opened, as the spend moves on. */
  refresh(): Promise<void> {
    return this.load();
  }

  private async load(): Promise<void> {
    try {
      const response = await this._api.get<Budgets>('/budgets');
      if (response.ok && response.data) {
        this._budgets = response.data;
        this._error = null;
      } else {
        this._error = `The server answered ${response.status}.`;
      }
    } catch (error) {
      this._error = error instanceof Error ? error.message : String(error);
    }
    if (this.isConnected) this.update();
  }

  render() {
    if (this._error && !this._budgets) return `<p class="message">Could not load the budgets. ${escapeHtml(this._error)}</p>`;
    const b = this._budgets;
    if (!b) return '<b-spinner></b-spinner>';
    const top = b.spend.topProject;
    const rows = [
      this.row('daily', 'Daily', b.dailyBudgetUsd, b.spend.todayUsd, 'today', `No daily budget. ${money(b.spend.todayUsd)} spent today.`),
      this.row('monthly', 'Monthly', b.monthlyBudgetUsd, b.spend.thisMonthUsd, 'this month',
        `No monthly budget. ${money(b.spend.thisMonthUsd)} spent this month.`),
      this.row('project', 'Per project', b.projectBudgetUsd, top?.spentUsd ?? 0,
        top ? `by ${top.projectName ?? top.projectId}, the most of any project` : 'by any project yet',
        top ? `No project budget. ${top.projectName ?? top.projectId} has spent the most: ${money(top.spentUsd)}.` : 'No project budget.'),
    ].join('');
    return `
      <h2>Budgets</h2>
      <p class="hint">LLM calls stop once a budget is spent: daily and monthly across all projects (UTC), per project over
        its whole life. 0 means no budget. A warning is shown from the threshold on.</p>
      <div class="budgets">${rows}</div>
      ${b.editable
        ? `<b-form id="budgetForm"></b-form>
           <div class="actions"><b-button id="saveBudgets" variant="primary" size="sm"${this._saving ? ' loading' : ''}>Save budgets</b-button></div>`
        : `<p class="read-only">Budgets cannot be saved on this server: it keeps no database. They come from
             <code>KoboldLair:CostTracking:Budget</code> in appsettings.</p>`}`;
  }

  /** One budget beside its spend; `none` when it is 0 (no budget). */
  private row(kind: string, name: string, limit: number, spent: number, period: string, none: string): string {
    if (!(limit > 0)) {
      return `<div class="budget ${kind}"><div class="line"><span class="name">${name}</span>
        <span class="figures">${escapeHtml(none)}</span></div></div>`;
    }
    const percent = Math.round((100 * spent) / limit);
    const threshold = this._budgets?.warningThresholdPercent ?? 80;
    const variant = percent >= 100 ? 'danger' : percent >= threshold ? 'warning' : 'success';
    return `<div class="budget ${kind}">
      <div class="line"><span class="name">${name}${percent >= 100 ? ' <span class="over">Over budget</span>' : ''}</span>
        <span class="figures">${escapeHtml(`${money(spent)} of ${money(limit)} ${period} (${percent}%)`)}</span></div>
      <b-progress value="${Math.min(100, percent)}" max="100" size="sm" variant="${variant}"></b-progress>
    </div>`;
  }

  protected onUpdated() {
    const b = this._budgets;
    const form = this.$<BForm>('#budgetForm');
    if (!b || !form) return;
    if (!(form as BForm & { klSchema?: boolean }).klSchema) {
      (form as BForm & { klSchema?: boolean }).klSchema = true;
      form.setSchema({ name: 'root', children: [
        { name: 'dailyBudgetUsd', type: 'decimal', label: 'Daily budget (USD)', min: 0, hint: '0 for none' },
        { name: 'monthlyBudgetUsd', type: 'decimal', label: 'Monthly budget (USD)', min: 0, hint: '0 for none' },
        { name: 'projectBudgetUsd', type: 'decimal', label: 'Budget per project (USD)', min: 0, hint: '0 for none' },
        { name: 'warningThresholdPercent', type: 'decimal', label: 'Warn from (% of a budget)', min: 1, max: 100 },
      ] } as FormSchema);
      form.setValues(Object.fromEntries(FIELDS.map((f) => [f, b[f]])));
    }
    const save = this.$('#saveBudgets');
    if (save) this.listen(save, 'click', () => void this.save());
  }

  private async save(): Promise<void> {
    const values = this.$<BForm>('#budgetForm')?.getValues() ?? {};
    const change = Object.fromEntries(FIELDS.map((f) => [f, Number(values[f] ?? 0)]));
    if (FIELDS.some((f) => !Number.isFinite(change[f]))) return void toast.error('Each budget is a number.');
    this._saving = true;
    this.update();
    try {
      const response = await this._api.put<Budgets>('/budgets', change);
      if (response.ok && response.data) {
        this._budgets = response.data;
        toast.success('Budgets saved. They apply from the next LLM call.');
      } else {
        toast.error((response.data as { error?: string } | undefined)?.error ?? `The server answered ${response.status}.`);
      }
    } finally {
      this._saving = false;
      this.update();
    }
  }
}

define('kobold-budgets-tab', BudgetsTab);
