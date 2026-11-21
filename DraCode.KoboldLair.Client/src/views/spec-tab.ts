/**
 * The workspace's Spec tab (TASK-150, FEATURE-026 D11/D19): the project's specification and its features, read only.
 * Edits go through Dragon, and the tab says so.
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { createRestClient } from '../services/http.js';
import { renderMarkdown } from '../services/markdown.js';

export interface SpecificationView {
  name: string;
  content: string;
  version: number;
  contentHash?: string;
}

export interface FeatureView {
  id: string;
  name: string;
  description: string;
  priority: string;
  status: string;
}

type SpecState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; spec: SpecificationView; features: FeatureView[] };

const FEATURE_VARIANT: Record<string, string> = {
  Draft: 'neutral',
  New: 'neutral',
  Ready: 'warning',
  AssignedToWyvern: 'info',
  InProgress: 'info',
  Completed: 'success',
};

export class SpecTab extends KoboldComponent {
  private _state: SpecState = { kind: 'loading' };

  static get styles() {
    return `
      :host { display: block; min-width: 0; color: var(--b-text); }
      .note {
        margin: 0 0 var(--b-space-lg, 1rem);
        font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary);
      }
      .layout { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 18rem); gap: var(--b-space-xl, 1.5rem); }
      @media (max-width: 56rem) { .layout { grid-template-columns: minmax(0, 1fr); } }
      h2.section { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-lg, 1rem); }
      .meta { font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-muted); margin-bottom: var(--b-space-sm, 0.5rem); }
      .spec-body {
        min-width: 0; overflow-wrap: anywhere; line-height: 1.6;
        padding: var(--b-space-lg, 1rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
      }
      .spec-body > :first-child { margin-top: 0; }
      .spec-body pre {
        overflow-x: auto; max-width: 100%;
        padding: var(--b-space-md, 0.75rem); border-radius: var(--b-radius, 0.375rem);
        background: var(--b-bg-subtle, var(--b-bg)); border: 1px solid var(--b-border);
      }
      .spec-body code { font-family: var(--b-font-mono, ui-monospace, monospace); font-size: 0.9em; }
      .spec-body pre code { overflow-wrap: normal; white-space: pre; }
      .spec-body table { display: block; max-width: 100%; overflow-x: auto; border-collapse: collapse; overflow-wrap: normal; }
      .spec-body th, .spec-body td { padding: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem); border: 1px solid var(--b-border); }
      .spec-body blockquote { margin: 0; padding-left: var(--b-space-md, 0.75rem); border-left: 3px solid var(--b-border); color: var(--b-text-secondary); }
      .spec-body img { max-width: 100%; }
      .spec-body a, .note a { color: var(--b-color-primary); }
      .features { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--b-space-sm, 0.5rem); }
      .feature {
        min-width: 0; padding: var(--b-space-md, 0.75rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
      }
      .feature-head { display: flex; flex-wrap: wrap; align-items: center; gap: var(--b-space-xs, 0.25rem) var(--b-space-sm, 0.5rem); }
      .feature-name { font-weight: var(--b-font-weight-bold, 700); overflow-wrap: anywhere; }
      .feature-desc { margin: var(--b-space-xs, 0.25rem) 0 0; font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); overflow-wrap: anywhere; }
      .empty { color: var(--b-text-secondary); }
    `;
  }

  protected async onMount() {
    const id = this.attr('project-id');
    if (!id) {
      this._state = { kind: 'error', message: 'No project given.' };
      this.update();
      return;
    }
    const base = `/projects/${encodeURIComponent(id)}`;
    try {
      const client = createRestClient();
      const [spec, features] = await Promise.all([
        client.get<SpecificationView>(`${base}/specification`),
        client.get<FeatureView[]>(`${base}/features`),
      ]);
      this._state = spec.ok && features.ok && spec.data && Array.isArray(features.data)
        ? { kind: 'loaded', spec: spec.data, features: features.data }
        : { kind: 'error', message: `The server answered ${spec.ok ? features.status : spec.status}.` };
    } catch (error) {
      this._state = { kind: 'error', message: error instanceof Error ? error.message : String(error) };
    }
    this.update();
  }

  render() {
    const state = this._state;
    if (state.kind === 'loading') return `<b-spinner></b-spinner>`;
    if (state.kind === 'error') {
      return `<p class="empty"><strong>Could not load the specification.</strong> ${escapeHtml(state.message)}</p>`;
    }
    const { spec, features } = state;
    const content = spec.content?.trim()
      ? `<div class="meta">Version ${escapeHtml(String(spec.version))}</div>
         <div class="spec-body">${renderMarkdown(spec.content)}</div>`
      : `<p class="empty">No specification yet.</p>`;
    const featureList = features.length === 0
      ? `<p class="empty">No features yet.</p>`
      : `<ul class="features">${features.map((f) => this.renderFeature(f)).join('')}</ul>`;
    return `
      <p class="note">Read only. To change the specification or its features, ask <a href="#/dragon">Dragon</a>.</p>
      <div class="layout">
        <section><h2 class="section">Specification</h2>${content}</section>
        <section><h2 class="section">Features</h2>${featureList}</section>
      </div>
    `;
  }

  private renderFeature(f: FeatureView): string {
    return `
      <li class="feature" data-feature-id="${escapeHtml(f.id)}">
        <div class="feature-head">
          <span class="feature-name">${escapeHtml(f.name)}</span>
          <b-badge variant="${FEATURE_VARIANT[f.status] ?? 'neutral'}">${escapeHtml(f.status)}</b-badge>
          <b-badge variant="neutral">${escapeHtml(f.priority)}</b-badge>
        </div>
        ${f.description ? `<p class="feature-desc">${escapeHtml(f.description)}</p>` : ''}
      </li>
    `;
  }
}

define('kobold-spec-tab', SpecTab);
