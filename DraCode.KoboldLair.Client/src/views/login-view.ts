/**
 * The sign-in screen (TASK-056, FEATURE-026 D5), the /login route the auth guard redirects to: "Sign in with GitHub"
 * starts the server's GitHub flow, which comes back to the homepage signed in. A signed-in user who lands here goes on
 * to the homepage. When the server needs no sign-in (`authRequired` false) it says so and links to the projects.
 */

import { define } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { getToken } from '../auth-store.js';
import configService from '../services/config.js';
import { gitHubSignInUrl } from '../services/auth-session.js';

export class LoginView extends KoboldComponent {
  static get styles() {
    return `
      :host { display: block; padding: var(--b-space-xl, 1.5rem) var(--b-space-md, 0.75rem); }
      .card {
        max-width: 26rem; margin: 3rem auto 0; padding: var(--b-space-xl, 1.5rem);
        background: var(--b-bg-elevated); border: 1px solid var(--b-border); border-radius: var(--b-radius-lg, 0.5rem);
      }
      h1 { margin: 0 0 var(--b-space-sm, 0.5rem); font-size: var(--b-text-2xl, 1.5rem); color: var(--b-text); }
      p { margin: 0 0 var(--b-space-lg, 1rem); color: var(--b-text-secondary); }
      /* A link (it navigates to the server's sign-in), drawn like Birko's primary b-button. */
      .github {
        display: inline-flex; align-items: center; gap: var(--b-space-sm, 0.5rem);
        padding: var(--b-space-sm, 0.5rem) var(--b-space-lg, 1rem); border-radius: var(--b-radius, 0.375rem);
        background: var(--b-color-primary); color: var(--b-text-on-primary, var(--b-text-inverse)); font-weight: var(--b-font-weight-bold, 700);
        text-decoration: none;
      }
      .github:hover { background: var(--b-color-primary-hover); }
      .github:focus-visible { outline: none; box-shadow: var(--b-focus-ring); }
      .home { display: inline-block; margin-top: var(--b-space-md, 0.75rem); color: var(--b-color-primary); font-size: var(--b-text-sm, 0.8125rem); }
    `;
  }

  protected onMount() {
    if (getToken()) window.location.hash = '#/';
  }

  render() {
    const required = configService.getConfig().authRequired;
    return `
      <div class="card">
        <h1>Sign in</h1>
        <p>${required
          ? 'Sign in with your GitHub account to see your projects.'
          : 'This server does not require signing in. You can still sign in with GitHub to work as yourself.'}</p>
        <a class="github" href="${gitHubSignInUrl()}">Sign in with GitHub</a>
        ${required ? '' : '<br><a class="home" href="#/">Open the projects</a>'}
      </div>
    `;
  }
}

define('kobold-login-view', LoginView);
