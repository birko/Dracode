/**
 * Not Found View — shown for a hash route no screen claims (TASK-113).
 */

import { define, escapeHtml } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';

export class NotFoundView extends KoboldComponent {
  static get styles() {
    return `
      :host { display: block; padding: var(--b-space-xl); }
      h1 { margin: 0 0 var(--b-space-sm); font-size: var(--b-text-2xl); }
      p { color: var(--b-text-muted); }
    `;
  }

  render() {
    const path = window.location.hash.slice(1) || '/';
    return `
      <h1>Page not found</h1>
      <p>Nothing lives at <code>${escapeHtml(path)}</code>.</p>
      <p><a href="#/">Back to your projects</a></p>
    `;
  }
}

define('kobold-not-found-view', NotFoundView);
