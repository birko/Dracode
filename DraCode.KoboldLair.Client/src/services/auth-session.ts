/**
 * Sign-in with GitHub (TASK-056, FEATURE-026 D5) on Birko.Web.Shell's auth helpers (Birko TASK-524): the server's
 * `/auth/github/login` sends the browser to GitHub, and its callback comes back to this page with
 * `#access_token=…&refresh_token=…` in the fragment. `readAuthFragment` takes it into the auth store and clears it from
 * the address bar; `createAuthSession` refreshes the token through `POST /auth/refresh` before it expires and after a
 * 401. A refused refresh, or a 401 nothing can fix, ends the session: the store is cleared and the page goes to
 * `#/login`. Signing out revokes the refresh token (`POST /auth/logout`) and clears the store.
 */

import { createAuthSession, readAuthFragment } from 'birko-web-shell/auth';
import { authHandle, authStore, clearAuth, setAuth } from '../auth-store.js';
import { serverHttpOrigin } from './http.js';

/** Where the sign-in button sends the browser: the server's GitHub flow. */
export function gitHubSignInUrl(): string {
  return `${serverHttpOrigin()}/auth/github/login`;
}

async function refresh(refreshToken: string): Promise<{ accessToken: string; refreshToken?: string } | null> {
  const response = await fetch(`${serverHttpOrigin()}/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  });
  if (response.status === 400 || response.status === 401) return null; // refused: the session is over
  if (!response.ok) throw new Error(`The server answered ${response.status}`); // kept, and retried
  const body = await response.json() as { token?: string; refreshToken?: string };
  return body.token ? { accessToken: body.token, refreshToken: body.refreshToken || undefined } : null;
}

function toLogin(): void {
  if (window.location.hash !== '#/login') window.location.hash = '#/login';
}

export const authSession = createAuthSession({ auth: authHandle, refresh, onSessionEnded: toLogin });

/**
 * At startup, before the router reads the hash: take a sign-in redirect's tokens into the store (the address bar then
 * holds no fragment, so the router opens the homepage), and keep the token fresh from then on.
 */
export function startAuth(): void {
  const fragment = readAuthFragment();
  if (fragment) setAuth({ accessToken: fragment.accessToken, refreshToken: fragment.refreshToken });
  authSession.start();
}

/** A 401 the refresh could not fix: the session is over. */
export function endSession(): void {
  clearAuth();
  toLogin();
}

/** Sign out: revoke the refresh token on the server (best effort) and clear the store. */
export function signOut(): void {
  const refreshToken = authStore.get('refreshToken');
  if (refreshToken) {
    void fetch(`${serverHttpOrigin()}/auth/logout`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
      keepalive: true,
    }).catch(() => { /* the token expires on its own */ });
  }
  clearAuth();
  toLogin();
}
