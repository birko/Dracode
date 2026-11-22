/**
 * The workspace's Board tab (TASK-153, FEATURE-026 D16): the project's tasks in fixed status columns — To do,
 * In progress, Blocked·Failed, Done — on Birko's `b-kanban` (TASK-183). The agents move the cards between columns:
 * those are locked to a person. To do is split into priority lanes (TASK-154): moving a card to another lane, by mouse
 * or keyboard, sets its priority, and a refused change puts it back with the reason. A failed card has Retry. Nothing
 * pushes task changes to a browser, so the board polls while the page is visible. When its own width is narrow (a
 * phone, or Dragon expanded beside it, TASK-152) the columns stack one per row (`stackBelow`) and the cards turn compact.
 */

import { define, escapeHtml } from 'birko-web-core';
import type { BKanban, KanbanCard, KanbanColumn } from 'birko-web-components/data';
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
/** To do's lanes, top first (TASK-154). A lane's id is the priority in lower case, as the priority call takes it. */
const PRIORITIES = ['Critical', 'High', 'Normal', 'Low'];
const LANES = PRIORITIES.map((p) => ({
  id: p.toLowerCase(),
  label: p,
  color: p === 'Critical' ? 'var(--b-color-danger)' : p === 'High' ? 'var(--b-color-warning)' : undefined,
}));
const laneOf = (priority: string) => (PRIORITIES.includes(priority) ? priority.toLowerCase() : 'normal');

const STATUS_NOTE: Record<string, string> = {
  BlockedByFailure: 'Blocked by a failed task',
  AwaitingHumanDecision: 'Waiting for your decision',
  NotInitialized: 'Starting',
};

/** How often the board refreshes while the page is visible. */
export const BOARD_REFRESH_MS = 5000;
const ERROR_PREVIEW = 160;
/** Below this board width the columns stack (b-kanban `stackBelow`). */
const STACK_BELOW = '44rem';

/** A task's title: the first non-empty line of its description, without Markdown heading or list marks. */
export function taskTitle(task: string): string {
  const line = task.split('\n').map((l) => l.trim()).find((l) => l.length > 0) ?? '';
  return line.replace(/^#+\s*/, '').replace(/^[-*]\s+/, '').replace(/\*\*/g, '');
}

/** What b-kanban's `card-move` carries that the board reads. */
interface CardMove {
  card: KanbanCard;
  fromLane?: string;
  toLane?: string;
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
  private _kanban: BKanban | null = null;
  /** Priorities being saved, by task id: the card stays in its new lane while a refresh still has the old one. */
  private _priorityChanges = new Map<string, string>();
  private _moveError: string | null = null;
  private _onVisibility = () => { if (document.visibilityState === 'visible') void this.refresh(); };

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); }
      b-kanban { --b-kanban-col-min-width: 0; --b-kanban-col-max-width: none; }
      .message { color: var(--b-text-secondary); }
      .move-error { margin: 0 0 var(--b-space-sm, 0.5rem); color: var(--b-color-danger-text, var(--b-color-danger)); font-size: var(--b-text-sm, 0.8125rem); }
      .move-error[hidden] { display: none; }
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
    if (JSON.stringify(this._state) !== before) this.show(); // an unchanged board keeps its DOM (focus, hover)
    if (document.visibilityState === 'visible') this._timer = setTimeout(() => void this.refresh(), BOARD_REFRESH_MS);
  }

  /**
   * Show the current state. While the board stays on screen its cards are handed to it: a re-render of this view only
   * syncs the `b-kanban` element's attributes and never reaches its cards.
   */
  private show(): void {
    const tasks = this._state.kind === 'loaded' && this._state.tasks.length ? this._state.tasks : null;
    if (tasks && this._kanban?.isConnected) this._kanban.setCards(this.cards(tasks));
    else this.update();
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
    // The error line is always rendered, so b-kanban keeps its place and is never rebuilt by a re-render.
    return `<p class="move-error" role="alert"${this._moveError ? '' : ' hidden'}>${escapeHtml(this._moveError ?? '')}</p>
      <b-kanban empty-text="Nothing here"></b-kanban>`;
  }

  protected onUpdated() {
    const kanban = this.$('b-kanban') as BKanban | null;
    if (kanban === this._kanban) return;
    this._kanban = kanban;
    if (!kanban || this._state.kind !== 'loaded') return;
    kanban.setConfig({
      columns: COLUMNS.map((c): KanbanColumn => c.id === 'todo'
        ? { id: c.id, label: c.label, lanes: LANES, defaultLane: 'normal', sortable: false }
        : { id: c.id, label: c.label, locked: true, sortable: false }),
      cards: this.cards(this._state.tasks),
      renderCard: (card) => this.renderCard(card.metadata!.task as TaskView),
      stackBelow: STACK_BELOW,
      maxNestingDepth: 0,
    });
    // Only To do takes a person's moves, and only between its lanes: every other column is locked.
    kanban.addEventListener('card-move', (e) => void this.moved((e as CustomEvent<CardMove>).detail));
    // Retry is the card's own control: b-kanban starts no drag and fires no card-click for it.
    kanban.shadowRoot!.addEventListener('click', (e) => {
      const button = e.composedPath().find((n): n is HTMLElement => n instanceof HTMLElement && n.matches('b-button[data-retry]'));
      if (button) void this.retry(button.dataset.retry!);
    });
  }

  /** The tasks as b-kanban cards, each column in priority order, To do's in their priority's lane. */
  private cards(tasks: TaskView[]): KanbanCard[] {
    return tasks
      .map((t) => {
        const priority = this._priorityChanges.get(t.id);
        return priority ? { ...t, priority } : t;
      })
      .sort((a, b) => (PRIORITY_ORDER[a.priority] ?? 9) - (PRIORITY_ORDER[b.priority] ?? 9))
      .map((t, i) => ({
        id: t.id,
        columnId: COLUMN_OF[t.status] ?? 'todo',
        laneId: laneOf(t.priority),
        title: taskTitle(t.task),
        order: i,
        color: t.status === 'Failed' ? 'var(--b-color-danger)' : undefined,
        // The card re-renders when any of this changes.
        metadata: { task: t, retrying: this._retrying.has(t.id), retryError: this._retryErrors.get(t.id) ?? null },
      }));
  }

  /**
   * A card's content. It renders inside b-kanban's shadow root, where this view's stylesheet does not reach, so it is
   * styled inline. A stacked board shows each title on one line.
   */
  private renderCard(t: TaskView): string {
    const failed = t.status === 'Failed';
    const compact = this._kanban?.hasAttribute('stacked') ?? false;
    const error = failed && t.errorMessage
      ? t.errorMessage.length > ERROR_PREVIEW ? `${t.errorMessage.slice(0, ERROR_PREVIEW)}…` : t.errorMessage
      : '';
    const retryError = this._retryErrors.get(t.id);
    const note = STATUS_NOTE[t.status];
    const small = 'font-size:var(--b-text-xs, 0.75rem);';
    const muted = `${small} color:var(--b-text-muted);`;
    const errorStyle = `margin:var(--b-space-xs, 0.25rem) 0 0; ${small} color:var(--b-color-danger-text, var(--b-color-danger)); overflow-wrap:anywhere;`;
    const title = escapeHtml(taskTitle(t.task));
    return `
      <div class="task" data-task-id="${escapeHtml(t.id)}" data-status="${escapeHtml(t.status)}" style="flex:1; min-width:0;">
        <p class="title" title="${title}" style="margin:0; font-weight:var(--b-font-weight-medium, 500); overflow-wrap:anywhere;
          display:-webkit-box; -webkit-line-clamp:${compact ? 1 : 2}; -webkit-box-orient:vertical; overflow:hidden;">${title}</p>
        <div class="meta" style="display:flex; flex-wrap:wrap; align-items:center; gap:var(--b-space-xs, 0.25rem); margin-top:var(--b-space-xs, 0.25rem);">
          <b-badge variant="${PRIORITY_VARIANT[t.priority] ?? 'neutral'}">${escapeHtml(t.priority)}</b-badge>
          <span class="agent" style="${muted}">${escapeHtml(t.assignedAgent || 'no agent yet')}</span>
          ${note ? `<span class="note" style="${muted}">· ${escapeHtml(note)}</span>` : ''}
        </div>
        ${error ? `<p class="error" style="${errorStyle}">${escapeHtml(error)}</p>` : ''}
        ${retryError ? `<p class="error retry-error" style="${errorStyle}">${escapeHtml(retryError)}</p>` : ''}
        ${failed ? `<div class="actions" style="margin-top:var(--b-space-xs, 0.25rem);"><b-button class="retry" size="sm" variant="secondary"
          data-retry="${escapeHtml(t.id)}" ${this._retrying.has(t.id) ? 'loading' : ''}>Retry</b-button></div>` : ''}
      </div>`;
  }

  /** A card moved to another lane of To do: save its new priority, or put it back and say why. */
  private async moved({ card, fromLane, toLane }: CardMove): Promise<void> {
    const priority = PRIORITIES.find((p) => p.toLowerCase() === toLane);
    if (!priority || toLane === fromLane || this._state.kind !== 'loaded') return;
    const title = taskTitle((card.metadata?.task as TaskView | undefined)?.task ?? card.title);
    this._priorityChanges.set(card.id, priority);
    this.setMoveError(null);
    let error: string | null = null;
    try {
      const response = await createRestClient().post<{ error?: string }>(`/tasks/${encodeURIComponent(card.id)}/priority`, { priority: toLane });
      if (response.ok && this._state.kind === 'loaded') {
        this._state = { kind: 'loaded', tasks: this._state.tasks.map((t) => (t.id === card.id ? { ...t, priority } : t)) };
      } else if (!response.ok) {
        error = response.data?.error ?? `the server answered ${response.status}`;
      }
    } catch (e) {
      error = e instanceof Error ? e.message : String(e);
    }
    this._priorityChanges.delete(card.id);
    if (!this.isConnected) return;
    if (error) {
      if (!this._kanban?.revertMove(card.id)) this.show(); // a refresh since the move: redraw from the tasks instead
      this.setMoveError(`Could not move “${title}” to ${priority}: ${error}`);
    } else {
      this.show();
    }
  }

  private setMoveError(message: string | null): void {
    if (message === this._moveError) return;
    this._moveError = message;
    this.update();
  }

  /** Put a failed task back to To do; the server resets it where Drake works from (TASK-153). */
  private async retry(taskId: string): Promise<void> {
    if (this._retrying.has(taskId) || this._state.kind !== 'loaded') return;
    this._retrying.add(taskId);
    this._retryErrors.delete(taskId);
    this.show();
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
    if (this.isConnected) this.show();
  }
}

define('kobold-board-tab', BoardTab);
