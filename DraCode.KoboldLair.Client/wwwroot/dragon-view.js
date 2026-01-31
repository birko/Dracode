import { WebSocketClient } from './websocket.js';

const DRAGON_SESSIONS_KEY = 'dragon_sessions';

/**
 * Represents a single Dragon session with its own WebSocket connection
 */
class DragonSession {
    constructor(id, view) {
        this.id = id;
        this.view = view;
        this.ws = null;
        this.messages = [];
        this.sessionId = null;
        this.receivedMessageIds = new Set();
        this.isConnected = false;
        this.name = `Session ${id}`;
    }

    connect() {
        this.ws = new WebSocketClient('/dragon');
        this.ws.connect(this.sessionId);

        this.ws.on('message', (data) => this.handleMessage(data));
        this.ws.onStatusChange((status) => {
            this.isConnected = status === 'connected';
            this.view.updateTabStatus(this.id, this.isConnected);
        });
    }

    handleMessage(data) {
        // Track sessionId from server
        if (data.sessionId && data.sessionId !== this.sessionId) {
            this.sessionId = data.sessionId;
            this.ws.setSessionId(this.sessionId);
            this.view.saveAllSessions();
            console.log(`[Session ${this.id}] Session ID updated:`, this.sessionId);
        }

        // Deduplicate by messageId - this handles both replay and regular duplicate messages
        if (data.messageId && this.receivedMessageIds.has(data.messageId)) {
            console.log(`[Session ${this.id}] Skipping duplicate message:`, data.messageId, data.isReplay ? '(replay)' : '');
            return;
        }
        if (data.messageId) {
            this.receivedMessageIds.add(data.messageId);
        }

        // Log replay messages that were accepted (not already in local state)
        if (data.isReplay) {
            console.log(`[Session ${this.id}] Accepted replay message:`, data.messageId);
        }

        if (data.type === 'session_resumed') {
            console.log(`[Session ${this.id}] Session resumed, message count:`, data.messageCount);
            return;
        } else if (data.type === 'dragon_message') {
            this.addMessage('assistant', data.message, data.messageId);
        } else if (data.type === 'dragon_reloaded') {
            // Clear session on reload
            this.clearMessages();
            this.sessionId = data.sessionId;
            this.ws.setSessionId(this.sessionId);
            this.ws.clearMessageHistory();
            this.addMessage('system', data.message, data.messageId);
            this.view.saveAllSessions();
        } else if (data.type === 'dragon_typing') {
            // Could show typing indicator here
        } else if (data.type === 'error') {
            this.addErrorMessage(data);
        } else if (data.type !== 'user_message') {
            this.addMessage('assistant', data.content || JSON.stringify(data), data.messageId);
        }
    }

    addMessage(role, content, messageId = null) {
        const msg = { role, content };
        if (messageId) {
            msg.messageId = messageId;
        }
        this.messages.push(msg);
        this.view.saveAllSessions();

        // Only update UI if this is the active session
        if (this.view.activeSessionId === this.id) {
            this.view.appendMessageToUI(msg, role);
        }
    }

    addErrorMessage(errorData) {
        const errorType = errorData.errorType || 'general';
        const message = errorData.message || 'An error occurred';
        const details = errorData.details || '';

        this.messages.push({ role: 'error', content: message, details, errorType });
        this.view.saveAllSessions();

        if (this.view.activeSessionId === this.id) {
            this.view.appendErrorToUI(errorData);
        }
    }

    clearMessages() {
        this.messages = [];
        this.receivedMessageIds.clear();
    }

    sendMessage(message) {
        if (message && this.ws) {
            this.addMessage('user', message);
            this.ws.send({
                message: message,
                sessionId: this.sessionId
            });
        }
    }

    reloadAgent(provider) {
        if (this.ws && this.sessionId) {
            this.ws.send({
                type: 'reload',
                sessionId: this.sessionId,
                provider: provider
            });
        }
    }

    disconnect() {
        this.ws?.disconnect();
        this.ws = null;
        this.isConnected = false;
    }

    toStorageObject() {
        return {
            id: this.id,
            name: this.name,
            sessionId: this.sessionId,
            messages: this.messages.slice(-100) // Keep last 100 messages
        };
    }

    loadFromStorage(data) {
        this.name = data.name || `Session ${this.id}`;
        this.sessionId = data.sessionId;
        this.messages = data.messages || [];
        // Rebuild receivedMessageIds
        this.messages.forEach(msg => {
            if (msg.messageId) {
                this.receivedMessageIds.add(msg.messageId);
            }
        });
    }
}

export class DragonView {
    constructor(api) {
        this.api = api;
        this.sessions = new Map(); // id -> DragonSession
        this.activeSessionId = null;
        this.nextSessionId = 1;
        this.providers = [];
        this.selectedProvider = null;

        // Load sessions from localStorage
        this.loadAllSessions();
    }

    /**
     * Load all sessions from localStorage
     */
    loadAllSessions() {
        try {
            const stored = localStorage.getItem(DRAGON_SESSIONS_KEY);
            if (stored) {
                const data = JSON.parse(stored);
                this.nextSessionId = data.nextSessionId || 1;
                this.activeSessionId = data.activeSessionId;

                if (data.sessions && data.sessions.length > 0) {
                    data.sessions.forEach(sessionData => {
                        const session = new DragonSession(sessionData.id, this);
                        session.loadFromStorage(sessionData);
                        this.sessions.set(session.id, session);
                    });
                    console.log('Loaded', this.sessions.size, 'sessions from storage');
                }
            }

            // Ensure we have at least one session
            if (this.sessions.size === 0) {
                this.createNewSession();
            }

            // Ensure activeSessionId is valid
            if (!this.sessions.has(this.activeSessionId)) {
                this.activeSessionId = this.sessions.keys().next().value;
            }
        } catch (error) {
            console.error('Failed to load sessions from storage:', error);
            this.sessions.clear();
            this.createNewSession();
        }
    }

    /**
     * Save all sessions to localStorage
     */
    saveAllSessions() {
        try {
            const data = {
                nextSessionId: this.nextSessionId,
                activeSessionId: this.activeSessionId,
                sessions: Array.from(this.sessions.values()).map(s => s.toStorageObject())
            };
            localStorage.setItem(DRAGON_SESSIONS_KEY, JSON.stringify(data));
        } catch (error) {
            console.error('Failed to save sessions to storage:', error);
        }
    }

    /**
     * Create a new session
     */
    createNewSession() {
        const id = this.nextSessionId++;
        const session = new DragonSession(id, this);
        this.sessions.set(id, session);
        this.activeSessionId = id;
        this.saveAllSessions();
        return session;
    }

    /**
     * Close a session
     */
    closeSession(id) {
        const session = this.sessions.get(id);
        if (!session) return;

        session.disconnect();
        this.sessions.delete(id);

        // If we closed the active session, switch to another
        if (this.activeSessionId === id) {
            if (this.sessions.size > 0) {
                this.activeSessionId = this.sessions.keys().next().value;
            } else {
                // Create a new session if all closed
                this.createNewSession();
            }
        }

        this.saveAllSessions();
        this.renderTabs();
        this.renderActiveSessionMessages();
    }

    /**
     * Switch to a session
     */
    switchToSession(id) {
        if (!this.sessions.has(id)) return;
        if (this.activeSessionId === id) return;

        this.activeSessionId = id;
        this.saveAllSessions();
        this.renderTabs();
        this.renderActiveSessionMessages();
    }

    /**
     * Update tab connection status indicator
     */
    updateTabStatus(sessionId, isConnected) {
        const tab = document.querySelector(`.dragon-tab[data-session-id="${sessionId}"]`);
        if (tab) {
            const statusDot = tab.querySelector('.tab-status-dot');
            if (statusDot) {
                statusDot.className = `tab-status-dot ${isConnected ? 'connected' : 'disconnected'}`;
            }
        }
    }

    async render() {
        // Load available providers
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
                <div class="dragon-tabs-container">
                    <div class="dragon-tabs" id="dragonTabs">
                        ${this.renderTabsHtml()}
                    </div>
                    <button class="dragon-tab-add" id="dragonAddTab" title="New Session">+</button>
                </div>
                <div class="chat-messages" id="dragonMessages">
                    ${this.renderMessagesHtml()}
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

    renderTabsHtml() {
        return Array.from(this.sessions.values()).map(session => `
            <div class="dragon-tab ${session.id === this.activeSessionId ? 'active' : ''}"
                 data-session-id="${session.id}">
                <span class="tab-status-dot ${session.isConnected ? 'connected' : 'disconnected'}"></span>
                <span class="tab-name">${this.escapeHtml(session.name)}</span>
                ${this.sessions.size > 1 ? `
                    <button class="tab-close" data-session-id="${session.id}" title="Close Session">×</button>
                ` : ''}
            </div>
        `).join('');
    }

    renderMessagesHtml() {
        const session = this.sessions.get(this.activeSessionId);
        if (!session || session.messages.length === 0) {
            return `
                <div class="empty-state">
                    <div class="empty-state-icon">🐉</div>
                    <div>Start a conversation with Dragon</div>
                </div>
            `;
        }

        return session.messages.filter(msg => msg.content).map(msg => {
            if (msg.role === 'error') {
                return this.renderErrorMessageHtml(msg);
            }
            return `
                <div class="chat-message ${msg.role}">
                    <div class="chat-message-icon">${msg.role === 'user' ? '👤' : msg.role === 'system' ? 'ℹ️' : '🐉'}</div>
                    <div class="chat-message-content">
                        <div class="chat-message-role">${msg.role}</div>
                        <div class="chat-message-text">${msg.content}</div>
                    </div>
                </div>
            `;
        }).join('');
    }

    renderErrorMessageHtml(msg) {
        const errorType = msg.errorType || 'general';
        const icon = errorType === 'llm_connection' ? '🔌' :
            errorType === 'llm_timeout' ? '⏱️' :
                errorType === 'llm_error' ? '🤖' :
                    errorType === 'llm_response' ? '📄' : '⚠️';

        const errorTitle = errorType === 'llm_connection' ? 'Connection Error' :
            errorType === 'llm_timeout' ? 'Timeout Error' :
                errorType === 'llm_error' ? 'LLM Provider Error' :
                    errorType === 'llm_response' ? 'Response Error' :
                        errorType === 'startup_error' ? 'Startup Error' : 'Error';

        return `
            <div class="chat-message error" style="background: linear-gradient(135deg, #fee2e2 0%, #fecaca 100%); border: 1px solid #f87171; border-left: 4px solid #dc2626;">
                <div class="chat-message-icon" style="color: #dc2626;">${icon}</div>
                <div class="chat-message-content">
                    <div class="chat-message-role" style="color: #dc2626; font-weight: bold;">${errorTitle}</div>
                    <div class="chat-message-text" style="color: #7f1d1d;">
                        <strong>${this.escapeHtml(msg.content)}</strong>
                        ${msg.details ? `<div style="margin-top: 8px; font-size: 0.9em; color: #991b1b;">${this.escapeHtml(msg.details)}</div>` : ''}
                    </div>
                </div>
            </div>
        `;
    }

    renderTabs() {
        const tabsContainer = document.getElementById('dragonTabs');
        if (tabsContainer) {
            tabsContainer.innerHTML = this.renderTabsHtml();
            this.attachTabEventListeners();
        }
    }

    renderActiveSessionMessages() {
        const messagesContainer = document.getElementById('dragonMessages');
        if (messagesContainer) {
            messagesContainer.innerHTML = this.renderMessagesHtml();
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }
    }

    onMount() {
        // Connect all sessions
        this.sessions.forEach(session => {
            session.connect();
        });

        this.attachEventListeners();
        this.attachTabEventListeners();
    }

    attachEventListeners() {
        const sendBtn = document.getElementById('dragonSendBtn');
        const input = document.getElementById('dragonInput');
        const reloadBtn = document.getElementById('dragonReloadBtn');
        const providerSelect = document.getElementById('dragonProviderSelect');
        const addTabBtn = document.getElementById('dragonAddTab');

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
        addTabBtn?.addEventListener('click', () => this.addNewTab());
    }

    attachTabEventListeners() {
        // Tab click handlers
        document.querySelectorAll('.dragon-tab').forEach(tab => {
            tab.addEventListener('click', (e) => {
                // Don't switch if clicking close button
                if (e.target.classList.contains('tab-close')) return;
                const sessionId = parseInt(tab.dataset.sessionId);
                this.switchToSession(sessionId);
            });
        });

        // Close button handlers
        document.querySelectorAll('.dragon-tab .tab-close').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const sessionId = parseInt(btn.dataset.sessionId);
                this.closeSession(sessionId);
            });
        });
    }

    addNewTab() {
        const session = this.createNewSession();
        session.connect();
        this.renderTabs();
        this.renderActiveSessionMessages();
    }

    onUnmount() {
        this.saveAllSessions();
        this.sessions.forEach(session => {
            session.disconnect();
        });
    }

    sendMessage() {
        const input = document.getElementById('dragonInput');
        const message = input.value.trim();

        if (message) {
            const session = this.sessions.get(this.activeSessionId);
            if (session) {
                session.sendMessage(message);
                input.value = '';
            }
        }
    }

    reloadAgent() {
        const session = this.sessions.get(this.activeSessionId);
        if (session) {
            session.reloadAgent(this.selectedProvider);
        }
    }

    appendMessageToUI(msg, role) {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return;

        // Clear empty state if present
        const emptyState = messagesContainer.querySelector('.empty-state');
        if (emptyState) {
            emptyState.remove();
        }

        if (msg.content === '') return;

        // Clear if system reload message
        if (role === 'system' && msg.content.includes('reloaded')) {
            messagesContainer.innerHTML = '';
        }

        const messageEl = document.createElement('div');
        messageEl.className = `chat-message ${role}`;
        const icon = role === 'user' ? '👤' : role === 'system' ? 'ℹ️' : '🐉';
        messageEl.innerHTML = `
            <div class="chat-message-icon">${icon}</div>
            <div class="chat-message-content">
                <div class="chat-message-role">${role}</div>
                <div class="chat-message-text">${msg.content}</div>
            </div>
        `;
        messagesContainer.appendChild(messageEl);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    appendErrorToUI(errorData) {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return;

        const errorType = errorData.errorType || 'general';
        const message = errorData.message || 'An error occurred';
        const details = errorData.details || '';

        const messageEl = document.createElement('div');
        messageEl.className = 'chat-message error';
        messageEl.style.cssText = `
            background: linear-gradient(135deg, #fee2e2 0%, #fecaca 100%);
            border: 1px solid #f87171;
            border-left: 4px solid #dc2626;
        `;

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
