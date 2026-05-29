/**
 * Services Index
 * Centralized exports for all services
 */

export { ApiClient } from './api-client.js';
export { configService as default, configService } from './config.js';
export { toastService, default as defaultToastService } from './toast-service.js';

// Re-export Birko.Web.Core services
export { WsClient } from 'birko-web-core/http';
export { ApiClient as BirkoApiClient } from 'birko-web-core/http';
export { SseClient } from 'birko-web-core/http';
export type { WsClientOptions, WsReadyState, ApiClientOptions, ApiResponse } from 'birko-web-core/http';
