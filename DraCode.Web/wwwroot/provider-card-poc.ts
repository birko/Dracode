/**
 * Proof of Concept: Migrating DraCode provider cards to Birko.Web.Components
 *
 * This demonstrates how to replace custom HTML/CSS provider cards with
 * Birko.Web's b-card and b-badge components.
 */

import { BaseComponent, define } from 'birko-web-core';

/**
 * Provider data interface
 */
interface Provider {
  name: string;
  model?: string;
  configured: boolean;
  connectionCount?: number;
}

/**
 * ProviderCard using Birko.Web.Components
 *
 * Replaces the custom .provider-card HTML/CSS with Shadow DOM components
 */
export class ProviderCard extends BaseComponent {
  private _provider: Provider = {
    name: '',
    model: 'Default model',
    configured: false,
    connectionCount: 0
  };

  static get styles() {
    return `
      :host {
        display: block;
        cursor: pointer;
        transition: transform 0.2s ease;
      }

      :host:hover {
        transform: translateY(-2px);
      }

      :host([connected]) {
        border: 2px solid var(--b-color-success, #10b981);
      }

      .provider-card-content {
        display: flex;
        flex-direction: column;
        gap: var(--b-space-sm, 8px);
      }

      .provider-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
      }

      .provider-name {
        font-size: var(--b-text-lg, 18px);
        font-weight: var(--b-font-weight-bold, 600);
        color: var(--b-text, #1f2937);
      }

      .provider-model {
        font-size: var(--b-text-sm, 14px);
        color: var(--b-text-secondary, #6b7280);
      }

      .provider-footer {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-top: var(--b-space-sm, 8px);
      }

      .connection-status {
        font-size: var(--b-text-sm, 14px);
        display: flex;
        align-items: center;
        gap: var(--b-space-xs, 4px);
      }

      .connection-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background-color: var(--b-color-secondary, #9ca3af);
      }

      :host([connected]) .connection-dot {
        background-color: var(--b-color-success, #10b981);
      }
    `;
  }

  /**
   * Set provider data
   */
  setProvider(provider: Provider): void {
    this._provider = provider;
    this.update();
  }

  /**
   * Get provider data
   */
  getProvider(): Provider {
    return this._provider;
  }

  render() {
    const { name, model, configured, connectionCount } = this._provider;
    const isConnected = (connectionCount || 0) > 0;
    const statusVariant = configured ? 'success' : 'secondary';
    const statusText = configured ? 'Configured' : 'Not configured';
    const connectionStatus = isConnected
      ? `🔗 ${connectionCount} active connection${connectionCount! > 1 ? 's' : ''}`
      : '○ Not connected';

    return `
      <div class="provider-card-content">
        <div class="provider-header">
          <div class="provider-name">${this.escapeHtml(name)}</div>
        </div>

        <div class="provider-model">
          ${this.escapeHtml(model || 'Default model')}
        </div>

        <b-badge variant="${statusVariant}">
          ${statusText}
        </b-badge>

        <div class="provider-footer">
          <div class="connection-status">
            <span class="connection-dot"></span>
            <span>${connectionStatus}</span>
          </div>
        </div>
      </div>
    `;
  }

  protected onMount() {
    this.addEventListener('click', () => {
      this.emit('connect', { provider: this._provider });
    });
  }

  protected onUpdated() {
    // Re-attach click listener after update
    this.onMount();
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  connectedCallback() {
    super.connectedCallback?.();
    const isConnected = (this._provider.connectionCount || 0) > 0;
    if (isConnected) {
      this.setAttribute('connected', '');
    }
  }
}

// Register the custom element
define('provider-card', ProviderCard);

/**
 * Factory function to create provider cards from data
 */
export function createProviderCard(provider: Provider): ProviderCard {
  const card = document.createElement('provider-card') as ProviderCard;
  card.setProvider(provider);

  const isConnected = (provider.connectionCount || 0) > 0;
  if (isConnected) {
    card.setAttribute('connected', '');
  }

  return card;
}

/**
 * Migration Example:
 *
 * OLD (Vanilla JS):
 * ```javascript
 * const card = document.createElement('div');
 * card.className = 'provider-card';
 * card.innerHTML = `
 *   <div class="provider-name">${provider.name}</div>
 *   <div class="provider-status ${provider.configured ? 'available' : 'unavailable'}">
 *     ${provider.configured ? 'Configured' : 'Not configured'}
 *   </div>
 * `;
 * card.addEventListener('click', () => connectToProvider(provider.name));
 * grid.appendChild(card);
 * ```
 *
 * NEW (Birko.Web.Components):
 * ```typescript
 * import { createProviderCard } from './provider-card-poc.js';
 *
 * const card = createProviderCard(provider);
 * card.addEventListener('connect', (e) => {
 *   connectToProvider(e.detail.provider.name);
 * });
 * grid.appendChild(card);
 * ```
 */
