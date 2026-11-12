/**
 * Authentication Store for KoboldLair
 * Built on Birko.Web.Shell auth store factory
 *
 * The one place the client keeps its token (TASK-114): the /wyvern WebSocket client, the REST client and the SSE
 * client all read it through getToken().
 */

import { createAuthStore } from 'birko-web-shell/auth';

const STORAGE_KEY = 'koboldlair_auth';

/** Where earlier clients kept a token; migrateLegacyTokens() moves it here once. */
const LEGACY_CONFIG_KEY = 'koboldlair-config';
const LEGACY_SERVERS_KEY = 'koboldlair-servers';

const auth = createAuthStore({
  storageKey: STORAGE_KEY,
  claimMappings: {
    userName: 'name',
    email: 'email',
    tenantId: 'tenant_id',
    permissions: 'permission'
  }
});

export const authStore = auth.store;
export const setAuth = auth.setAuth;
export const clearAuth = auth.clearAuth;
export const setPendingChallenge = auth.setPendingChallenge;
export const clearChallenge = auth.clearChallenge;

/** The current token, or null when signed out. */
export function getToken(): string | null {
  return authStore.get('token') ?? null;
}

/**
 * Earlier clients kept the token in `koboldlair-config.authToken` (and the server selector one per saved server in
 * `koboldlair-servers`). Move the config token into the auth store, unless it already holds one, and delete both, so
 * no second copy of a token survives. Runs on every load; after the first it finds nothing to move.
 */
export function migrateLegacyTokens(storage: Storage = localStorage): void {
  try {
    const rawConfig = storage.getItem(LEGACY_CONFIG_KEY);
    if (rawConfig) {
      const config = JSON.parse(rawConfig) as Record<string, unknown>;
      if ('authToken' in config) {
        const legacy = typeof config.authToken === 'string' ? config.authToken : '';
        if (legacy && !getToken()) setAuth({ token: legacy });
        delete config.authToken;
        storage.setItem(LEGACY_CONFIG_KEY, JSON.stringify(config));
      }
    }

    const rawServers = storage.getItem(LEGACY_SERVERS_KEY);
    if (rawServers) {
      const servers = JSON.parse(rawServers) as Array<Record<string, unknown>>;
      if (Array.isArray(servers) && servers.some((s) => s && 'token' in s)) {
        storage.setItem(LEGACY_SERVERS_KEY, JSON.stringify(servers.map(({ token: _token, ...rest }) => rest)));
      }
    }
  } catch (error) {
    console.warn('Could not migrate the stored token:', error);
  }
}

migrateLegacyTokens();
