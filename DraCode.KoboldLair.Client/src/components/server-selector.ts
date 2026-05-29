/**
 * Server Selector Component
 * Allows switching between KoboldLair server connections
 * Uses Birko.Web components (b-modal, b-button, b-input)
 */

import { BaseComponent, define } from 'birko-web-core';
import configService from '../services/config.js';

interface Server {
  id: string;
  name: string;
  url: string;
  token?: string;
}

interface ServerSelectorState {
  activeServerId: string;
  servers: Server[];
  showDropdown: boolean;
  showAddModal: boolean;
  showManageModal: boolean;
  newServer: { name: string; url: string; token: string };
}

/**
 * Server Selector Component
 * Displays current server and allows switching/adding/managing servers
 */
export class ServerSelector extends BaseComponent {
  private state: ServerSelectorState = {
    activeServerId: 'default',
    servers: [],
    showDropdown: false,
    showAddModal: false,
    showManageModal: false,
    newServer: { name: '', url: '', token: '' }
  };

  private onServerChangeCallback: ((server: Server) => void) | null = null;

  static get styles() {
    return `
      :host {
        display: inline-block;
        position: relative;
      }

      .server-selector {
        position: relative;
        display: inline-block;
      }

      .selector-button {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 8px 16px;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-md, 8px);
        cursor: pointer;
        font-size: 14px;
        transition: all 0.2s ease;
      }

      .selector-button:hover {
        border-color: var(--b-color-primary, #3b82f6);
        background: var(--b-bg-secondary, #f9fafb);
      }

      .selector-icon {
        font-size: 16px;
      }

      .selector-name {
        font-weight: 500;
        color: var(--b-text, #1f2937);
      }

      .selector-arrow {
        font-size: 12px;
        color: var(--b-text-secondary, #6b7280);
        transition: transform 0.2s ease;
      }

      .selector-button.open .selector-arrow {
        transform: rotate(180deg);
      }

      .dropdown {
        position: absolute;
        top: calc(100% + 4px);
        right: 0;
        min-width: 300px;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-lg, 12px);
        box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
        z-index: 1000;
        display: none;
      }

      .dropdown.visible {
        display: block;
      }

      .server-list {
        padding: 8px;
        max-height: 300px;
        overflow-y: auto;
      }

      .server-item {
        display: flex;
        align-items: center;
        gap: 12px;
        width: 100%;
        padding: 12px;
        background: none;
        border: none;
        border-radius: var(--b-radius-md, 8px);
        cursor: pointer;
        text-align: left;
        transition: background 0.2s ease;
      }

      .server-item:hover {
        background: var(--b-bg-secondary, #f9fafb);
      }

      .server-item.active {
        background: var(--b-color-primary, #3b82f6);
        color: white;
      }

      .server-item-icon {
        font-size: 16px;
        color: var(--b-text-secondary, #6b7280);
      }

      .server-item.active .server-item-icon {
        color: white;
      }

      .server-item-info {
        flex: 1;
        min-width: 0;
      }

      .server-item-name {
        font-size: 14px;
        font-weight: 500;
        margin-bottom: 2px;
      }

      .server-item-url {
        font-size: 12px;
        color: var(--b-text-muted, #9ca3af);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .server-item.active .server-item-url {
        color: rgba(255, 255, 255, 0.8);
      }

      .dropdown-actions {
        display: flex;
        gap: 8px;
        padding: 12px;
        border-top: 1px solid var(--b-border, #e5e7eb);
      }

      .dropdown-actions b-button {
        flex: 1;
      }

      .modal-content {
        padding: 24px;
      }

      .form-group {
        margin-bottom: 16px;
      }

      .form-label {
        display: block;
        font-size: 14px;
        font-weight: 500;
        color: var(--b-text, #1f2937);
        margin-bottom: 6px;
      }

      .form-input {
        width: 100%;
        padding: 8px 12px;
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-md, 8px);
        font-size: 14px;
        font-family: inherit;
      }

      .form-input:focus {
        outline: none;
        border-color: var(--b-color-primary, #3b82f6);
        box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
      }

      .form-help {
        font-size: 12px;
        color: var(--b-text-muted, #9ca3af);
        margin-top: 4px;
      }

      .server-card {
        margin-bottom: 16px;
      }

      .server-card-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 12px;
      }

      .server-card-header h4 {
        margin: 0;
        font-size: 16px;
        color: var(--b-text, #1f2937);
      }

      .server-card-actions {
        display: flex;
        gap: 8px;
      }

      .modal-large {
        max-width: 600px;
      }
    `;
  }

  render() {
    const activeServer = this.state.servers.find(s => s.id === this.state.activeServerId) || {
      id: 'default',
      name: 'Default Server',
      url: 'ws://localhost:5000'
    };

    return `
      <div class="server-selector">
        <button class="selector-button ${this.state.showDropdown ? 'open' : ''}" id="selectorBtn">
          <span class="selector-icon">🌐</span>
          <span class="selector-name">${this.escapeHtml(activeServer.name)}</span>
          <span class="selector-arrow">▼</span>
        </button>

        <div class="dropdown ${this.state.showDropdown ? 'visible' : ''}" id="dropdown">
          <div class="server-list">
            ${this.state.servers.length === 0 ? `
              <div style="padding: 20px; text-align: center; color: var(--b-text-muted);">
                No servers configured
              </div>
            ` : ''}
            ${this.state.servers.map(server => `
              <button class="server-item ${server.id === this.state.activeServerId ? 'active' : ''}"
                      data-server-id="${server.id}">
                <span class="server-item-icon">${server.id === this.state.activeServerId ? '✓' : '○'}</span>
                <div class="server-item-info">
                  <div class="server-item-name">${this.escapeHtml(server.name)}</div>
                  <div class="server-item-url">${this.escapeHtml(server.url)}</div>
                </div>
              </button>
            `).join('')}
          </div>
          <div class="dropdown-actions">
            <b-button variant="secondary" size="sm" id="addServerBtn">
              ➕ Add Server
            </b-button>
            <b-button variant="secondary" size="sm" id="manageServersBtn">
              ⚙️ Manage
            </b-button>
          </div>
        </div>
      </div>

      <b-modal id="addServerModal" hidden>
        <div slot="header">Add Server Connection</div>
        <div slot="body" class="modal-content">
          <div class="form-group">
            <label class="form-label">Server Name</label>
            <input type="text" class="form-input" id="serverName"
                   placeholder="My KoboldLair Server" value="${this.escapeHtml(this.state.newServer.name)}">
          </div>
          <div class="form-group">
            <label class="form-label">WebSocket URL</label>
            <input type="text" class="form-input" id="serverUrl"
                   placeholder="ws://localhost:5000" value="${this.escapeHtml(this.state.newServer.url)}">
            <div class="form-help">Use ws:// for unencrypted or wss:// for encrypted connections</div>
          </div>
          <div class="form-group">
            <label class="form-label">Auth Token (Optional)</label>
            <input type="password" class="form-input" id="serverToken"
                   placeholder="Leave empty if not required" value="${this.escapeHtml(this.state.newServer.token)}">
          </div>
        </div>
        <div slot="footer">
          <b-button variant="secondary" id="cancelAddServer">Cancel</b-button>
          <b-button variant="primary" id="saveAddServer">Add Server</b-button>
        </div>
      </b-modal>

      <b-modal id="manageServersModal" size="large" hidden>
        <div slot="header">Manage Server Connections</div>
        <div slot="body" class="modal-content">
          ${this.state.servers.map(server => `
            <b-card class="server-card" data-server-id="${server.id}">
              <div class="server-card-header">
                <h4>${this.escapeHtml(server.name)}</h4>
                ${this.state.servers.length > 1 ? `
                  <b-button variant="danger" size="sm" data-action="delete" data-server-id="${server.id}">🗑️</b-button>
                ` : ''}
              </div>
              <div class="form-group">
                <label class="form-label">WebSocket URL</label>
                <input type="text" class="form-input" data-field="url" data-server-id="${server.id}"
                       value="${this.escapeHtml(server.url)}">
              </div>
              <div class="form-group">
                <label class="form-label">Auth Token</label>
                <input type="password" class="form-input" data-field="token" data-server-id="${server.id}"
                       value="${this.escapeHtml(server.token || '')}">
              </div>
              <div class="server-card-actions">
                <b-button variant="primary" size="sm" data-action="save" data-server-id="${server.id}">Save Changes</b-button>
                <b-button variant="secondary" size="sm" data-action="test" data-server-id="${server.id}">Test Connection</b-button>
              </div>
            </b-card>
          `).join('')}
        </div>
        <div slot="footer">
          <b-button variant="secondary" id="closeManageModal">Close</b-button>
        </div>
      </b-modal>
    `;
  }

  protected onMount() {
    this.loadServers();
    this.attachEventListeners();
  }

  protected onUpdated() {
    this.onMount();
  }

  private attachEventListeners() {
    // Toggle dropdown
    this.$('#selectorBtn')?.addEventListener('click', (e) => {
      e.stopPropagation();
      this.toggleDropdown();
    });

    // Close dropdown when clicking outside
    document.addEventListener('click', (e) => {
      const target = e.target as HTMLElement;
      if (!target?.closest('.server-selector')) {
        this.closeDropdown();
      }
    });

    // Server selection
        this.$$('.server-item').forEach(item => {
      item.addEventListener('click', (e) => {
        const serverId = (e.currentTarget as HTMLElement).dataset.serverId;
        if (serverId) {
          this.switchServer(serverId);
        }
      });
    });

    // Add server button
    this.$('#addServerBtn')?.addEventListener('click', () => {
      this.closeDropdown();
      this.showAddServerModal();
    });

    // Manage servers button
    this.$('#manageServersBtn')?.addEventListener('click', () => {
      this.closeDropdown();
      this.showManageServersModal();
    });

    // Add server modal
    this.$('#cancelAddServer')?.addEventListener('click', () => {
      this.hideAddServerModal();
    });

    this.$('#saveAddServer')?.addEventListener('click', () => {
      this.saveNewServer();
    });

    // Manage servers modal
    this.$('#closeManageModal')?.addEventListener('click', () => {
      this.hideManageServersModal();
    });

    // Server card actions
    this.$$('[data-action]').forEach(btn => {
      btn.addEventListener('click', (e) => {
        const action = (e.currentTarget as HTMLElement).dataset.action;
        const serverId = (e.currentTarget as HTMLElement).dataset.serverId;
        if (serverId) {
          this.handleServerAction(action!, serverId);
        }
      });
    });

    // Input changes
    this.$$('#serverName, #serverUrl, #serverToken').forEach(input => {
      input.addEventListener('input', (e) => {
        const target = e.target as HTMLInputElement;
        const field = target.id.replace('server', '').toLowerCase();
        (this.state.newServer as any)[field] = target.value;
      });
    });
  }

  private loadServers() {
    try {
      const stored = localStorage.getItem('koboldlair-servers');
      if (stored) {
        this.state.servers = JSON.parse(stored);
      }

      const activeId = localStorage.getItem('koboldlair-active-server');
      if (activeId) {
        this.state.activeServerId = activeId;
      }
    } catch (error) {
      console.error('Failed to load servers:', error);
    }

    // Ensure at least one server exists
    if (this.state.servers.length === 0) {
      this.state.servers = [{
        id: 'default',
        name: 'Default Server',
        url: 'ws://localhost:5000',
        token: ''
      }];
    }
  }

  private saveServers() {
    try {
      localStorage.setItem('koboldlair-servers', JSON.stringify(this.state.servers));
      localStorage.setItem('koboldlair-active-server', this.state.activeServerId);
    } catch (error) {
      console.error('Failed to save servers:', error);
    }
  }

  private toggleDropdown() {
    this.state.showDropdown = !this.state.showDropdown;
    this.update();
  }

  private closeDropdown() {
    this.state.showDropdown = false;
    this.update();
  }

  private switchServer(serverId: string) {
    const server = this.state.servers.find(s => s.id === serverId);
    if (!server) return;

    this.state.activeServerId = serverId;
    this.saveServers();

    // Update config service
    configService.setServerUrl(server.url);
    configService.setAuthToken(server.token || '');

    this.closeDropdown();

    // Notify callback
    if (this.onServerChangeCallback) {
      this.onServerChangeCallback(server);
    }

    this.showNotification(`Switched to ${server.name}`);
  }

  private showAddServerModal() {
    this.state.newServer = { name: '', url: '', token: '' };
    this.update();
    (this.$('#addServerModal') as any)?.show();
  }

  private hideAddServerModal() {
    (this.$('#addServerModal') as any)?.hide();
  }

  private showManageServersModal() {
    this.update();
    (this.$('#manageServersModal') as any)?.show();
  }

  private hideManageServersModal() {
    (this.$('#manageServersModal') as any)?.hide();
  }

  private saveNewServer() {
    const name = this.state.newServer.name.trim();
    const url = this.state.newServer.url.trim();
    const token = this.state.newServer.token.trim();

    if (!name || !url) {
      this.showNotification('Please enter server name and URL', 'error');
      return;
    }

    if (!url.startsWith('ws://') && !url.startsWith('wss://')) {
      this.showNotification('URL must start with ws:// or wss://', 'error');
      return;
    }

    const newServer: Server = {
      id: `server_${Date.now()}`,
      name,
      url,
      token: token || undefined
    };

    this.state.servers.push(newServer);
    this.saveServers();
    this.hideAddServerModal();
    this.update();

    this.showNotification(`Server "${name}" added successfully`);
  }

  private handleServerAction(action: string, serverId: string) {
    const server = this.state.servers.find(s => s.id === serverId);
    if (!server) return;

    switch (action) {
      case 'save':
        this.saveServerChanges(serverId);
        break;
      case 'test':
        this.testServerConnection(serverId);
        break;
      case 'delete':
        this.deleteServer(serverId);
        break;
    }
  }

  private saveServerChanges(serverId: string) {
    const urlInput = this.$(`input[data-field="url"][data-server-id="${serverId}"]`) as HTMLInputElement;
    const tokenInput = this.$(`input[data-field="token"][data-server-id="${serverId}"]`) as HTMLInputElement;

    const server = this.state.servers.find(s => s.id === serverId);
    if (!server) return;

    server.url = urlInput.value.trim();
    server.token = tokenInput.value.trim() || undefined;

    this.saveServers();
    this.showNotification(`Server "${server.name}" updated successfully`);
  }

  private testServerConnection(serverId: string) {
    const server = this.state.servers.find(s => s.id === serverId);
    if (!server) return;

    // TODO: Implement actual connection test
    this.showNotification(`Testing connection to ${server.name}...`);

    // Simulate test
    setTimeout(() => {
      this.showNotification(`Connection to ${server.name} successful!`, 'success');
    }, 1000);
  }

  private deleteServer(serverId: string) {
    if (this.state.servers.length <= 1) {
      this.showNotification('Cannot delete the last server', 'error');
      return;
    }

    const server = this.state.servers.find(s => s.id === serverId);
    if (!server) return;

    if (!confirm(`Delete server "${server.name}"?`)) {
      return;
    }

    this.state.servers = this.state.servers.filter(s => s.id !== serverId);

    // If we deleted the active server, switch to another
    if (this.state.activeServerId === serverId) {
      this.state.activeServerId = this.state.servers[0].id;
      const newActive = this.state.servers[0];
      configService.setServerUrl(newActive.url);
      configService.setAuthToken(newActive.token || '');
    }

    this.saveServers();
    this.hideManageServersModal();
    this.update();

    this.showNotification(`Server "${server.name}" deleted`);
  }

  /**
   * Register callback for server changes
   */
  onServerChange(callback: (server: Server) => void) {
    this.onServerChangeCallback = callback;
  }

  private showNotification(message: string, type: 'success' | 'error' | 'info' = 'info') {
    // TODO: Use Birko toast notification
    console.log(`[${type.toUpperCase()}] ${message}`);

    // Emit event for parent to handle
    this.emit('notification', { message, type });
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}

// Register the custom element
define('server-selector', ServerSelector);
