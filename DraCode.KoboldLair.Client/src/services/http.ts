/**
 * REST and SSE clients for the KoboldLair server, on Birko.Web.Core's ApiClient and SseClient. Both read the token
 * from the auth store on every request / connection (TASK-114), like the /wyvern WebSocket client.
 */

import { ApiClient as RestClient, SseClient } from 'birko-web-core/http';
import configService from './config.js';
import { getToken, clearAuth } from '../auth-store.js';

/** The server's HTTP origin, from the configured WebSocket URL (ws://host:port → http://host:port). */
export function serverHttpOrigin(): string {
  return configService.getConfig().serverUrl.replace(/^ws(s?):\/\//, 'http$1://').replace(/\/$/, '');
}

/** A REST client for the server's /api/v1 facade. A 401 the token cannot fix signs the user out. */
export function createRestClient(): RestClient {
  return new RestClient({
    baseUrl: `${serverHttpOrigin()}/api/v1`,
    getToken,
    onUnauthorized: () => clearAuth(),
  });
}

/** An SSE client for a server event stream, e.g. `/api/v1/runs/{id}/events`. */
export function createSseClient(path: string, onMessage?: (event: MessageEvent) => void): SseClient {
  return new SseClient({ url: `${serverHttpOrigin()}${path}`, getToken, onMessage });
}
