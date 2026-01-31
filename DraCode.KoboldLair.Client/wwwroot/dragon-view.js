import { WebSocketClient } from './websocket.js';

const DRAGON_SESSION_KEY = 'dragon_session_id';
const DRAGON_MESSAGES_KEY = 'dragon_messages';

export class DragonView {
    constructor(api) {
        this.api = api;
        this.ws = null;
        this.messages = [];
        this.sessionId = null;
        this.providers = [];
        this.selectedProvider = null;
        this.receivedMessageIds = new Set();

        // Load session from localStorage
        this.loadSession();
    }

    /**
     * Load session data from localStorage
     */
    loadSession() {
        try {
            const storedSessionId = localStorage.getItem(DRAGON_SESSION_KEY);
            if (storedSessionId) {
                this.sessionId = storedSessionId;
                console.log('Loaded session ID from storage:', this.sessionId);
            }

            const storedMessages = localStorage.getItem(DRAGON_MESSAGES_KEY);
            if (storedMessages) {
                this.messages = JSON.parse(storedMessages);
                // Rebuild receivedMessageIds from stored messages
                this.messages.forEach(msg => {
                    if (msg.messageId) {
                        this.receivedMessageIds.add(msg.messageId);
                    }
                });
                console.log('Loaded', this.messages.length, 'messages from storage');
            }
        } catch (error) {
            console.error('Failed to load session from storage:', error);
            this.clearSession();
        }
    }

    /**
     * Save session data to localStorage
     */
    saveSession() {
        try {
            if (this.sessionId) {
                localStorage.setItem(DRAGON_SESSION_KEY, this.sessionId);
            }
            // Only keep the last 100 messages in storage
            const messagesToStore = this.messages.slice(-100);
            localStorage.setItem(DRAGON_MESSAGES_KEY, JSON.stringify(messagesToStore));
        } catch (error) {
            console.error('Failed to save session to storage:', error);
        }
    }

    /**
     * Clear session data from localStorage
     */
    clearSession() {
        localStorage.removeItem(DRAGON_SESSION_KEY);
        localStorage.removeItem(DRAGON_MESSAGES_KEY);
        this.sessionId = null;
        this.messages = [];
        this.receivedMessageIds.clear();
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
                    ${this.messages.length > 0 ? this.messages.filter(msg => msg.content).map(msg => `
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

        // Pass stored sessionId when connecting
        this.ws.connect(this.sessionId);

        this.ws.on('message', (data) => {
            // Track sessionId from server
            if (data.sessionId && data.sessionId !== this.sessionId) {
                this.sessionId = data.sessionId;
                this.ws.setSessionId(this.sessionId);
                this.saveSession();
                console.log('Session ID updated:', this.sessionId);
            }

            // Skip replay messages - they're just for catching up the connection
            // We already have these in our local message history
            if (data.isReplay) {
                console.log('Skipping replay message:', data.messageId);
                return;
            }

            // Deduplicate by messageId
            if (data.messageId && this.receivedMessageIds.has(data.messageId)) {
                console.log('Skipping duplicate message:', data.messageId);
                return;
            }
            if (data.messageId) {
                this.receivedMessageIds.add(data.messageId);
            }

            if (data.type === 'session_resumed') {
                console.log('Session resumed, message count:', data.messageCount);
                // Session was resumed, we already have messages loaded from localStorage
                return;
            } else if (data.type === 'dragon_message') {
                this.addMessage('assistant', data.message, data.messageId);
            } else if (data.type === 'dragon_reloaded') {
                // Clear session on reload
                this.clearSession();
                this.sessionId = data.sessionId;
                this.ws.setSessionId(this.sessionId);
                this.ws.clearMessageHistory();
                this.addMessage('system', data.message, data.messageId);
                this.saveSession();
                // Clear and re-render messages container
                const messagesContainer = document.getElementById('dragonMessages');
                if (messagesContainer) {
                    messagesContainer.innerHTML = '';
                }
            } else if (data.type === 'dragon_typing') {
                // Could show typing indicator here
            } else if (data.type === 'error') {
                this.addErrorMessage(data);
            } else if (data.type !== 'user_message') {
                // Ignore user_message type (tracked but not displayed as server message)
                this.addMessage('assistant', data.content || JSON.stringify(data), data.messageId);
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
        this.saveSession();
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
            this.saveSession();
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

    addMessage(role, content, messageId = null) {
        const msg = { role, content };
        if (messageId) {
            msg.messageId = messageId;
        }
        this.messages.push(msg);
        this.saveSession();
        const messagesContainer = document.getElementById('dragonMessages');
        if (messagesContainer) {
            // Clear if it's a system message from reload
            if (role === 'system' && content.includes('reloaded')) {
                messagesContainer.innerHTML = '';
            }
            if (content === '') return;
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

    addErrorMessage(errorData) {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return;

        const errorType = errorData.errorType || 'general';
        const message = errorData.message || 'An error occurred';
        const details = errorData.details || '';

        // Store in messages array
        this.messages.push({ role: 'error', content: message, details });

        // Create error message element with distinct styling
        const messageEl = document.createElement('div');
        messageEl.className = 'chat-message error';
        messageEl.style.cssText = `
            background: linear-gradient(135deg, #fee2e2 0%, #fecaca 100%);
            border: 1px solid #f87171;
            border-left: 4px solid #dc2626;
        `;

        // Icon based on error type
        const icon = errorType === 'llm_connection' ? '🔌' :
            errorType === 'llm_timeout' ? '⏱️' :
                errorType === 'llm_error' ? '🤖' :
                    errorType === 'llm_response' ? '📄' : '⚠️';

        const errorTitle = errorType === 'llm_connection' ? 'Connection Error' :
            errorType === 'llm_timeout' ? 'Timeout Error' :
                errorType === 'llm_error' ? 'LLM Provider Error' :
                    errorType === 'llm_response' ? 'Response Error' :
                        errorType === 'startup_error' ? 'Startup Error' : 'Error';

        messageEl.innerHTML = `
            <div class="chat-message-icon" style="color: #dc2626;">${icon}</div>
            <div class="chat-message-content">
                <div class="chat-message-role" style="color: #dc2626; font-weight: bold;">${errorTitle}</div>
                <div class="chat-message-text" style="color: #7f1d1d;">
                    <strong>${this.escapeHtml(message)}</strong>
                    ${details ? `<div style="margin-top: 8px; font-size: 0.9em; color: #991b1b;">${this.escapeHtml(details)}</div>` : ''}
                </div>
            </div>
        `;

        messagesContainer.appendChild(messageEl);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}
