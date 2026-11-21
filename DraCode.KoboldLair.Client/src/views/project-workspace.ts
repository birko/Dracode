/**
 * Project workspace frame (TASK-150, FEATURE-026 D11): a project opens at `#/projects/{id}/{tab}` with its tabs in the
 * centre, in D11's order. A tab is listed once its screen exists: Board (TASK-153) and Spec (TASK-150). An unknown or not-owned project (the API
 * answers 404 for both) and an unknown tab show the not-found view. Dragon is docked on the right and follows the
 * project (TASK-151); below the breakpoint it sits under the tabs.
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { getToken } from '../auth-store.js';
import type { ProjectSummary } from './home-view.js';
import './board-tab.js';
import './spec-tab.js';
import './dragon-dock.js';

/** The workspace tabs whose screens exist, in display order: id, label and the element that renders it. */
export const WORKSPACE_TABS: readonly { id: string; label: string; tag: string }[] = [
  { id: 'board', label: 'Board', tag: 'kobold-board-tab' },
  { id: 'spec', label: 'Spec', tag: 'kobold-spec-tab' },
];

export const DEFAULT_TAB = WORKSPACE_TABS[0].id;

type WorkspaceState =
  | { kind: 'loading' }
  | { kind: 'signed-out' }
  | { kind: 'not-found' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; project: Omit<ProjectSummary, 'tasks'> };

export class ProjectWorkspace extends KoboldComponent {
  private _state: WorkspaceState = { kind: 'loading' };

  static get styles() {
    return `
      :host { display: block; min-width: 0; }
      .workspace { display: grid; grid-template-columns: minmax(0, 1fr) 360px; gap: var(--b-space-lg, 1rem); align-items: start; }
      .main { min-width: 0; }
      .dock {
        position: sticky; top: 0; min-width: 0;
        height: calc(100vh - 7rem); min-height: 24rem;
      }
      @media (max-width: 56rem) {
        .workspace { grid-template-columns: minmax(0, 1fr); }
        .dock { position: static; height: 28rem; min-height: 0; }
      }
      .project-head { display: flex; flex-wrap: wrap; align-items: center; gap: var(--b-space-sm, 0.5rem); margin-bottom: var(--b-space-md, 0.75rem); min-width: 0; }
      .back { color: var(--b-color-primary); font-size: var(--b-text-sm, 0.8125rem); }
      .project-title {
        margin: 0; min-width: 0; font-size: var(--b-text-2xl, 1.5rem); color: var(--b-text);
        overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
      }
      .badges { display: flex; flex-wrap: wrap; gap: var(--b-space-xs, 0.25rem); }
      b-tabs { min-width: 0; }
      .message {
        padding: var(--b-space-xl, 1.5rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
        color: var(--b-text-secondary);
      }
      .message strong { display: block; margin-bottom: var(--b-space-xs, 0.25rem); color: var(--b-text); }
    `;
  }

  private get tab() {
    return WORKSPACE_TABS.find((t) => t.id === this.attr('tab'));
  }

  protected async onMount() {
    const id = this.attr('project-id');
    if (!id || !this.tab) {
      this._state = { kind: 'not-found' };
    } else if (!getToken()) {
      this._state = { kind: 'signed-out' };
    } else {
      try {
        const response = await createRestClient().get<ProjectSummary>(`/projects/${encodeURIComponent(id)}`);
        this._state = response.ok && response.data
          ? { kind: 'loaded', project: response.data }
          : response.status === 404
            ? { kind: 'not-found' }
            : { kind: 'error', message: `The server answered ${response.status}.` };
      } catch (error) {
        this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
      }
    }
    if (this._state.kind === 'loaded') document.title = `${this._state.project.name} - KoboldLair`;
    this.update();
  }

  render() {
    const state = this._state;
    switch (state.kind) {
      case 'loading':
        return `<b-spinner></b-spinner>`;
      case 'not-found':
        return `<kobold-not-found-view></kobold-not-found-view>`;
      case 'signed-out':
        return `<div class="message"><strong>Sign in to open this project.</strong>Projects come from the server's API,
          which needs a signed-in user.</div>`;
      case 'error':
        return `<div class="message"><strong>Could not load the project.</strong>${escapeHtml(state.message)}</div>`;
      case 'loaded': {
        const { project } = state;
        const active = this.tab!;
        return `
          <div class="workspace">
          <div class="main">
          <div class="project-head">
            <a class="back" href="#/">← Projects</a>
            <h1 class="project-title" title="${escapeHtml(project.name)}">${escapeHtml(project.name)}</h1>
            <div class="badges">
              <b-badge variant="neutral">${escapeHtml(project.status)}</b-badge>
              <b-badge variant="neutral">${escapeHtml(project.executionState)}</b-badge>
            </div>
          </div>
          <b-tabs active="${active.id}">
            <div slot="${active.id}"><${active.tag} project-id="${escapeHtml(project.id)}"></${active.tag}></div>
          </b-tabs>
          </div>
          <aside class="dock" aria-label="Dragon"><kobold-dragon-dock project-id="${escapeHtml(project.id)}"></kobold-dragon-dock></aside>
          </div>
        `;
      }
    }
  }

  protected onUpdated() {
    const tabs = this.$('b-tabs') as (HTMLElement & { setTabs(t: { id: string; label: string }[], a?: string): void }) | null;
    if (!tabs) return;
    tabs.setTabs(WORKSPACE_TABS.map(({ id, label }) => ({ id, label })), this.attr('tab') ?? DEFAULT_TAB);
    this.listen(tabs, 'tab-change', (e: Event) => {
      const next = (e as CustomEvent<{ tab?: string }>).detail?.tab;
      const id = this.attr('project-id');
      if (next && id && next !== this.attr('tab')) window.location.hash = `#/projects/${encodeURIComponent(id)}/${next}`;
    });
  }
}

define('kobold-project-workspace', ProjectWorkspace);
