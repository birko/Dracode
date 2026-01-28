import { WebSocketClient } from './websocket.js';

export class DragonView {
    constructor(api) {
        this.api = api;
        this.ws = null;
        this.messages = [];
        this.sessionId = null;
        this.providers = [];
        this.selectedProvider = null;
    }

    async render() {
        // Load available providers for dragon
        try {
            const providersData = await this.api.getProvidersForAgent('Dragon');
            this.providers = providersData.availableProviders || [];
            this.selectedProvider = providersData.currentProvider || (this.providers[0]?.name);
        } catch (error) {
            console.error('Failed to load providers:', error);
            this.providers = [];
        }

        return `
            <div class="chat-container">
                <div class="chat-header">
                    <h2>🐉 Dragon Requirements Agent</h2>
                    <div style="display: flex; gap: 10px; align-items: center;">
                        <select class="form-select" id="dragonProviderSelect" style="width: 200px;">
                            ${this.providers.map(p => `
                                <option value="${p.name}" ${p.name === this.selectedProvider ? 'selected' : ''}>
                                    ${p.displayName} (${p.defaultModel})
                                </option>
                            `).join('')}
                        </select>
                        <button class="btn btn-secondary" id="dragonReloadBtn" title="Reload agent (clear context and reload provider)">
                            <span>🔄 Reload Agent</span>
                        </button>
                    </div>
                </div>
                <div class="chat-messages" id="dragonMessages">
                    ${this.messages.length > 0 ? this.messages.map(msg => `
                        <div class="chat-message ${msg.role}">
                            <div class="chat-message-icon">${msg.role === 'user' ? '👤' : '🐉'}</div>
                            <div class="chat-message-content">
                                <div class="chat-message-role">${msg.role}</div>
                                <div class="chat-message-text">${msg.content}</div>
                            </div>
                        </div>
                    `).join('') : `
                        <div class="empty-state">
                            <div class="empty-state-icon">🐉</div>
                            <div>Start a conversation with Dragon</div>
                        </div>
                    `}
                </div>
                <div class="chat-input-container">
                    <textarea 
                        class="chat-input" 
                        id="dragonInput" 
                        placeholder="Type your message here..."
                        rows="3"
                    ></textarea>
                    <button class="btn btn-primary" id="dragonSendBtn">
                        <span>Send</span>
                        <span>📤</span>
                    </button>
                </div>
            </div>
        `;
    }

    onMount() {
        this.ws = new WebSocketClient('/dragon');
        this.ws.connect();

        this.ws.on('message', (data) => {
            if (data.type === 'dragon_message') {
                this.sessionId = data.sessionId;
                this.addMessage('assistant', data.message);
            } else if (data.type === 'dragon_reloaded') {
                this.sessionId = data.sessionId;
                this.messages = []; // Clear message history
                this.addMessage('system', data.message);
            } else if (data.type === 'dragon_typing') {
                // Could show typing indicator here
            } else if (data.type === 'error') {
                this.addMessage('system', `Error: ${data.message}`);
            } else {
                this.addMessage('assistant', data.content || JSON.stringify(data));
            }
        });

        const sendBtn = document.getElementById('dragonSendBtn');
        const input = document.getElementById('dragonInput');
        const reloadBtn = document.getElementById('dragonReloadBtn');
        const providerSelect = document.getElementById('dragonProviderSelect');

        sendBtn?.addEventListener('click', () => this.sendMessage());
        input?.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
        });
        reloadBtn?.addEventListener('click', () => this.reloadAgent());
        providerSelect?.addEventListener('change', (e) => {
            this.selectedProvider = e.target.value;
            this.reloadAgent();
        });
    }

    onUnmount() {
        this.ws?.disconnect();
        this.ws = null;
    }

    sendMessage() {
        const input = document.getElementById('dragonInput');
        const message = input.value.trim();
        
        if (message && this.ws) {
            this.addMessage('user', message);
            this.ws.send({ 
                message: message,
                sessionId: this.sessionId
            });
            input.value = '';
        }
    }

    reloadAgent() {
        if (this.ws && this.sessionId) {
            this.ws.send({ 
                type: 'reload',
                sessionId: this.sessionId,
                provider: this.selectedProvider
            });
        }
    }

    addMessage(role, content) {
        this.messages.push({ role, content });
        const messagesContainer = document.getElementById('dragonMessages');
        if (messagesContainer) {
            // Clear if it's a system message from reload
            if (role === 'system' && content.includes('reloaded')) {
                messagesContainer.innerHTML = '';
            }
            
            const messageEl = document.createElement('div');
            messageEl.className = `chat-message ${role}`;
            const icon = role === 'user' ? '👤' : role === 'system' ? 'ℹ️' : '🐉';
            messageEl.innerHTML = `
                <div class="chat-message-icon">${icon}</div>
                <div class="chat-message-content">
                    <div class="chat-message-role">${role}</div>
                    <div class="chat-message-text">${content}</div>
                </div>
            `;
            messagesContainer.appendChild(messageEl);
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }
    }
}
