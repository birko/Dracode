/**
 * Login View — the /login route the auth guard redirects to (TASK-113). The sign-in itself (OAuth button, token
 * handling) is TASK-056; until then this explains why the user landed here.
 */

import { define } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';

export class LoginView extends KoboldComponent {
  static get styles() {
    return `
      :host { display: block; padding: var(--b-space-xl); max-width: 480px; }
      h1 { margin: 0 0 var(--b-space-sm); font-size: var(--b-text-2xl); }
      p { color: var(--b-text-muted); }
    `;
  }

  render() {
    return `
      <h1>Sign in</h1>
      <p>This KoboldLair server requires you to sign in. Sign-in is not available in the web client yet.</p>
    `;
  }
}

define('kobold-login-view', LoginView);
