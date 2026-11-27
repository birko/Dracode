/**
 * Projects homepage (TASK-145, FEATURE-026 D10): the caller's projects as cards, each with its status, execution
 * state and a progress bar from its task counts. Data comes from `GET /api/v1/projects` (owner-scoped; `scope=all`
 * lets an admin see every project and is ignored for anyone else). A card opens the project's workspace (TASK-150).
 * Above the cards, "Needs you" (TASK-147) lists what waits on the user, grouped by project, each item opening it.
 * Beside them (below, on a narrow screen) the home Dragon (TASK-149): the user's own conversation with no project, for
 * starting projects. A project it registers reloads the cards. Under the title, `b-stat` tiles over the caller's
 * projects, and below the cards two `b-chart` charts (TASK-148, FEATURE-026 D18), from `GET /api/v1/stats`: active
 * projects, tasks done this week, success rate, spend this month; cost over the last 30 days and tasks by status per
 * project. Numbers that fail to load say so there and leave the cards alone. On a tablet the home Dragon opens as an
 * overlay, and a phone shows none: Dragon is the tab bar's own tab (TASK-164, D22).
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BChart } from 'birko-web-components/data';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { getToken } from '../auth-store.js';
import { DEFAULT_TAB } from './project-workspace.js';
import { notificationStore } from '../stores/notification-store.js';
import { needsYouStore, type NeedsYouItem } from '../stores/needs-you-store.js';
import { dragonConnection } from '../services/dragon-connection.js';
import { money } from './stats-tab.js';
import './dragon-dock.js';
import type { DragonDock } from './dragon-dock.js';
import { onLayoutModeChange } from '../services/layout-mode.js';
import { applyDockOverlay, DOCK_CLOSE_HTML, dockOverlayStyles, dragonToggleHtml, wireDockOverlay } from '../services/dock-overlay.js';

export interface ProjectTaskCounts {
  total: number;
  done: number;
  inProgress: number;
  failed: number;
}

export interface ProjectSummary {
  id: string;
  name: string;
  ownerId: string | null;
  status: string;
  executionState: string;
  createdAt: string;
  /** Null when the server has no task store. */
  tasks: ProjectTaskCounts | null;
}

/** `GET /api/v1/stats`: the homepage's numbers across the caller's projects (TASK-148). */
export interface Overview {
  projects: { total: number; active: number };
  tasks: { total: number; byStatus: Record<string, number>; successRate: number | null; doneThisWeek: number; failedThisWeek: number };
  cost: {
    thisMonthUsd: number; thisMonthTokens: number; thisMonthRequests: number; monthlyBudgetUsd: number | null;
    byDay: Array<{ date: string; requests: number; tokens: number; costUsd: number }>;
  };
  byProject: Array<{ projectId: string; name: string; total: number; byStatus: Record<string, number> }>;
}

type OverviewState = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'loaded'; overview: Overview };

/** The per-project chart's stacks: task statuses grouped as a person reads them, each with its colour. */
const STATUS_GROUPS: Array<{ id: string; label: string; statuses: string[]; color: string }> = [
  { id: 'done', label: 'Done', statuses: ['Done'], color: 'var(--b-color-success)' },
  { id: 'working', label: 'Working', statuses: ['Working', 'NotInitialized'], color: 'var(--b-color-primary)' },
  { id: 'waiting', label: 'Waiting on you', statuses: ['AwaitingHumanDecision'], color: 'var(--b-color-warning)' },
  { id: 'todo', label: 'To do', statuses: ['Unassigned'], color: 'var(--b-text-muted)' },
  { id: 'blocked', label: 'Blocked', statuses: ['BlockedByFailure'], color: 'var(--b-text-secondary)' },
  { id: 'failed', label: 'Failed', statuses: ['Failed'], color: 'var(--b-color-danger)' },
];

type HomeState =
  | { kind: 'loading' }
  | { kind: 'signed-out' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; projects: ProjectSummary[] };

const STATUS_VARIANT: Record<string, string> = {
  InProgress: 'info',
  AwaitingVerification: 'info',
  SpecificationModified: 'warning',
  Verified: 'success',
  Completed: 'success',
  Failed: 'danger',
};

const EXECUTION_VARIANT: Record<string, string> = {
  Running: 'success',
  Paused: 'warning',
  Suspended: 'warning',
  Cancelled: 'danger',
};

const NEEDS_LABEL: Record<string, string> = {
  decision: 'Decision',
  failed_task: 'Failed',
  blocked_task: 'Blocked',
  merge_ready: 'Ready to merge',
  agent_answer: 'Agent answered',
};
const NEEDS_VARIANT: Record<string, string> = {
  decision: 'warning', failed_task: 'danger', blocked_task: 'warning', merge_ready: 'success', agent_answer: 'info',
};

function since(iso: string): string {
  const t = Date.parse(iso);
  if (!t) return '';
  const minutes = Math.round((Date.now() - t) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return new Date(t).toLocaleDateString();
}

export class HomeView extends KoboldComponent {
  private _state: HomeState = { kind: 'loading' };
  private _unread = new Map<string, number>();
  private _needsYou: NeedsYouItem[] = [];
  private _overview: OverviewState = { kind: 'loading' };
  /** The overview the charts were last given, so a re-render for a notification does not redraw them. */
  private _charted: Overview | null = null;
  private _unsubscribe: (() => void) | null = null;
  private _unsubscribeNeeds: (() => void) | null = null;
  private _unsubscribeProjects: (() => void) | null = null;
  private _unsubscribeLayout: (() => void) | null = null;
  /** The tablet's Dragon overlay is open. */
  private _overlayOpen = false;

  static get styles() {
    return `
      :host { display: block; min-width: 0; }
      .home {
        display: grid; grid-template-columns: minmax(0, 1fr) 360px; gap: var(--b-space-lg, 1rem); align-items: start;
      }
      .main { min-width: 0; }
      .dock { position: sticky; top: 0; min-width: 0; height: calc(100vh - 7rem); min-height: 24rem; }
      h1 { margin: 0 0 var(--b-space-lg, 1rem); font-size: var(--b-text-2xl, 1.5rem); color: var(--b-text); }
      .grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(min(100%, 18rem), 1fr));
        gap: var(--b-space-lg, 1rem);
      }
      .project-card {
        display: flex; flex-direction: column; gap: var(--b-space-sm, 0.5rem);
        min-width: 0;
        padding: var(--b-space-lg, 1rem);
        background: var(--b-bg-elevated);
        border: 1px solid var(--b-border);
        border-radius: var(--b-radius-lg, 0.5rem);
        color: var(--b-text);
        text-decoration: none;
        transition: border-color var(--b-transition, 150ms ease);
      }
      .project-card:hover { border-color: var(--b-color-primary); }
      .project-card:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .project-name {
        margin: 0; font-size: var(--b-text-lg, 1rem); font-weight: var(--b-font-weight-bold, 700);
        overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
      }
      .badges { display: flex; flex-wrap: wrap; gap: var(--b-space-xs, 0.25rem); }
      .counts { font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .failed { color: var(--b-color-danger); }
      .message {
        padding: var(--b-space-xl, 1.5rem);
        background: var(--b-bg-elevated);
        border: 1px solid var(--b-border);
        border-radius: var(--b-radius-lg, 0.5rem);
        color: var(--b-text-secondary);
      }
      .message strong { display: block; margin-bottom: var(--b-space-xs, 0.25rem); color: var(--b-text); }
      a { color: var(--b-color-primary); }
      .needs-you { margin: 0 0 var(--b-space-xl, 1.5rem); }
      .needs-you h2 { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-lg, 1rem); color: var(--b-text); }
      .needs-group {
        margin: 0 0 var(--b-space-sm, 0.5rem); padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-left: 3px solid var(--b-color-warning);
        border-radius: var(--b-radius-lg, 0.5rem);
      }
      .needs-group h3 { margin: 0 0 var(--b-space-xs, 0.25rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text); overflow-wrap: anywhere; }
      .needs-items { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.125rem; }
      .needs-item {
        display: flex; align-items: baseline; gap: var(--b-space-sm, 0.5rem); min-width: 0;
        padding: 0.25rem 0; color: var(--b-text); text-decoration: none; font-size: var(--b-text-sm, 0.8125rem);
      }
      .needs-item:hover .needs-title { text-decoration: underline; }
      .needs-item:focus-visible { outline: none; box-shadow: var(--b-focus-ring); border-radius: var(--b-radius-sm, 0.25rem); }
      .needs-title { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .needs-when { flex-shrink: 0; font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .all-clear { margin: 0; font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-muted); }
      .tiles {
        display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 10rem), 1fr)); gap: var(--b-space-sm, 0.5rem);
        margin: 0 0 var(--b-space-xl, 1.5rem);
      }
      .tiles b-stat { min-width: 0; }
      .charts {
        display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--b-space-lg, 1rem);
        margin-top: var(--b-space-xl, 1.5rem);
      }
      @media (max-width: 70rem) { .charts { grid-template-columns: minmax(0, 1fr); } }
      .panel {
        min-width: 0; padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
      }
      .panel h3 { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .panel b-chart { display: block; min-width: 0; }
      .note { margin: var(--b-space-xs, 0.25rem) 0 0; font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .overview-error { margin: 0 0 var(--b-space-xl, 1.5rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      ${dockOverlayStyles('.home')}
    `;
  }

  protected onUnmount() {
    this._unsubscribe?.();
    this._unsubscribe = null;
    this._unsubscribeNeeds?.();
    this._unsubscribeNeeds = null;
    this._unsubscribeProjects?.();
    this._unsubscribeProjects = null;
    this._unsubscribeLayout?.();
    this._unsubscribeLayout = null;
  }

  protected async onMount() {
    this._unsubscribeLayout = onLayoutModeChange((mode) => {
      if (mode !== 'tablet' && this._overlayOpen) this.setOverlay(false);
    });
    // Each card's unread notifications (TASK-159), from the same store as the header bell
    this._unsubscribe = notificationStore.subscribe(() => {
      this._unread = notificationStore.unreadByProject();
      if (this._state.kind === 'loaded') this.update();
    });
    // What waits on the user (TASK-147). The header's pill may already keep the store polling, so ask for a fresh list:
    // the homepage is where the person looks.
    if (getToken()) {
      this._unsubscribeNeeds = needsYouStore.subscribe((state) => {
        this._needsYou = state.items;
        if (this._state.kind === 'loaded') this.update();
      });
      void needsYouStore.refresh();
    }
    if (!getToken()) {
      this._state = { kind: 'signed-out' };
      this.update();
      return;
    }
    this._unsubscribeProjects = dragonConnection.onProjectsChanged(() => void this.load());
    await this.load();
  }

  private async load(): Promise<void> {
    try {
      const response = await createRestClient().get<ProjectSummary[]>('/projects', { scope: 'all' });
      this._state = response.ok && Array.isArray(response.data)
        ? { kind: 'loaded', projects: response.data }
        : { kind: 'error', message: `The server answered ${response.status}.` };
    } catch (error) {
      this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    this.update();
    if (this._state.kind === 'loaded' && this._state.projects.length > 0) await this.loadOverview();
  }

  /** The numbers over the same projects the cards show (`scope=all`, which only an admin's call widens). */
  private async loadOverview(): Promise<void> {
    try {
      const response = await createRestClient().get<Overview>('/stats', { scope: 'all' });
      this._overview = response.ok && response.data
        ? { kind: 'loaded', overview: response.data }
        : { kind: 'error', message: `The server answered ${response.status}.` };
    } catch (error) {
      this._overview = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    if (this.isConnected) this.update();
  }

  render() {
    // The dock is left alone by re-renders (data-morph="skip"), so its chat survives the cards reloading
    return `<div class="home${this._overlayOpen ? ' overlay-open' : ''}">
      <div class="main"><h1>Projects</h1>${this.renderBody()}</div>
      ${dragonToggleHtml(this._overlayOpen)}
      <aside class="dock" aria-label="Dragon" data-morph="skip">${DOCK_CLOSE_HTML}<kobold-dragon-dock></kobold-dragon-dock></aside>
    </div>`;
  }

  private renderBody(): string {
    const state = this._state;
    switch (state.kind) {
      case 'loading':
        return `<b-spinner></b-spinner>`;
      case 'signed-out':
        return `<div class="message"><strong>Sign in to see your projects.</strong>Your projects come from the
          server's API, which needs a signed-in user.</div>`;
      case 'error':
        return `<div class="message"><strong>Could not load your projects.</strong>${escapeHtml(state.message)}</div>`;
      case 'loaded':
        return state.projects.length === 0
          ? `<div class="message"><strong>No projects yet.</strong>A project starts as a conversation with Dragon, which
              writes its specification; the agents take it from there.</div>`
          : `${this.renderTiles()}${this.renderNeedsYou()}<div class="grid">${state.projects.map((p) => this.renderCard(p)).join('')}</div>${this.renderCharts()}`;
    }
  }

  /** Tokens rather than dollars: calls were recorded in the 30 days but no price is set, so a cost chart is flat. */
  private static unpriced(overview: Overview): boolean {
    return overview.cost.byDay.some((d) => d.requests > 0) && overview.cost.byDay.every((d) => d.costUsd === 0);
  }

  private renderTiles(): string {
    const o = this._overview;
    if (o.kind === 'loading') return '';
    if (o.kind === 'error') {
      return `<p class="overview overview-error"><strong>Could not load the numbers.</strong> ${escapeHtml(o.message)}</p>`;
    }
    const { projects, tasks, cost } = o.overview;
    const finished = (tasks.byStatus.Done ?? 0) + (tasks.byStatus.Failed ?? 0);
    const quiet = 'trend="flat" sentiment="neutral"';
    return `<section class="overview tiles" aria-label="Your numbers">
      <b-stat class="active" label="Active projects" value="${projects.active}" delta="of ${projects.total}" ${quiet}></b-stat>
      <b-stat class="done-week" label="Tasks done this week" value="${tasks.doneThisWeek}"
        ${tasks.failedThisWeek ? `delta="${tasks.failedThisWeek} failed" trend="flat" sentiment="negative"` : ''}></b-stat>
      <b-stat class="success" label="Success rate" value="${tasks.successRate === null ? '—' : `${tasks.successRate}%`}"
        ${tasks.successRate === null ? '' : `delta="${finished} finished" ${quiet}`}></b-stat>
      <b-stat class="spend" label="Spent this month" value="${escapeHtml(money(cost.thisMonthUsd))}"
        ${cost.monthlyBudgetUsd ? `delta="of ${escapeHtml(money(cost.monthlyBudgetUsd))} budget" ${quiet}` : ''}></b-stat>
    </section>`;
  }

  private renderCharts(): string {
    if (this._overview.kind !== 'loaded') return '';
    const overview = this._overview.overview;
    const tokens = HomeView.unpriced(overview);
    const usage = overview.cost.byDay.some((d) => d.requests > 0);
    const dayNote = !usage
      ? `<p class="note">No LLM usage in the last 30 days.</p>`
      : tokens ? `<p class="note">Calls were recorded but cost $0: prices are set in <code>CostTracking.Pricing</code>.</p>` : '';
    const taskNote = overview.tasks.total === 0 ? `<p class="note">No tasks yet.</p>` : '';
    return `<section class="overview charts" aria-label="Charts">
      <div class="panel by-day"><h3>${tokens ? 'Tokens' : 'Cost'}, last 30 days</h3>
        <b-chart class="by-day-chart" type="bar" height="200"></b-chart>${dayNote}</div>
      <div class="panel by-project"><h3>Tasks by status per project</h3>
        <b-chart class="by-project-chart" type="bar" height="200"></b-chart>${taskNote}</div>
    </section>`;
  }

  private setOverlay(open: boolean): void {
    this._overlayOpen = open;
    const home = this.$('.home') as HTMLElement | null;
    if (home) applyDockOverlay(home, open, () => void (this.$('kobold-dragon-dock') as DragonDock | null)?.focusInput());
  }

  protected onUpdated() {
    const home = this.$('.home') as HTMLElement | null;
    if (home) wireDockOverlay(this.listen.bind(this), home, () => this._overlayOpen, (open) => this.setOverlay(open));
    if (this._overview.kind !== 'loaded') return;
    const overview = this._overview.overview;
    const byDay = this.$('.by-day-chart') as (BChart & { klDrawn?: Overview }) | null;
    const byProject = this.$('.by-project-chart') as BChart | null;
    // A re-render for a notification keeps the same chart elements: leave what they already show
    if (!byDay || !byProject || byDay.klDrawn === overview) return;
    byDay.klDrawn = overview;

    const tokens = HomeView.unpriced(overview);
    byDay.setData({
      labels: overview.cost.byDay.map((d) => d.date.slice(5)),
      series: [tokens
        ? { id: 'tokens', label: 'Tokens', data: overview.cost.byDay.map((d) => ({ y: d.tokens, label: d.tokens.toLocaleString() })) }
        : { id: 'cost', label: 'Cost (USD)', data: overview.cost.byDay.map((d) => ({ y: d.costUsd, label: money(d.costUsd) })) }],
    });
    // Only the groups some project has: an empty stack adds a legend entry that says nothing
    const count = (p: Overview['byProject'][number], g: (typeof STATUS_GROUPS)[number]) =>
      g.statuses.reduce((n, s) => n + (p.byStatus[s] ?? 0), 0);
    const present = STATUS_GROUPS.filter((g) => overview.byProject.some((p) => count(p, g) > 0));
    byProject.setData({
      labels: overview.byProject.map((p) => p.name),
      series: (present.length ? present : STATUS_GROUPS.slice(0, 1)).map((g) => ({
        id: g.id, label: g.label, color: g.color, data: overview.byProject.map((p) => ({ y: count(p, g) })),
      })),
    });
    byProject.setOptions({ stacked: true });
  }

  /** Needs you (TASK-147): grouped by project, the project with the newest item first, each item newest first. */
  private renderNeedsYou(): string {
    if (this._needsYou.length === 0) {
      return `<section class="needs-you" id="needs-you" aria-label="Needs you"><p class="all-clear">Nothing needs you right now.</p></section>`;
    }
    const groups = new Map<string, NeedsYouItem[]>();
    for (const item of [...this._needsYou].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt))) {
      groups.set(item.projectId, [...(groups.get(item.projectId) ?? []), item]);
    }
    return `<section class="needs-you" id="needs-you" aria-labelledby="needs-you-title">
      <h2 id="needs-you-title">Needs you</h2>
      ${[...groups.values()].map((items) => `
        <div class="needs-group" data-project-id="${escapeHtml(items[0].projectId)}">
          <h3>${escapeHtml(items[0].projectName)}</h3>
          <ul class="needs-items">${items.map((i) => `<li><a class="needs-item" data-kind="${escapeHtml(i.kind)}"
              href="#/projects/${encodeURIComponent(i.projectId)}/${DEFAULT_TAB}">
            <b-badge variant="${NEEDS_VARIANT[i.kind] ?? 'neutral'}">${escapeHtml(NEEDS_LABEL[i.kind] ?? i.kind)}</b-badge>
            <span class="needs-title" title="${escapeHtml(i.title)}">${escapeHtml(i.title)}</span>
            <span class="needs-when">${escapeHtml(since(i.createdAt))}</span>
          </a></li>`).join('')}</ul>
        </div>`).join('')}
    </section>`;
  }

  private renderCard(project: ProjectSummary): string {
    const unread = this._unread.get(project.id) ?? 0;
    const tasks = project.tasks;
    const percent = tasks && tasks.total > 0 ? Math.round((tasks.done / tasks.total) * 100) : 0;
    const progress = !tasks
      ? `<div class="counts">Task counts unavailable</div>`
      : tasks.total === 0
        ? `<div class="counts">No tasks yet</div>`
        : `<b-progress value="${percent}" max="100" size="sm" variant="${tasks.failed > 0 ? 'warning' : 'success'}"></b-progress>
           <div class="counts">${tasks.done} of ${tasks.total} tasks done${
             tasks.inProgress > 0 ? ` · ${tasks.inProgress} in progress` : ''}${
             tasks.failed > 0 ? ` · <span class="failed">${tasks.failed} failed</span>` : ''}</div>`;
    return `
      <a class="project-card" data-project-id="${escapeHtml(project.id)}"
         href="#/projects/${encodeURIComponent(project.id)}/${DEFAULT_TAB}">
        <h2 class="project-name" title="${escapeHtml(project.name)}">${escapeHtml(project.name)}</h2>
        <div class="badges">
          <b-badge variant="${STATUS_VARIANT[project.status] ?? 'neutral'}">${escapeHtml(project.status)}</b-badge>
          <b-badge variant="${EXECUTION_VARIANT[project.executionState] ?? 'neutral'}">${escapeHtml(project.executionState)}</b-badge>
          ${unread ? `<b-badge class="unread" variant="danger" title="Unread notifications">${unread} new</b-badge>` : ''}
        </div>
        ${progress}
      </a>
    `;
  }
}

define('kobold-home-view', HomeView);
