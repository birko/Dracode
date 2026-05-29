/**
 * DraCode Application Shell
 * Birko.Web.Shell integration with component-based architecture
 */

import { BaseComponent, define } from 'birko-web-core';
import { BAppShell } from 'birko-web-shell/shell';
import { WsClient } from 'birko-web-core/http';
import { Store } from 'birko-web-core/state';

// Import Birko components to ensure they're registered
import 'birko-web-components';

import type { Agent, Provider, WebSocketMessage, WebSocketResponse } from './types.js';

/**
 * Application State
 */
interface AppState {
  wsUrl: string;
  isConnected: boolean;
  providers: Provider[];
  agents: Map<string, Agent>;
  activeAgentId: string | null;
  providerFilter: 'configured' | 'all' | 'notConfigured';
}

/**
 * DraCode Application Shell Component
 * Main application component using Birko.Web.Shell
 */
export class DraCodeAppShell extends BaseComponent {
  private _wsClient: WsClient | null = null;
  private _appStore: Store<AppState>;

  // Agent tabs state
  private _agentTabs: Array<{ id: string; provider: string; model: string }> = [];

  // Preserve input values across re-renders
  private _inputValues: Map<string, string> = new Map();

  static get styles() {
    return `
      :host {
        display: block;
        height: 100vh;
        background: var(--b-bg-primary, #ffffff);
      }

      .spa-container {
        display: flex;
        flex-direction: column;
        height: 100vh;
      }

      /* App Header */
      .app-header {
        background: var(--b-bg-secondary, #f9fafb);
        border-bottom: 1px solid var(--b-border, #e5e7eb);
        padding: 16px 24px;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      }

      .header-content {
        display: flex;
        justify-content: space-between;
        align-items: center;
        max-width: 1600px;
        margin: 0 auto;
      }

      .app-title {
        margin: 0;
        font-size: 24px;
        font-weight: 700;
        color: var(--b-text, #1f2937);
      }

      .header-actions {
        display: flex;
        gap: 12px;
        align-items: center;
      }

      /* Main Content */
      .main-content {
        flex: 1;
        overflow: auto;
        padding: 24px;
        max-width: 1600px;
        margin: 0 auto;
        width: 100%;
      }

      /* Tab Content */
      .tab-content {
        padding: 20px 0;
      }

      .tab-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
      }

      .tab-header h2 {
        margin: 0;
        font-size: 20px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
      }

      /* Grid Layouts */
      .providers-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
        gap: 20px;
      }

      .agents-grid {
        display: grid;
        grid-template-columns: 1fr;
        gap: 20px;
      }

      /* Provider Cards */
      .provider-card {
        transition: all 0.2s ease;
        cursor: pointer;
      }

      .provider-card:hover {
        transform: translateY(-2px);
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      }

      .provider-name {
        font-size: 18px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
        margin-bottom: 8px;
      }

      .provider-details {
        font-size: 14px;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 12px;
        line-height: 1.5;
      }

      /* Agent Cards */
      .agent-card {
        height: 100%;
      }

      .agent-card-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 16px;
      }

      .agent-info {
        flex: 1;
      }

      .agent-title {
        margin: 0 0 8px 0;
        font-size: 16px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
      }

      .agent-actions {
        display: flex;
        gap: 8px;
      }

      .agent-log {
        background: var(--b-bg-tertiary, #f1f5f9);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-md, 8px);
        padding: 16px;
        height: 300px;
        overflow-y: auto;
        font-family: 'Monaco', 'Courier New', monospace;
        font-size: 13px;
        line-height: 1.6;
        margin-bottom: 16px;
      }

      .agent-input-section {
        display: flex;
        gap: 8px;
      }

      .agent-input {
        flex: 1;
      }

      /* Connection Card */
      .connection-card {
        max-width: 600px;
      }

      .connection-form {
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      .connection-actions {
        display: flex;
        gap: 8px;
      }

      /* Log Entries */
      .agent-log-entry {
        margin-bottom: 8px;
        padding: 8px 12px;
        border-radius: 4px;
      }

      .agent-log-entry.error {
        background: var(--b-color-error-bg, #fee2e2);
        color: var(--b-color-error, #ef4444);
      }

      .agent-log-entry.success {
        background: var(--b-color-success-bg, #d1fae5);
        color: var(--b-color-success, #10b981);
      }

      .agent-log-entry.warning {
        background: var(--b-color-warning-bg, #fef3c7);
        color: var(--b-color-warning, #d59f0b);
      }

      .agent-log-entry.info {
        background: var(--b-color-info-bg, #dbeafe);
        color: var(--b-color-info, #3b82f6);
      }
    `;
  }

  constructor() {
    super();

    // Initialize reactive state store
    this._appStore = new Store<AppState>({
      wsUrl: 'ws://localhost:5000/ws',
      isConnected: false,
      providers: [],
      agents: new Map(),
      activeAgentId: null,
      providerFilter: 'configured'
    });
  }

  render() {
    const isConnected = this._appStore.get('isConnected');
    const providers = this._appStore.get('providers');
    const providerFilter = this._appStore.get('providerFilter');

    console.log('🎨 Rendering app-shell, isConnected:', isConnected, 'providers:', providers.length, 'agentTabs:', this._agentTabs.length);

    return `
      <div class="spa-container">
        <!-- App Header -->
        <header class="app-header">
          <div class="header-content">
            <h1 class="app-title">🤖 DraCode</h1>
            <div class="header-actions">
              <b-badge variant="${isConnected ? 'success' : 'secondary'}">
                ${isConnected ? '● Connected' : '○ Disconnected'}
              </b-badge>
            </div>
          </div>
        </header>

        <!-- Main Content -->
        <div class="main-content">
          <b-tabs id="mainTabs">
            <!-- Providers Tab -->
            <div slot="providers" class="tab-content">
              <div class="tab-header">
                <h2>Providers</h2>
                <b-button id="listBtn" ${isConnected ? '' : 'disabled'} variant="primary" size="sm">
                  🔄 Refresh
                </b-button>
              </div>

              ${isConnected ? `
                <div class="providers-grid">
                  ${providers.length > 0 ? this.renderProviders(providers, providerFilter) : '<b-empty icon="🔌" message="Click Refresh to load providers"></b-empty>'}
                </div>
              ` : '<b-empty icon="🔌" message="Connect to server to view providers"></b-empty>'}
            </div>

            <!-- Agents Tab -->
            <div slot="agents" class="tab-content">
              <div class="tab-header">
                <h2>Active Agents</h2>
                <b-badge variant="info">${this._agentTabs.length} Active</b-badge>
              </div>

              ${this._agentTabs.length > 0 ? `
                <div class="agents-grid">
                  ${this.renderAgentCards()}
                </div>
              ` : '<b-empty icon="🤖" message="No active agents. Connect a provider to create an agent."></b-empty>'}
            </div>

            <!-- Connection Tab -->
            <div slot="connection" class="tab-content">
              <div class="tab-header">
                <h2>Connection Settings</h2>
              </div>

              <b-card header="WebSocket Connection">
                <div class="connection-form">
                  <b-input
                    id="wsUrl"
                    label="Server URL"
                    value="ws://localhost:5000/ws"
                    placeholder="ws://localhost:5000/ws">
                  </b-input>

                  <div class="connection-actions">
                    <b-button id="connectBtn" ${isConnected ? 'disabled' : ''} variant="primary">
                      🔌 Connect
                    </b-button>
                    <b-button id="disconnectBtn" ${isConnected ? '' : 'disabled'} variant="secondary">
                      🔌 Disconnect
                    </b-button>
                  </div>
                </div>
              </b-card>
            </div>
          </b-tabs>
        </div>
      </div>
    `;
  }

  private renderAgentCards(): string {
    return this._agentTabs.map(tab => `
      <b-card class="agent-card">
        <div slot="header">
          <div class="agent-card-header">
            <div class="agent-info">
              <h3 class="agent-title">🤖 ${this.escapeHtml(tab.id)}</h3>
              <b-badge variant="info">${this.escapeHtml(tab.provider)}</b-badge>
            </div>
            <div class="agent-actions">
              <b-button class="chat-agent-btn" data-agent-id="${this.escapeHtml(tab.id)}" variant="primary" size="sm">
                💬 Chat
              </b-button>
              <b-button class="reset-agent-btn" data-agent-id="${this.escapeHtml(tab.id)}" variant="secondary" size="sm">
                🔄 Reset
              </b-button>
            </div>
          </div>
        </div>

        <div id="agentLog-${tab.id}" class="agent-log">
          <div class="agent-log-entry info">✅ Agent ready</div>
        </div>

        <div class="agent-input-section">
          <b-input id="agentInput-${tab.id}" placeholder="Send a task to the agent..." class="agent-input"></b-input>
          <b-button class="send-task-btn" data-agent-id="${this.escapeHtml(tab.id)}" variant="primary" size="sm">
            Send
          </b-button>
        </div>
      </b-card>
    `).join('');
  }

  private renderProviders(providers: Provider[], filter: string): string {
    const filteredProviders = providers.filter(provider => {
      if (filter === 'configured') return provider.configured;
      if (filter === 'notConfigured') return !provider.configured;
      return true;
    });

    if (filteredProviders.length === 0) {
      return `
        <div class="empty-state" style="grid-column: 1 / -1;">
          <p>No providers found</p>
        </div>
      `;
    }

    return filteredProviders.map(provider => `
      <b-card class="provider-card">
        <div class="provider-name">${this.escapeHtml(provider.name)}</div>
        <div class="provider-details">
          Provider: ${this.escapeHtml(provider.provider)} •
          ${provider.model ? `Model: ${this.escapeHtml(provider.model)}` : 'No model configured'}
        </div>
        <div style="margin: 8px 0;">
          <b-badge variant="${provider.configured ? 'success' : 'neutral'}">
            ${provider.configured ? '✅ Configured' : '❌ Not Configured'}
          </b-badge>
        </div>
        <div style="margin-top: 12px; display: flex; gap: 8px;">
          <b-button
            class="connect-provider-btn"
            data-provider-id="${this.escapeHtml(provider.name)}"
            ${provider.configured ? '' : 'disabled'}
            variant="${provider.configured ? 'primary' : 'secondary'}">
            ${provider.configured ? 'Connect Agent' : 'Configure'}
          </b-button>
        </div>
      </b-card>
    `).join('');
  }

  protected onMount() {
    console.log('🎯 DraCodeAppShell mounted!');
    this.setupEventListeners();
    this.setupWebSocket();
  }

  protected onUpdated() {
    console.log('🔄 DraCodeAppShell updated!');
    // Re-initialize b-tabs
    this.initializeTabs();
    // Re-attach event listeners for dynamically rendered content
    this.attachDynamicEventListeners();
    // Restore input values
    this.restoreInputValues();
  }

  private saveInputValues() {
    this._agentTabs.forEach(tab => {
      const input = this.$(`#agentInput-${tab.id}`) as HTMLInputElement;
      if (input && input.value) {
        this._inputValues.set(tab.id, input.value);
      }
    });
  }

  private restoreInputValues() {
    this._agentTabs.forEach(tab => {
      const input = this.$(`#agentInput-${tab.id}`) as HTMLInputElement;
      const savedValue = this._inputValues.get(tab.id);
      if (input && savedValue) {
        input.value = savedValue;
      }
    });
  }

  private initializeTabs() {
    const tabsEl = this.$('#mainTabs');
    if (tabsEl) {
      (tabsEl as any).setTabs([
        { id: 'providers', label: '🔌 Providers' },
        { id: 'agents', label: '🤖 Agents' },
        { id: 'connection', label: '⚙️ Connection' },
      ]);

      // Listen for tab changes
      tabsEl.removeEventListener('tab-change', this.handleTabChange);
      tabsEl.addEventListener('tab-change', this.handleTabChange);
    }
  }

  private handleTabChange = (e: any) => {
    console.log('🔄 Tab changed to:', e.detail.id);
  };

  private setupEventListeners() {
    console.log('🎧 setupEventListeners called');

    // Initialize b-tabs
    this.initializeTabs();

    // Connection buttons
    this.$('#connectBtn')?.addEventListener('click', () => {
      console.log('🖱️ Connect button clicked!');
      this.connectToServer();
    });

    this.$('#disconnectBtn')?.addEventListener('click', () => this.disconnectFromServer());
    this.$('#listBtn')?.addEventListener('click', () => {
      console.log('🖱️ Refresh button clicked');
      this.listProviders();
    });

    // Attach event listeners for dynamic content
    this.attachDynamicEventListeners();
  }

  private attachDynamicEventListeners() {
    // Provider cards
    this.$$('.connect-provider-btn').forEach(btn => {
      btn.removeEventListener('click', this.handleProviderConnectClick);
      btn.addEventListener('click', this.handleProviderConnectClick);
    });

    // Agent action buttons
    this.$$('.send-task-btn').forEach(btn => {
      btn.removeEventListener('click', this.handleSendTaskClick);
      btn.addEventListener('click', this.handleSendTaskClick);
    });

    this.$$('.reset-agent-btn').forEach(btn => {
      btn.removeEventListener('click', this.handleResetAgentClick);
      btn.addEventListener('click', this.handleResetAgentClick);
    });

    this.$$('.chat-agent-btn').forEach(btn => {
      btn.removeEventListener('click', this.handleChatAgentClick);
      btn.addEventListener('click', this.handleChatAgentClick);
    });

    // Agent input fields - Enter key to submit and save on input
    this.$$('input[id^="agentInput-"]').forEach(input => {
      const agentId = input.id.replace('agentInput-', '');

      // Remove old listeners
      input.removeEventListener('keydown', this.handleAgentInputKeydown);
      input.removeEventListener('input', this.handleAgentInput);

      // Add new listeners
      input.addEventListener('keydown', this.handleAgentInputKeydown);
      input.addEventListener('input', this.handleAgentInput);

      // Restore saved value
      const savedValue = this._inputValues.get(agentId);
      if (savedValue) {
        input.value = savedValue;
      }
    });
  }

  private handleAgentInput = (e: Event) => {
    const target = e.target as HTMLInputElement;
    const agentId = target.id.replace('agentInput-', '');
    if (agentId) {
      this._inputValues.set(agentId, target.value);
    }
  };

  private handleAgentInputKeydown = (e: KeyboardEvent) => {
    if (e.key === 'Enter') {
      const target = e.target as HTMLInputElement;
      const agentId = target.id.replace('agentInput-', '');
      if (agentId) {
        this.sendTaskToAgent(agentId);
      }
    }
  };

  private handleProviderConnectClick = (e: Event) => {
    const target = e.currentTarget as HTMLElement;
    const providerId = target.getAttribute('data-provider-id');
    if (providerId) {
      console.log('🖱️ Connect Provider button clicked for:', providerId);
      this.connectProvider(providerId);
    }
  };

  private handleSendTaskClick = (e: Event) => {
    const target = e.currentTarget as HTMLElement;
    const agentId = target.getAttribute('data-agent-id');
    if (agentId) {
      this.sendTaskToAgent(agentId);
    }
  };

  private handleResetAgentClick = (e: Event) => {
    const target = e.currentTarget as HTMLElement;
    const agentId = target.getAttribute('data-agent-id');
    if (agentId) {
      this.resetAgent(agentId);
    }
  };

  private handleChatAgentClick = (e: Event) => {
    const target = e.currentTarget as HTMLElement;
    const agentId = target.getAttribute('data-agent-id');
    if (agentId) {
      console.log('💬 Chat button clicked for agent:', agentId);
      // Focus on the agent input
      const input = this.$(`#agentInput-${agentId}`) as HTMLInputElement;
      if (input) {
        input.focus();
        input.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }
  };

  private setupWebSocket() {
    console.log('🔧 setupWebSocket called');
    // Will be implemented with WsClient
    const wsUrl = this._appStore.get('wsUrl');
    console.log('📍 Initial WS URL from store:', wsUrl);

    this._wsClient = new WsClient({
      url: wsUrl,
      onOpen: () => {
        console.log('✅ WebSocket connected!');
        this._appStore.set('isConnected', true);
        this.update();
      },
      onClose: () => {
        console.log('❌ WebSocket disconnected');
        this._appStore.set('isConnected', false);
        this.update();
      },
      onError: (event: Event) => {
        console.error('❌ WebSocket error:', event);
      },
      reconnectMs: 5000,
      heartbeatMs: 30000
    });

    // Register message handler
    this._wsClient.on('message', (message) => {
      console.log('📨 WebSocket message received:', message);
      this.handleWebSocketMessage(message);
    });

    console.log('✅ WsClient created');
  }

  private connectToServer() {
    console.log('🔌 connectToServer called');
    const wsUrlInput = this.$('#wsUrl') as HTMLInputElement;
    const wsUrl = wsUrlInput?.value || 'ws://localhost:5000/ws';

    console.log('📍 WebSocket URL:', wsUrl);
    this._appStore.set('wsUrl', wsUrl);

    if (this._wsClient) {
      console.log('✅ Calling wsClient.connect()...');
      this._wsClient.connect();
    } else {
      console.error('❌ wsClient is null!');
    }
  }

  private disconnectFromServer() {
    console.log('🔌 Disconnecting from server...');
    if (this._wsClient) {
      this._wsClient.disconnect();
      this._appStore.set('isConnected', false);
      this.update();
    }
  }

  private listProviders() {
    console.log('📋 Listing providers...');
    if (this._wsClient) {
      const message = {
        Command: 'list',
        Data: null
      };
      this._wsClient.send(JSON.stringify(message));
      console.log('✅ Sent list command:', message);
    } else {
      console.error('❌ Cannot list providers: wsClient is null');
    }
  }

  private connectProvider(providerId: string) {
    if (this._wsClient) {
      const message = {
        Command: 'connect',
        AgentId: providerId,
        Data: null
      };
      const messageStr = JSON.stringify(message);
      console.log('✅ Sending connect command for provider:', providerId);
      console.log('📤 Message object:', message);
      console.log('📤 Message JSON:', messageStr);
      this._wsClient.send(messageStr);
    } else {
      console.error('❌ Cannot connect: WebSocket client is null');
    }
  }

  private connectManualProvider() {
    const provider = (this.$('#manualProvider') as HTMLSelectElement)?.value;
    const apiKey = (this.$('#manualApiKey') as HTMLInputElement)?.value;
    const model = (this.$('#manualModel') as HTMLInputElement)?.value;
    const workingDir = (this.$('#manualWorkingDir') as HTMLInputElement)?.value;

    if (this._wsClient) {
      const message = {
        Command: 'connect',
        Data: JSON.stringify({
          provider,
          apiKey,
          model,
          workingDir
        })
      };
      this._wsClient.send(JSON.stringify(message));
      console.log('✅ Sent manual provider connection');
    }

    // Close modal and clear form
    this.$('#manualProviderModal')?.setAttribute('hidden', '');
  }

  private sendTaskToAgent(agentId: string) {
    const input = this.$(`#agentInput-${agentId}`) as HTMLInputElement;
    const task = input?.value;

    if (task && this._wsClient) {
      const message = {
        Command: 'task',
        AgentId: agentId,
        Data: task
      };
      this._wsClient.send(JSON.stringify(message));
      console.log('✅ Sent task to agent:', agentId, 'task:', task);

      // Clear input and add user message to log
      input.value = '';
      this._inputValues.delete(agentId); // Clear saved value
      this.logToAgent(agentId, `📤 You: ${task}`, 'info');
    } else if (!task) {
      console.warn('⚠️ No task entered');
    } else {
      console.error('❌ WebSocket client is null');
    }
  }

  private resetAgent(agentId: string) {
    if (this._wsClient) {
      const message = {
        Command: 'reset',
        AgentId: agentId,
        Data: null
      };
      this._wsClient.send(JSON.stringify(message));
      console.log('✅ Sent reset command for agent:', agentId);
    }
  }

  private handleWebSocketMessage(message: any) {
    console.log('📨 WebSocket message received:', message);

    // Handle different message types
    if (message && typeof message === 'object') {
      // Handle provider list response
      if (message.Status === 'success' && message.Data) {
        console.log('📋 Got success response with data');

        // Parse Data if it's a string, otherwise use as-is
        let parsedData: any;
        if (typeof message.Data === 'string') {
          try {
            parsedData = JSON.parse(message.Data);
          } catch (e) {
            console.error('❌ Failed to parse Data as JSON:', e);
            return;
          }
        } else {
          parsedData = message.Data;
        }

        // Check if it's a provider list
        if (Array.isArray(parsedData)) {
          console.log('📋 Got provider list response:', parsedData);
          this._appStore.set('providers', parsedData);
          console.log('✅ Providers updated:', parsedData);
          // Don't update here - only update when tab changes to providers
        }
      }

      // Handle agent creation
      if ((message as any).AgentId) {
        const agentId = (message as any).AgentId;
        const provider = (message as any).Message || 'Agent';
        console.log(`🤖 Agent created: ${agentId}`);
        this.addAgentTab(agentId, provider);
      }

      // Handle agent responses - log to agent console
      if ((message as any).AgentId && (message as any).Message) {
        const agentId = (message as any).AgentId;
        const msg = (message as any).Message;
        console.log('💬 Agent message:', msg);
        this.logToAgent(agentId, `🤖 Agent: ${msg}`, 'info');
      }

      // Handle generic messages for logging
      if ((message as any).Message && !(message as any).AgentId) {
        console.log('💬 Message:', (message as any).Message);
      }
    }
  }

  private addAgentTab(agentId: string, providerInfo: string) {
    // Check if tab already exists
    if (!this._agentTabs.find(tab => tab.id === agentId)) {
      // Save input values before re-rendering
      this.saveInputValues();

      this._agentTabs.push({
        id: agentId,
        provider: providerInfo,
        model: 'Agent'
      });
      console.log('✅ Agent tab added, updating UI...');
      this.update();
    }
  }

  private logToAgent(agentId: string, message: string, type: 'info' | 'success' | 'error' | 'warning' = 'info') {
    const logContainer = this.$(`#agentLog-${agentId}`);
    if (logContainer) {
      const entry = document.createElement('div');
      entry.className = `agent-log-entry ${type}`;
      entry.innerHTML = message;
      logContainer.appendChild(entry);
      logContainer.scrollTop = logContainer.scrollHeight;
    }
  }

  private formatResponse(response: WebSocketResponse): string {
    let msg = `<strong>${(response.Status || 'info').toUpperCase()}</strong>`;
    if (response.Message) msg += `: ${response.Message}`;
    if (response.Data) msg += `<br><pre>${this.escapeHtml(response.Data)}</pre>`;
    if (response.Error) msg += `<br><span class="error">${this.escapeHtml(response.Error)}</span>`;
    return msg;
  }

  private handleGeneralModalConfirm() {
    const input = this.$('#generalModalInput') as HTMLInputElement;
    const value = input?.value || '';

    // Hide modal
    this.$('#generalModal')?.setAttribute('hidden', '');

    // Handle response (can be overridden by specific modals)
    console.log('Modal confirmed with value:', value);
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}

// Register the custom element
define('dracode-app-shell', DraCodeAppShell);
