/**
 * The workspace's Stats tab (TASK-158, FEATURE-026 D18): the project's numbers from `GET /api/v1/projects/{id}/stats` —
 * `b-stat` tiles for tasks, success rate, average task duration, spend and tokens, spend against the project budget, and
 * `b-chart` charts of tasks by status, tasks by agent type (done, failed, the rest), cost by day and cost by provider and
 * model. A project with nothing yet shows dashes and says so in place of each chart. Loaded when the tab opens and on
 * Refresh.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BChart } from 'birko-web-components/data';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';

export interface ProjectStats {
  tasks: {
    total: number;
    byStatus: Record<string, number>;
    byAgent: Array<{ agentType: string; total: number; done: number; failed: number }>;
    successRate: number | null;
    averageDurationSeconds: number | null;
    timedTasks: number;
  };
  cost: {
    totalUsd: number;
    totalTokens: number;
    requests: number;
    byModel: Array<{ provider: string; model: string; requests: number; tokens: number; costUsd: number }>;
    byDay: Array<{ date: string; requests: number; tokens: number; costUsd: number }>;
  };
  budget: { projectBudgetUsd: number | null; spentUsd: number; percentUsed: number | null; warningThresholdPercent: number };
}

type State = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; stats: ProjectStats };

/** Task statuses in the order a person reads them, with their labels. */
const STATUSES: Array<[string, string]> = [
  ['Unassigned', 'To do'], ['NotInitialized', 'Starting'], ['Working', 'Working'], ['AwaitingHumanDecision', 'Waiting on you'],
  ['BlockedByFailure', 'Blocked'], ['Failed', 'Failed'], ['Done', 'Done'],
];

export const money = (usd: number) => (usd >= 100 ? `$${usd.toFixed(0)}` : usd >= 1 ? `$${usd.toFixed(2)}` : usd > 0 ? `$${usd.toFixed(4)}` : '$0');
export const duration = (seconds: number) => {
  if (seconds < 60) return `${Math.round(seconds)} s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ${Math.round(seconds % 60)} s`;
  return `${Math.floor(minutes / 60)} h ${minutes % 60} min`;
};
const count = (n: number) => n.toLocaleString();

export class StatsTab extends KoboldComponent {
  private _state: State = { kind: 'loading' };

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); container-type: inline-size; }
      .message { color: var(--b-text-secondary); }
      .bar { display: flex; justify-content: flex-end; margin-bottom: var(--b-space-sm, 0.5rem); }
      .tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 10rem), 1fr)); gap: var(--b-space-sm, 0.5rem);
        margin-bottom: var(--b-space-lg, 1rem); }
      .tiles b-stat { min-width: 0; }
      .budget { margin-bottom: var(--b-space-lg, 1rem); padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); }
      .budget .line { display: flex; flex-wrap: wrap; justify-content: space-between; gap: var(--b-space-xs, 0.25rem); margin-bottom: var(--b-space-xs, 0.25rem); }
      .charts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--b-space-lg, 1rem); }
      @container (max-width: 44rem) { .charts { grid-template-columns: minmax(0, 1fr); } }
      .panel { min-width: 0; padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem); }
      .panel h3 { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .panel b-chart { display: block; min-width: 0; }
      .empty { margin: var(--b-space-md, 0.75rem) 0; font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-muted); }
      .note { margin: var(--b-space-xs, 0.25rem) 0 0; font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
    `;
  }

  /** Calls recorded with no price set: the cost charts would be flat, so they chart tokens instead. */
  private get unpriced(): boolean {
    return this._state.kind === 'loaded' && this._state.stats.cost.requests > 0 && this._state.stats.cost.totalUsd === 0;
  }

  protected onMount() {
    void this.load();
  }

  async load(): Promise<void> {
    try {
      const response = await createRestClient().get<ProjectStats>(`/projects/${encodeURIComponent(this.attr('project-id') ?? '')}/stats`);
      this._state = response.ok && response.data
        ? { kind: 'loaded', stats: response.data }
        : { kind: 'error', message: `The server answered ${response.status}.` };
    } catch (error) {
      this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    if (this.isConnected) this.update();
  }

  render() {
    const s = this._state;
    if (s.kind === 'loading') return `<b-spinner></b-spinner>`;
    if (s.kind === 'error') return `<p class="message"><strong>Could not load the numbers.</strong> ${escapeHtml(s.message)}</p>`;
    const { tasks, cost, budget } = s.stats;
    const finished = (tasks.byStatus.Done ?? 0) + (tasks.byStatus.Failed ?? 0);
    const tiles = [
      `<b-stat class="tasks" label="Tasks" value="${count(tasks.total)}"></b-stat>`,
      `<b-stat class="done" label="Done" value="${count(tasks.byStatus.Done ?? 0)}"></b-stat>`,
      `<b-stat class="success" label="Success rate" value="${tasks.successRate === null ? '—' : `${tasks.successRate}%`}"
        ${tasks.successRate === null ? '' : `delta="${finished} finished" trend="flat" sentiment="neutral"`}></b-stat>`,
      `<b-stat class="duration" label="Average task" value="${tasks.averageDurationSeconds === null ? '—' : escapeHtml(duration(tasks.averageDurationSeconds))}"
        ${tasks.timedTasks ? `delta="${tasks.timedTasks} timed" trend="flat" sentiment="neutral"` : ''}></b-stat>`,
      `<b-stat class="spend" label="Spend" value="${escapeHtml(money(cost.totalUsd))}"></b-stat>`,
      `<b-stat class="tokens" label="Tokens" value="${count(cost.totalTokens)}" delta="${count(cost.requests)} calls" trend="flat" sentiment="neutral"></b-stat>`,
    ].join('');
    const over = budget.percentUsed !== null && budget.percentUsed >= budget.warningThresholdPercent;
    const budgetHtml = budget.projectBudgetUsd === null
      ? `<div class="budget"><div class="line"><span>Budget</span><span>No project budget is set (<code>CostTracking.Budget.ProjectBudgetUsd</code>).</span></div></div>`
      : `<div class="budget${over ? ' over' : ''}"><div class="line"><span>Budget</span>
          <span class="budget-text">${escapeHtml(money(budget.spentUsd))} of ${escapeHtml(money(budget.projectBudgetUsd))} (${budget.percentUsed}%)</span></div>
          <b-progress value="${Math.min(100, budget.percentUsed ?? 0)}" max="100" size="sm" variant="${(budget.percentUsed ?? 0) >= 100 ? 'danger' : over ? 'warning' : 'success'}"></b-progress></div>`;
    const zeroCost = cost.requests > 0 && cost.totalUsd === 0
      ? `<p class="note">Calls were recorded but cost $0: prices are set in <code>CostTracking.Pricing</code>. The charts show tokens until then.</p>` : '';
    const panel = (id: string, title: string, has: boolean, emptyText: string, extra = '') => `<section class="panel ${id}">
        <h3>${title}</h3>${has ? `<b-chart class="${id}-chart" type="bar" height="200"></b-chart>` : `<p class="empty">${emptyText}</p>`}${extra}</section>`;
    return `
      <div class="bar"><b-button class="refresh" size="sm" variant="ghost">Refresh</b-button></div>
      <div class="tiles">${tiles}</div>
      ${budgetHtml}
      <div class="charts">
        ${panel('by-status', 'Tasks by status', tasks.total > 0, 'No tasks yet.')}
        ${panel('by-agent', 'Tasks by agent type', tasks.byAgent.length > 0, 'No tasks yet.')}
        ${panel('by-day', `${this.unpriced ? 'Tokens' : 'Cost'} by day`, cost.byDay.length > 0, 'No LLM usage recorded yet.', zeroCost)}
        ${panel('by-model', `${this.unpriced ? 'Tokens' : 'Cost'} by provider and model`, cost.byModel.length > 0, 'No LLM usage recorded yet.')}
      </div>`;
  }

  protected onUpdated() {
    const refresh = this.$('b-button.refresh');
    if (refresh) this.listen(refresh, 'click', () => void this.load());
    if (this._state.kind !== 'loaded') return;
    const { tasks, cost } = this._state.stats;
    const chart = (selector: string) => this.$(selector) as BChart | null;

    // Only the statuses that have tasks: an empty slot says nothing, and seven labels crowd a phone-width chart
    const statuses = STATUSES.filter(([status]) => (tasks.byStatus[status] ?? 0) > 0);
    chart('.by-status-chart')?.setData({
      labels: statuses.map(([, label]) => label),
      series: [{ id: 'tasks', label: 'Tasks', data: statuses.map(([status]) => ({
        y: tasks.byStatus[status] ?? 0,
        color: status === 'Done' ? 'var(--b-color-success)' : status === 'Failed' ? 'var(--b-color-danger)' : undefined,
      })) }],
    });
    const byAgent = chart('.by-agent-chart');
    if (byAgent) {
      byAgent.setData({
        labels: tasks.byAgent.map((a) => a.agentType),
        series: [
          { id: 'done', label: 'Done', color: 'var(--b-color-success)', data: tasks.byAgent.map((a) => ({ y: a.done })) },
          { id: 'failed', label: 'Failed', color: 'var(--b-color-danger)', data: tasks.byAgent.map((a) => ({ y: a.failed })) },
          { id: 'other', label: 'Other', color: 'var(--b-color-primary)', data: tasks.byAgent.map((a) => ({ y: a.total - a.done - a.failed })) },
        ],
      });
      byAgent.setOptions({ stacked: true });
    }
    const tokens = this.unpriced;
    chart('.by-day-chart')?.setData({
      labels: cost.byDay.map((d) => d.date.slice(5)),
      series: [tokens
        ? { id: 'tokens', label: 'Tokens', data: cost.byDay.map((d) => ({ y: d.tokens, label: count(d.tokens) })) }
        : { id: 'cost', label: 'Cost (USD)', data: cost.byDay.map((d) => ({ y: d.costUsd, label: money(d.costUsd) })) }],
    });
    chart('.by-model-chart')?.setData({
      labels: cost.byModel.map((m) => `${m.provider}/${m.model}`),
      series: [tokens
        ? { id: 'tokens', label: 'Tokens', data: cost.byModel.map((m) => ({ y: m.tokens, label: count(m.tokens) })) }
        : { id: 'cost', label: 'Cost (USD)', data: cost.byModel.map((m) => ({ y: m.costUsd, label: money(m.costUsd) })) }],
    });
  }
}

define('kobold-stats-tab', StatsTab);
