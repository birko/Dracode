/**
 * The header's "N other projects need you" pill (TASK-147, FEATURE-026 D10): inside a project, the number of the
 * user's other projects with something waiting (`needsYouStore`), leading back to the homepage's list. Hidden at zero,
 * outside a project, and signed out; a phone has none (TASK-164: its Projects tab lists what needs you).
 */

import { define } from 'birko-web-core';
import { KoboldComponent } from './kobold-component.js';
import { needsYouStore, type NeedsYouState } from '../stores/needs-you-store.js';
import { authStore, getToken } from '../auth-store.js';

const currentProject = (): string | null => {
  const match = /^#\/projects\/([^/?#]+)/.exec(window.location.hash);
  return match ? decodeURIComponent(match[1]) : null;
};

export class NeedsYouPill extends KoboldComponent {
  private _state: NeedsYouState = needsYouStore.state;
  private _project = currentProject();
  private _unsubscribe: (() => void) | null = null;
  private _unsubscribeAuth: (() => void) | null = null;
  private readonly _onHash = () => {
    this._project = currentProject();
    this.update();
  };

  static get styles() {
    return `
      :host { display: inline-flex; flex-shrink: 0; }
      a {
        display: inline-flex; align-items: center; gap: 0.25rem; padding: 0.125rem 0.625rem;
        border-radius: 999px; background: var(--b-color-warning-light, var(--b-bg-tertiary)); color: var(--b-color-warning-text, var(--b-text));
        font-size: var(--b-text-xs, 0.75rem); font-weight: var(--b-font-weight-semibold, 600); text-decoration: none; white-space: nowrap;
      }
      a:hover { filter: brightness(0.97); }
      a:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
    `;
  }

  protected onMount() {
    window.addEventListener('hashchange', this._onHash);
    this.follow();
    this._unsubscribeAuth = authStore.onChange('isAuthenticated', () => { this.follow(); this.update(); });
  }

  protected onUnmount() {
    window.removeEventListener('hashchange', this._onHash);
    this._unsubscribe?.();
    this._unsubscribe = null;
    this._unsubscribeAuth?.();
    this._unsubscribeAuth = null;
  }

  private follow(): void {
    if (getToken() && !this._unsubscribe) {
      this._unsubscribe = needsYouStore.subscribe((state) => { this._state = state; this.update(); });
    } else if (!getToken() && this._unsubscribe) {
      this._unsubscribe();
      this._unsubscribe = null;
    }
  }

  render() {
    if (!this._project || !getToken()) return '';
    const others = new Set(this._state.items.filter((i) => i.projectId !== this._project).map((i) => i.projectId)).size;
    if (others === 0) return '';
    const long = others === 1 ? '1 other project needs you' : `${others} other projects need you`;
    return `<a href="#/" title="${long}" aria-label="${long}"><span>${long}</span></a>`;
  }
}

define('kobold-needs-you-pill', NeedsYouPill);
