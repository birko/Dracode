/**
 * The workspace's Board tab (TASK-153, FEATURE-026 D16): the project's tasks in fixed status columns — To do,
 * In progress, Blocked·Failed, Done. The agents move the cards; the person does not drag between columns. A failed card
 * has Retry. Nothing pushes task changes to a browser, so the board polls while the page is visible. When its own width
 * is narrow (a phone, or Dragon expanded beside it, TASK-152) the columns become a compact list (container query).
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';

export interface TaskView {
  id: string;
  task: string;
  projectId: string;
  status: string;
  priority: string;
  assignedAgent: string | null;
  featureId: string | null;
  errorMessage: string | null;
  commitFailed: boolean;
}

type ColumnId = 'todo' | 'progress' | 'blocked' | 'done';

const COLUMNS: { id: ColumnId; label: string }[] = [
  { id: 'todo', label: 'To do' },
  { id: 'progress', label: 'In progress' },
  { id: 'blocked', label: 'Blocked·Failed' },
  { id: 'done', label: 'Done' },
];

/** Status → column. Picked up but not started counts as in progress, as on the homepage. */
const COLUMN_OF: Record<string, ColumnId> = {
  Unassigned: 'todo',
  NotInitialized: 'progress',
  Working: 'progress',
  Failed: 'blocked',
  BlockedByFailure: 'blocked',
  AwaitingHumanDecision: 'blocked',
  Done: 'done',
};

const PRIORITY_ORDER: Record<string, number> = { Critical: 0, High: 1, Normal: 2, Low: 3 };
const PRIORITY_VARIANT: Record<string, string> = { Critical: 'danger', High: 'warning', Normal: 'neutral', Low: 'neutral' };
const STATUS_NOTE: Record<string, string> = {
  BlockedByFailure: 'Blocked by a failed task',
  AwaitingHumanDecision: 'Waiting for your decision',
  NotInitialized: 'Starting',
};

/** How often the board refreshes while the page is visible. */
export const BOARD_REFRESH_MS = 5000;
const ERROR_PREVIEW = 160;

/** A task's title: the first non-empty line of its description, without Markdown heading or list marks. */
export function taskTitle(task: string): string {
  const line = task.split('\n').map((l) => l.trim()).find((l) => l.length > 0) ?? '';
  return line.replace(/^#+\s*/, '').replace(/^[-*]\s+/, '').replace(/\*\*/g, '');
}

type BoardState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; tasks: TaskView[] };

export class BoardTab extends KoboldComponent {
  private _state: BoardState = { kind: 'loading' };
  private _timer: ReturnType<typeof setTimeout> | null = null;
  private _retrying = new Set<string>();
  private _retryErrors = new Map<string, string>();
  private _onVisibility = () => { if (document.visibilityState === 'visible') void this.refresh(); };

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); container-type: inline-size; }
      .columns { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: var(--b-space-md, 0.75rem); align-items: start; }
      .column {
        min-width: 0; padding: var(--b-space-sm, 0.5rem);
        background: var(--b-bg-subtle, var(--b-bg)); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
      }
      .column-head {
        display: flex; align-items: center; gap: var(--b-space-xs, 0.25rem);
        margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); font-weight: var(--b-font-weight-bold, 700);
      }
      .count { color: var(--b-text-muted); font-weight: normal; }
      .cards { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--b-space-sm, 0.5rem); }
      .card {
        min-width: 0; padding: var(--b-space-sm, 0.5rem) var(--b-space-md, 0.75rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius, 0.375rem);
      }
      .card.failed { border-left: 3px solid var(--b-color-danger); }
      .title {
        margin: 0; font-size: var(--b-text-sm, 0.8125rem); font-weight: var(--b-font-weight-medium, 500); overflow-wrap: anywhere;
        display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
      }
      .meta { display: flex; flex-wrap: wrap; align-items: center; gap: var(--b-space-xs, 0.25rem); margin-top: var(--b-space-xs, 0.25rem); }
      .agent, .note { font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); }
      .error { margin: var(--b-space-xs, 0.25rem) 0 0; font-size: var(--b-text-xs, 0.75rem); color: var(--b-color-danger); overflow-wrap: anywhere; }
      .actions { margin-top: var(--b-space-xs, 0.25rem); }
      .empty-col { font-size: var(--b-text-xs, 0.75rem); color: var(--b-text-muted); padding: var(--b-space-xs, 0.25rem); }
      .message { color: var(--b-text-secondary); }
      /* Compact list: narrow space (phone, or Dragon expanded beside the board). */
      @container (max-width: 44rem) {
        .columns { grid-template-columns: minmax(0, 1fr); gap: var(--b-space-sm, 0.5rem); }
        .column { padding: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem); background: none; border: none; border-bottom: 1px solid var(--b-border); border-radius: 0; }
        .cards { gap: 0; }
        .card { padding: var(--b-space-xs, 0.25rem) 0; background: none; border: none; border-radius: 0; }
        .card.failed { border-left: none; }
        .title { -webkit-line-clamp: 1; }
      }
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

  /** Load the tasks, then come back in BOARD_REFRESH_MS while mounted and visible. */
  async refresh(): Promise<void> {
    if (this._timer) clearTimeout(this._timer);
    this._timer = null;
    const id = this.attr('project-id');
    if (!id) return;
    const before = JSON.stringify(this._state);
    try {
      const response = await createRestClient().get<TaskView[]>(`/projects/${encodeURIComponent(id)}/tasks`);
      if (response.ok && Array.isArray(response.data)) this._state = { kind: 'loaded', tasks: response.data };
      else if (this._state.kind !== 'loaded') this._state = { kind: 'error', message: `The server answered ${response.status}.` };
    } catch (error) {
      if (this._state.kind !== 'loaded') this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    if (!this.isConnected) return;
    if (JSON.stringify(this._state) !== before) this.update(); // an unchanged board keeps its DOM (focus, hover)
    if (document.visibilityState === 'visible') this._timer = setTimeout(() => void this.refresh(), BOARD_REFRESH_MS);
  }

  render() {
    const state = this._state;
    if (state.kind === 'loading') return `<b-spinner></b-spinner>`;
    if (state.kind === 'error') {
      return `<p class="message"><strong>Could not load the tasks.</strong> ${escapeHtml(state.message)}</p>`;
    }
    if (state.tasks.length === 0) {
      return `<p class="message"><strong>No tasks yet.</strong> Wyvern breaks the specification into tasks once it is
        approved.</p>`;
    }
    const byColumn = new Map<ColumnId, TaskView[]>(COLUMNS.map((c) => [c.id, []]));
    for (const task of state.tasks) byColumn.get(COLUMN_OF[task.status] ?? 'todo')!.push(task);
    return `<div class="columns">${COLUMNS.map((c) => {
      const tasks = byColumn.get(c.id)!.sort((a, b) => (PRIORITY_ORDER[a.priority] ?? 9) - (PRIORITY_ORDER[b.priority] ?? 9));
      return `
        <section class="column" data-column="${c.id}">
          <h3 class="column-head">${escapeHtml(c.label)} <span class="count">${tasks.length}</span></h3>
          ${tasks.length === 0 ? `<div class="empty-col">Nothing here</div>`
            : `<ul class="cards">${tasks.map((t) => this.renderCard(t)).join('')}</ul>`}
        </section>`;
    }).join('')}</div>`;
  }

  private renderCard(t: TaskView): string {
    const failed = t.status === 'Failed';
    const error = failed && t.errorMessage
      ? t.errorMessage.length > ERROR_PREVIEW ? `${t.errorMessage.slice(0, ERROR_PREVIEW)}…` : t.errorMessage
      : '';
    const retryError = this._retryErrors.get(t.id);
    const note = STATUS_NOTE[t.status];
    return `
      <li class="card${failed ? ' failed' : ''}" data-task-id="${escapeHtml(t.id)}" data-status="${escapeHtml(t.status)}">
        <p class="title" title="${escapeHtml(taskTitle(t.task))}">${escapeHtml(taskTitle(t.task))}</p>
        <div class="meta">
          <b-badge variant="${PRIORITY_VARIANT[t.priority] ?? 'neutral'}">${escapeHtml(t.priority)}</b-badge>
          <span class="agent">${escapeHtml(t.assignedAgent || 'no agent yet')}</span>
          ${note ? `<span class="note">· ${escapeHtml(note)}</span>` : ''}
        </div>
        ${error ? `<p class="error">${escapeHtml(error)}</p>` : ''}
        ${retryError ? `<p class="error retry-error">${escapeHtml(retryError)}</p>` : ''}
        ${failed ? `<div class="actions"><b-button class="retry" size="sm" variant="secondary" data-retry="${escapeHtml(t.id)}"
          ${this._retrying.has(t.id) ? 'loading' : ''}>Retry</b-button></div>` : ''}
      </li>`;
  }

  protected onUpdated() {
    for (const button of Array.from(this.$$<HTMLElement>('b-button[data-retry]'))) {
      this.listen(button, 'click', () => void this.retry(button.dataset.retry!));
    }
  }

  /** Put a failed task back to To do; the server resets it where Drake works from (TASK-153). */
  private async retry(taskId: string): Promise<void> {
    if (this._retrying.has(taskId) || this._state.kind !== 'loaded') return;
    this._retrying.add(taskId);
    this._retryErrors.delete(taskId);
    this.update();
    try {
      const response = await createRestClient().post<{ status?: string; error?: string }>(`/tasks/${encodeURIComponent(taskId)}/retry`);
      if (response.ok && this._state.kind === 'loaded') {
        this._state = {
          kind: 'loaded',
          tasks: this._state.tasks.map((t) => (t.id === taskId ? { ...t, status: 'Unassigned', errorMessage: null } : t)),
        };
      } else if (!response.ok) {
        this._retryErrors.set(taskId, response.data?.error ?? `Retry failed: the server answered ${response.status}.`);
      }
    } catch (error) {
      this._retryErrors.set(taskId, `Retry failed: ${error instanceof Error ? error.message : String(error)}`);
    }
    this._retrying.delete(taskId);
    if (this.isConnected) this.update();
  }
}

define('kobold-board-tab', BoardTab);
