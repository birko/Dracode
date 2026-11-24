/**
 * Dragon full screen (`#/dragon`), the phone's Dragon tab (TASK-164, FEATURE-026 D22): the last project opened's
 * Dragon with its decision cards, or before any project the home Dragon (the user's own conversation, for starting
 * projects). It is the same dock the workspace and the homepage show, on the same connection.
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { lastProject } from '../services/layout-mode.js';
import './dragon-dock.js';

export class DragonPage extends KoboldComponent {
  static get styles() {
    return `
      :host {
        display: flex; flex-direction: column; min-width: 0;
        /* The shell's content area, less its padding: the dock fills what the header and tab bar leave (each with its
           1px border, the tab bar with the bottom safe area); a real phone scrolled by those 2px otherwise */
        height: calc(100dvh - var(--b-header-height, 3rem) - 3.25rem - 2px - env(safe-area-inset-bottom, 0px)
          - 2 * var(--b-space-lg, 1rem));
        min-height: 20rem;
      }
      .context { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-sm, 0.8125rem); color: var(--b-text-secondary); }
      .context a { color: var(--b-color-primary); }
      kobold-dragon-dock { flex: 1; min-height: 0; }
    `;
  }

  render() {
    const project = lastProject();
    if (!project) {
      return `<p class="context">Your own conversation with Dragon, for starting projects.</p>
        <kobold-dragon-dock></kobold-dragon-dock>`;
    }
    const href = `#/projects/${encodeURIComponent(project.id)}/board`;
    return `<p class="context">About <a href="${escapeHtml(href)}">${escapeHtml(project.name)}</a></p>
      <kobold-dragon-dock project-id="${escapeHtml(project.id)}"></kobold-dragon-dock>`;
  }
}

define('kobold-dragon-page', DragonPage);
