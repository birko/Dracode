/**
 * Projects homepage (TASK-145, FEATURE-026 D10): the caller's projects as cards, each with its status, execution
 * state and a progress bar from its task counts. Data comes from `GET /api/v1/projects` (owner-scoped; `scope=all`
 * lets an admin see every project and is ignored for anyone else). A card opens the project's workspace (TASK-150).
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { getToken } from '../auth-store.js';
import { DEFAULT_TAB } from './project-workspace.js';
import { notificationStore } from '../stores/notification-store.js';

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

export class HomeView extends KoboldComponent {
  private _state: HomeState = { kind: 'loading' };
  private _unread = new Map<string, number>();
  private _unsubscribe: (() => void) | null = null;

  static get styles() {
    return `
      :host { display: block; }
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
    `;
  }

  protected onUnmount() {
    this._unsubscribe?.();
    this._unsubscribe = null;
  }

  protected async onMount() {
    // Each card's unread notifications (TASK-159), from the same store as the header bell
    this._unsubscribe = notificationStore.subscribe(() => {
      this._unread = notificationStore.unreadByProject();
      if (this._state.kind === 'loaded') this.update();
    });
    if (!getToken()) {
      this._state = { kind: 'signed-out' };
      this.update();
      return;
    }
    try {
      const response = await createRestClient().get<ProjectSummary[]>('/projects', { scope: 'all' });
      this._state = response.ok && Array.isArray(response.data)
        ? { kind: 'loaded', projects: response.data }
        : { kind: 'error', message: `The server answered ${response.status}.` };
    } catch (error) {
      this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    this.update();
  }

  render() {
    return `<h1>Projects</h1>${this.renderBody()}`;
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
          : `<div class="grid">${state.projects.map((p) => this.renderCard(p)).join('')}</div>`;
    }
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
