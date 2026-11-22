/**
 * The workspace's Runs tab (TASK-155, FEATURE-026 D7/D20): this project's runs, and the user's ad-hoc runs (which belong
 * to no project, so they show beside every project, marked ad-hoc), newest first — mode, status, start time and step
 * progress. Data from `GET /api/v1/runs?projectId=…&adhoc=true`; a project run's task is named from the project's tasks.
 * Runs started over `/kobold` (the CLI) or `POST /api/v1/runs` are listed; the pipeline's own Kobolds are on the Board.
 * The server keeps runs in memory, so a restart empties the list. Polls while the page is visible.
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { taskTitle, type TaskView } from './board-tab.js';

export interface RunView {
  runId: string;
  mode: string;
  projectId: string | null;
  taskId: string | null;
  prompt: string | null;
  status: 'pending' | 'running' | 'completed' | 'failed' | string;
  startedAt: string;
  completedAt: string | null;
  completedSteps: number;
  totalSteps: number;
  summary: string | null;
}

/** How often the list refreshes while the page is visible. */
export const RUNS_REFRESH_MS = 5000;

const STATUS_VARIANT: Record<string, string> = { pending: 'neutral', running: 'info', completed: 'success', failed: 'danger' };
const STATUS_LABEL: Record<string, string> = { pending: 'Pending', running: 'Running', completed: 'Completed', failed: 'Failed' };

type RunsState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; runs: RunView[]; titles: Record<string, string> };

function started(iso: string): string {
  const t = Date.parse(iso);
  if (!t) return '';
  const minutes = Math.round((Date.now() - t) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const d = new Date(t);
  return d.toDateString() === new Date().toDateString()
    ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    : d.toLocaleString([], { dateStyle: 'short', timeStyle: 'short' });
}

export class RunsTab extends KoboldComponent {
  private _state: RunsState = { kind: 'loading' };
  private _timer: ReturnType<typeof setTimeout> | null = null;
  private readonly _onVisibility = () => { if (document.visibilityState === 'visible') void this.refresh(); };

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); }
      .message { color: var(--b-text-secondary); }
      .runs { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--b-space-sm, 0.5rem); }
      .run {
        display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: var(--b-space-xs, 0.25rem) var(--b-space-md, 0.75rem);
        align-items: center; min-width: 0; padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius, 0.375rem);
      }
      .run.failed { border-left: 3px solid var(--b-color-danger); }
      .what { min-width: 0; font-size: var(--b-text-sm, 0.8125rem); font-weight: var(--b-font-weight-medium, 500);
        overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .meta { grid-column: 1 / -1; display: flex; flex-wrap: wrap; align-items: center; gap: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem);
        font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .steps { display: inline-flex; align-items: center; gap: var(--b-space-xs, 0.25rem); }
      .steps b-progress { width: 6rem; }
      .summary { grid-column: 1 / -1; margin: 0; font-size: var(--b-text-xs, 0.75rem); color: var(--b-color-danger-text, var(--b-color-danger)); overflow-wrap: anywhere; }
    `;
  }

  protected onMount() {
    document.addEventListener('visibilitychange', this._onVisibility);
    void this.refresh();
  }

  protected onUnmount() {
    document.removeEventListener('visibilitychange', this._onVisibility);
    if (this._timer) clearTimeout(this._timer);
    this._timer = null;
  }

  /** Load the runs (and the task titles they refer to), then come back in RUNS_REFRESH_MS while visible. */
  async refresh(): Promise<void> {
    if (this._timer) clearTimeout(this._timer);
    this._timer = null;
    const id = this.attr('project-id');
    if (!id) return;
    const before = JSON.stringify(this._state);
    try {
      const client = createRestClient();
      const [runs, tasks] = await Promise.all([
        client.get<RunView[]>('/runs', { projectId: id, adhoc: 'true' }),
        client.get<TaskView[]>(`/projects/${encodeURIComponent(id)}/tasks`).catch(() => null),
      ]);
      if (runs.ok && Array.isArray(runs.data)) {
        const titles: Record<string, string> = {};
        if (tasks?.ok && Array.isArray(tasks.data)) for (const t of tasks.data) titles[t.id] = taskTitle(t.task);
        this._state = { kind: 'loaded', runs: runs.data, titles };
      } else if (this._state.kind !== 'loaded') {
        this._state = { kind: 'error', message: `The server answered ${runs.status}.` };
      }
    } catch (error) {
      if (this._state.kind !== 'loaded') this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    if (!this.isConnected) return;
    if (JSON.stringify(this._state) !== before) this.update();
    if (document.visibilityState === 'visible') this._timer = setTimeout(() => void this.refresh(), RUNS_REFRESH_MS);
  }

  render() {
    const state = this._state;
    if (state.kind === 'loading') return `<b-spinner></b-spinner>`;
    if (state.kind === 'error') return `<p class="message"><strong>Could not load the runs.</strong> ${escapeHtml(state.message)}</p>`;
    if (state.runs.length === 0) {
      return `<p class="message"><strong>No runs yet.</strong> Runs started from the CLI or the API show here, with your ad-hoc
        runs; the server keeps them until it restarts.</p>`;
    }
    return `<ul class="runs">${state.runs.map((r) => this.renderRun(r, state.titles)).join('')}</ul>`;
  }

  private renderRun(r: RunView, titles: Record<string, string>): string {
    const adhoc = !r.projectId;
    const what = adhoc
      ? r.prompt || 'Ad-hoc run'
      : (r.taskId && titles[r.taskId]) || (r.taskId ? `Task ${r.taskId.slice(0, 8)}` : 'Project run');
    const percent = r.totalSteps > 0 ? Math.round((r.completedSteps / r.totalSteps) * 100) : 0;
    const steps = r.totalSteps > 0
      ? `<span class="steps"><b-progress value="${percent}" max="100" size="sm" variant="${r.status === 'failed' ? 'danger' : 'success'}"></b-progress>
         ${r.completedSteps} of ${r.totalSteps} steps</span>`
      : `<span class="steps">${r.status === 'pending' || r.status === 'running' ? 'no plan steps yet' : 'no plan steps'}</span>`;
    return `
      <li class="run${r.status === 'failed' ? ' failed' : ''}" data-run-id="${escapeHtml(r.runId)}" data-status="${escapeHtml(r.status)}">
        <span class="what" title="${escapeHtml(what)}">${escapeHtml(what)}</span>
        <b-badge class="status" variant="${STATUS_VARIANT[r.status] ?? 'neutral'}">${escapeHtml(STATUS_LABEL[r.status] ?? r.status)}</b-badge>
        <div class="meta">
          ${adhoc ? `<b-badge class="adhoc" variant="warning">Ad-hoc</b-badge>` : `<b-badge class="mode" variant="neutral">${escapeHtml(r.mode)}</b-badge>`}
          <span class="started">started ${escapeHtml(started(r.startedAt))}</span>
          ${steps}
        </div>
        ${r.status === 'failed' && r.summary ? `<p class="summary">${escapeHtml(r.summary)}</p>` : ''}
      </li>`;
  }
}

define('kobold-runs-tab', RunsTab);
