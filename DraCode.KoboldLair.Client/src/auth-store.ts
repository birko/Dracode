/**
 * Authentication Store for KoboldLair
 * Built on Birko.Web.Shell auth store factory
 */

import { createAuthStore } from 'birko-web-shell/auth';

const auth = createAuthStore({
  storageKey: 'koboldlair_auth',
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
