/**
 * Dragon View for KoboldLair
 * Interactive chat interface for requirements gathering with Dragon agent
 */

import { BaseComponent, define } from 'birko-web-core';

interface DragonMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: string;
}

interface DragonState {
  isConnected: boolean;
  isProcessing: boolean;
  currentProject: string | null;
}

/**
 * Dragon View Component
 * Chat interface for gathering project requirements
 */
export class DragonView extends BaseComponent {
  private _messages: DragonMessage[] = [];
  private _state: DragonState = {
    isConnected: false,
    isProcessing: false,
    currentProject: null
  };

  static get styles() {
    return `
      :host {
        display: flex;
        flex-direction: column;
        height: 100%;
        padding: 20px;
        max-width: 1400px;
        margin: 0 auto;
      }

      .dragon-header {
        margin-bottom: 20px;
      }

      .dragon-header h1 {
        font-size: 28px;
        font-weight: 600;
        margin: 0 0 8px 0;
        color: var(--b-text, #1f2937);
        display: flex;
        align-items: center;
        gap: 12px;
      }

      .dragon-status {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 14px;
        color: var(--b-text-secondary, #6b7280);
      }

      .status-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background-color: var(--b-color-secondary, #9ca3af);
      }

      .status-dot.connected {
        background-color: var(--b-color-success, #10b981);
        animation: pulse 2s ease-in-out infinite;
      }

      .status-dot.processing {
        background-color: var(--b-color-warning, #f59e0b);
        animation: pulse 1s ease-in-out infinite;
      }

      @keyframes pulse {
        0%, 100% { opacity: 1; }
        50% { opacity: 0.5; }
      }

      .chat-container {
        display: flex;
        flex-direction: column;
        flex: 1;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-lg, 12px);
        overflow: hidden;
      }

      .chat-messages {
        flex: 1;
        overflow-y: auto;
        padding: 20px;
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      .message {
        display: flex;
        gap: 12px;
        animation: slideIn 0.3s ease;
      }

      @keyframes slideIn {
        from {
          opacity: 0;
          transform: translateY(10px);
        }
        to {
          opacity: 1;
          transform: translateY(0);
        }
      }

      .message.user {
        flex-direction: row-reverse;
      }

      .message-avatar {
        width: 36px;
        height: 36px;
        border-radius: var(--b-radius-full, 9999px);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 20px;
        flex-shrink: 0;
      }

      .message.user .message-avatar {
        background: var(--b-color-primary, #3b82f6);
      }

      .message.assistant .message-avatar {
        background: var(--b-color-success, #10b981);
      }

      .message.system .message-avatar {
        background: var(--b-color-secondary, #6b7280);
      }

      .message-content {
        max-width: 70%;
        padding: 12px 16px;
        border-radius: 12px;
        line-height: 1.5;
      }

      .message.user .message-content {
        background: var(--b-color-primary, #3b82f6);
        color: white;
        border-bottom-right-radius: 4px;
      }

      .message.assistant .message-content {
        background: var(--b-bg-tertiary, #f3f4f6);
        color: var(--b-text, #1f2937);
        border-bottom-left-radius: 4px;
      }

      .message.system .message-content {
        background: var(--b-color-info, #06b6d4);
        color: white;
        text-align: center;
        font-size: 13px;
      }

      .message-meta {
        font-size: 11px;
        color: var(--b-text-muted, #9ca3af);
        margin-top: 4px;
      }

      .chat-input-area {
        border-top: 1px solid var(--b-border, #e5e7eb);
        padding: 20px;
        background: var(--b-bg-secondary, #f9fafb);
      }

      .input-rows {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .quick-actions {
        display: flex;
        gap: 8px;
        margin-bottom: 12px;
        flex-wrap: wrap;
      }

      .spec-preview {
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-md, 8px);
        padding: 12px;
        margin-bottom: 12px;
      }

      .spec-preview-title {
        font-size: 13px;
        font-weight: 600;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 8px;
      }

      .spec-preview-content {
        font-size: 14px;
        color: var(--b-text, #1f2937);
        max-height: 150px;
        overflow-y: auto;
        white-space: pre-wrap;
      }

      .empty-state {
        text-align: center;
        padding: 60px 20px;
        color: var(--b-text-secondary, #6b7280);
      }

      .empty-state-icon {
        font-size: 48px;
        margin-bottom: 16px;
      }

      .typing-indicator {
        display: flex;
        gap: 4px;
        padding: 12px 16px;
        background: var(--b-bg-tertiary, #f3f4f6);
        border-radius: 12px;
        border-bottom-left-radius: 4px;
        width: fit-content;
      }

      .typing-indicator span {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: var(--b-color-secondary, #9ca3af);
        animation: bounce 1.4s ease-in-out infinite;
      }

      .typing-indicator span:nth-child(2) {
        animation-delay: 0.2s;
      }

      .typing-indicator span:nth-child(3) {
        animation-delay: 0.4s;
      }

      @keyframes bounce {
        0%, 60%, 100% { transform: translateY(0); }
        30% { transform: translateY(-8px); }
      }
    `;
  }

  render() {
    return `
      <div class="dragon-header">
        <h1>
          <span>🐉</span>
          Dragon - Requirements Gathering
        </h1>
        <div class="dragon-status">
          <span class="status-dot ${this._state.isConnected ? 'connected' : ''} ${this._state.isProcessing ? 'processing' : ''}"></span>
          <span id="status-text">${this.getStatusText()}</span>
        </div>
      </div>

      ${this._state.currentProject ? `
        <div class="spec-preview">
          <div class="spec-preview-title">📋 Current Specification</div>
          <div class="spec-preview-content">${this.escapeHtml(this._state.currentProject)}</div>
        </div>
      ` : ''}

      <div class="chat-container">
        <div class="chat-messages" id="messages">
          ${this._messages.length === 0 ? this.renderEmptyState() : this.renderMessages()}
          ${this._state.isProcessing ? this.renderTypingIndicator() : ''}
        </div>

        <div class="chat-input-area">
          <div class="quick-actions">
            <b-button variant="ghost" size="sm" id="newProject">+ New Project</b-button>
            <b-button variant="ghost" size="sm" id="loadSpec">Load Specification</b-button>
            <b-button variant="ghost" size="sm" id="exportSpec">Export Specification</b-button>
          </div>

          <div class="input-rows">
            <b-textarea
              id="message-input"
              placeholder="Describe your project requirements..."
              rows="4">
            </b-textarea>

            <div style="display: flex; justify-content: flex-end;">
              <b-button
                variant="primary"
                id="send-button"
                ${this._state.isProcessing ? 'disabled' : ''}>
                ${this._state.isProcessing ? 'Processing...' : 'Send Message'}
              </b-button>
            </div>
          </div>
        </div>
      </div>
    `;
  }

  private getStatusText(): string {
    if (this._state.isProcessing) {
      return 'Dragon is thinking...';
    }
    if (this._state.isConnected) {
      return 'Connected to Dragon agent';
    }
    return 'Connecting to Dragon...';
  }

  private renderMessages(): string {
    return this._messages.map(msg => this.renderMessage(msg)).join('');
  }

  private renderMessage(msg: DragonMessage): string {
    const avatar = msg.role === 'user' ? '👤' : msg.role === 'assistant' ? '🐉' : '⚙️';

    return `
      <div class="message ${msg.role}">
        <div class="message-avatar">${avatar}</div>
        <div>
          <div class="message-content">${this.escapeHtml(msg.content)}</div>
          <div class="message-meta">${msg.timestamp}</div>
        </div>
      </div>
    `;
  }

  private renderEmptyState(): string {
    return `
      <div class="empty-state">
        <div class="empty-state-icon">🐉</div>
        <h3>Welcome to Dragon</h3>
        <p>Describe your project requirements and Dragon will help you create a detailed specification.</p>
        <p style="font-size: 12px; margin-top: 12px;">Start by describing what you want to build, or load an existing specification.</p>
      </div>
    `;
  }

  private renderTypingIndicator(): string {
    return `
      <div class="message assistant">
        <div class="message-avatar">🐉</div>
        <div class="typing-indicator">
          <span></span>
          <span></span>
          <span></span>
        </div>
      </div>
    `;
  }

  protected onMount() {
    // Connect to Dragon agent (simulated for now)
    this.connectToDragon();

    // Set up event listeners
    this.$('#send-button')?.addEventListener('click', () => this.handleSend());
    this.$('#message-input')?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && (e as KeyboardEvent).shiftKey) {
        // Allow line breaks with Shift+Enter
        return;
      }
      if (e.key === 'Enter') {
        e.preventDefault();
        this.handleSend();
      }
    });

    this.$('#newProject')?.addEventListener('click', () => this.handleNewProject());
    this.$('#loadSpec')?.addEventListener('click', () => this.handleLoadSpec());
    this.$('#exportSpec')?.addEventListener('click', () => this.handleExportSpec());

    // Scroll to bottom of messages
    this.scrollToBottom();
  }

  protected onUpdated() {
    // Re-attach event listeners and scroll
    this.onMount();
    this.scrollToBottom();
  }

  private async connectToDragon() {
    // TODO: Connect to actual WebSocket Dragon agent
    // For now, simulate connection
    await this.delay(500);

    this._state.isConnected = true;
    this.addSystemMessage('🐉 Dragon agent connected. Ready to gather requirements for your project!');
    this.update();
  }

  private handleSend() {
    const input = this.$('#message-input') as any;
    const message = input?.value?.trim();

    if (!message || this._state.isProcessing) {
      return;
    }

    // Add user message
    this.addMessage('user', message);
    input.value = '';

    // Process with Dragon
    this.processWithDragon(message);
  }

  private async processWithDragon(userMessage: string) {
    this._state.isProcessing = true;
    this.update();

    // TODO: Send to actual Dragon agent via WebSocket
    // For now, simulate response
    await this.delay(1500 + Math.random() * 1000);

    // Simulate Dragon response
    const response = this.generateDragonResponse(userMessage);
    this.addMessage('assistant', response);

    this._state.isProcessing = false;
    this.update();
  }

  private generateDragonResponse(userMessage: string): string {
    // Simple simulation of Dragon responses
    const responses = [
      `I understand you want to build: "${userMessage.substring(0, 50)}..."\n\nLet me ask a few clarifying questions:\n1. What is the primary goal of this project?\n2. Who are the target users?\n3. What are the key features you need?`,
      `That's a great starting point! Based on your requirements, I can see this will be a ${this.detectProjectType(userMessage)} project.\n\nShould I create a preliminary specification with the following modules?`,
      `Excellent! I've analyzed your requirements. This project will need:\n\n• Frontend: Web/Mobile interface\n• Backend: API server with database\n• Authentication: User management\n• Real-time features: WebSocket integration\n\nShall I proceed with creating the detailed specification?`
    ];

    return responses[Math.floor(Math.random() * responses.length)];
  }

  private detectProjectType(message: string): string {
    const lower = message.toLowerCase();
    if (lower.includes('web') || lower.includes('website')) return 'web application';
    if (lower.includes('api') || lower.includes('service')) return 'API service';
    if (lower.includes('mobile') || lower.includes('app')) return 'mobile application';
    if (lower.includes('dashboard') || lower.includes('admin')) return 'admin dashboard';
    return 'full-stack';
  }

  private handleNewProject() {
    console.log('New Project clicked');
    this._messages = [];
    this._state.currentProject = null;
    this.update();
    this.addSystemMessage('Started new project. Describe what you want to build!');
  }

  private handleLoadSpec() {
    console.log('Load Specification clicked');
    // TODO: Show file picker or load from existing specs
    this.addSystemMessage('💡 Feature coming soon: Load from existing specification files');
  }

  private handleExportSpec() {
    console.log('Export Specification clicked');
    if (this._state.currentProject) {
      // Create downloadable file
      const blob = new Blob([this._state.currentProject], { type: 'text/markdown' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `specification-${Date.now()}.md`;
      a.click();
      URL.revokeObjectURL(url);

      this.addSystemMessage('✅ Specification exported successfully');
    } else {
      this.addSystemMessage('⚠️ No specification to export. Start a conversation first!');
    }
  }

  private addMessage(role: 'user' | 'assistant', content: string) {
    this._messages.push({
      role,
      content,
      timestamp: new Date().toLocaleTimeString()
    });

    // Update current project spec if assistant responds
    if (role === 'assistant' && !this._state.currentProject) {
      this._state.currentProject = content;
    }

    this.update();
  }

  private addSystemMessage(content: string) {
    this._messages.push({
      role: 'system',
      content,
      timestamp: new Date().toLocaleTimeString()
    });
    this.update();
  }

  private scrollToBottom() {
    setTimeout(() => {
      const messages = this.$('#messages');
      if (messages) {
        messages.scrollTop = messages.scrollHeight;
      }
    }, 100);
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  /**
   * Public methods
   */
  public refresh(): void {
    this.update();
  }

  public loadSpecification(content: string): void {
    this._state.currentProject = content;
    this.addMessage('assistant', `I've loaded the following specification:\n\n${content}`);
  }
}

// Register the custom element
define('kobold-dragon-view', DragonView);
