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
        this.isProcessing = false;
        this.name = `Session ${id}`;
    }

    connect() {
        this.ws = new WebSocketClient('/dragon');

        // Set up session not found handler before connecting
        this.ws.onSessionNotFound = (data) => this.handleSessionNotFound(data);

        this.ws.connect(this.sessionId);

        this.ws.on('message', (data) => this.handleMessage(data));
        this.ws.onStatusChange((status) => {
            this.isConnected = status === 'connected';
            this.view.updateTabStatus(this.id, this.isConnected);
        });
    }

    /**
     * Handle session not found notification from server
     */
    handleSessionNotFound(data) {
        console.log(`[Session ${this.id}] Session not found:`, data);

        // If we have local messages, show recovery modal
        if (this.messages.length > 0) {
            this.view.showSessionRecoveryModal(this, data);
        } else {
            // No local messages, just accept the new session
            this.acceptNewSession(data.newSessionId);
        }
    }

    /**
     * Accept a new session ID from the server (discards local history)
     */
    acceptNewSession(newSessionId) {
        this.sessionId = newSessionId;
        this.ws.setSessionId(newSessionId);
        this.messages = [];
        this.receivedMessageIds.clear();
        this.view.saveAllSessions();
        console.log(`[Session ${this.id}] Accepted new session:`, newSessionId);
    }

    /**
     * Replay local messages to restore server context
     */
    replayMessages() {
        const msgs = this.messages
            .filter(m => m.role === 'user' || m.role === 'assistant')
            .map(m => ({ role: m.role, content: m.content }));

        console.log(`[Session ${this.id}] Replaying ${msgs.length} messages to server`);
        this.ws.sendReplayRequest(msgs);
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
        } else if (data.type === 'session_replay_complete') {
            console.log(`[Session ${this.id}] Session replay complete:`, data.messagesProcessed, 'messages');
            this.view.showNotification(`Conversation restored (${data.messagesProcessed} messages)`, 'success');
            return;
        } else if (data.type === 'session_replay_error') {
            console.error(`[Session ${this.id}] Session replay error:`, data.error);
            this.view.showNotification(`Failed to restore conversation: ${data.error}`, 'error');
            return;
        } else if (data.type === 'dragon_message') {
            // Check if this is a completion of a streaming response.
            // Note: isStreamed may be true even if no streaming chunks arrived (e.g., after tool use)
            const streamingElementExists = this.view.hasStreamingElement();
            if (data.isStreamed && streamingElementExists) {
                // Finalize the streaming message display (remove cursor)
                this.view.finalizeStreamingMessage();
            }

            // Always save the message from server response (handles both streamed and non-streamed)
            // Use data.message with fallback - prevents "undefined" display
            const content = data.message ?? data.content ?? '';
            if (content) {
                if (data.isStreamed && streamingElementExists) {
                    // For streamed: save without UI update (streaming element already displays it)
                    const msg = { role: 'assistant', content };
                    if (data.messageId) msg.messageId = data.messageId;
                    this.messages.push(msg);
                    this.view.saveAllSessions();
                } else {
                    // For non-streamed OR streamed without chunks: normal flow with UI update
                    this.addMessage('assistant', content, data.messageId);
                }
            }

            // Hide thinking indicator and re-enable input
            this.isProcessing = false;
            this.view.hideThinkingIndicator();
            this.view.setInputEnabled(true);
        } else if (data.type === 'dragon_stream') {
            // Handle streaming chunk - append to current streaming message
            this.view.appendStreamingChunk(data.chunk);
        } else if (data.type === 'dragon_reloaded') {
            // Hide thinking indicator and clear session on reload
            this.isProcessing = false;
            this.view.hideThinkingIndicator();
            this.view.setInputEnabled(true);
            this.clearMessages();
            this.sessionId = data.sessionId;
            this.ws.setSessionId(this.sessionId);
            this.ws.clearMessageHistory();
            this.addMessage('system', data.message, data.messageId);
            this.view.saveAllSessions();
        } else if (data.type === 'dragon_typing') {
            // Show initial thinking indicator and disable input
            this.isProcessing = true;
            this.view.showThinkingIndicator();
            this.view.setInputEnabled(false);
        } else if (data.type === 'dragon_thinking') {
            // Update thinking indicator with tool/processing info
            this.view.updateThinkingIndicator(data.description, data.toolName);
        } else if (data.type === 'context_cleared') {
            // Handle context cleared notification
            this.addMessage('system', data.message || 'Conversation context cleared.', data.messageId);
        } else if (data.type === 'specification_created') {
            // Handle specification created notification
            const projectName = data.projectFolder ? data.projectFolder.split(/[/\\]/).pop() : 'project';
            const message = `✅ Specification created for project: **${projectName}**\n📄 File: \`${data.filename}\``;
            this.addMessage('system', message, data.messageId);
            this.view.showNotification(`Specification created: ${projectName}`, 'success');
        } else if (data.type === 'error') {
            // Hide thinking indicator and re-enable input on error
            this.isProcessing = false;
            this.view.hideThinkingIndicator();
            this.view.setInputEnabled(true);
            this.addErrorMessage(data);
        } else if (data.type !== 'user_message') {
            // Fallback for unknown message types - only show if content exists
            if (data.content) {
                this.addMessage('assistant', data.content, data.messageId);
            } else {
                console.warn(`[Session ${this.id}] Received message with unknown type and no content:`, data.type, data);
            }
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

    /**
     * Clear the context (messages) without reloading the agent
     */
    clearContext() {
        if (this.ws && this.sessionId) {
            this.ws.send({
                type: 'clear_context',
                sessionId: this.sessionId
            });
        }
        // Clear local messages
        this.clearMessages();
        // Add a system message indicating context was cleared
        this.addMessage('system', 'Context cleared - conversation memory has been reset.');
        this.view.saveAllSessions();
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

        // Hide thinking indicator from previous session
        this.hideThinkingIndicator();

        this.activeSessionId = id;
        this.saveAllSessions();
        this.renderTabs();
        this.renderActiveSessionMessages();

        // Sync input state with new session
        const session = this.sessions.get(id);
        this.setInputEnabled(!session?.isProcessing);
        if (session?.isProcessing) {
            this.showThinkingIndicator();
        }
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
                        <button class="btn btn-secondary" id="dragonClearBtn" title="Clear conversation context (keeps agent loaded)">
                            <span>🧹 Clear Context</span>
                        </button>
                        <button class="btn btn-secondary" id="dragonDownloadBtn" title="Download conversation as text file">
                            <span>📥 Download</span>
                        </button>
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
            // Special styling for context cleared indicator
            if (msg.role === 'system' && msg.content.includes('Context cleared')) {
                return this.renderContextClearedHtml(msg);
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

    renderContextClearedHtml(msg) {
        return `
            <div class="chat-message context-cleared" style="background: linear-gradient(135deg, #dbeafe 0%, #bfdbfe 100%); border: 1px solid #60a5fa; border-left: 4px solid #3b82f6; justify-content: center;">
                <div class="chat-message-icon" style="color: #3b82f6;">🧹</div>
                <div class="chat-message-content" style="text-align: center;">
                    <div class="chat-message-text" style="color: #1e40af; font-style: italic;">${this.escapeHtml(msg.content)}</div>
                </div>
            </div>
        `;
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

        // Scroll to last message
        const messagesContainer = document.getElementById('dragonMessages');
        if (messagesContainer) {
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }

        // Sync input state with active session
        const activeSession = this.sessions.get(this.activeSessionId);
        if (activeSession?.isProcessing) {
            this.setInputEnabled(false);
            this.showThinkingIndicator();
        }
    }

    attachEventListeners() {
        const sendBtn = document.getElementById('dragonSendBtn');
        const input = document.getElementById('dragonInput');
        const reloadBtn = document.getElementById('dragonReloadBtn');
        const clearBtn = document.getElementById('dragonClearBtn');
        const downloadBtn = document.getElementById('dragonDownloadBtn');
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
        clearBtn?.addEventListener('click', () => this.clearContext());
        downloadBtn?.addEventListener('click', () => this.downloadConversation());
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
            if (session && !session.isProcessing) {
                session.sendMessage(message);
                input.value = '';
                // Disable input immediately while waiting for response
                session.isProcessing = true;
                this.setInputEnabled(false);
                this.showThinkingIndicator();
            }
        }
    }

    reloadAgent() {
        const session = this.sessions.get(this.activeSessionId);
        if (session) {
            session.reloadAgent(this.selectedProvider);
        }
    }

    /**
     * Clear the context for the active session
     */
    clearContext() {
        const session = this.sessions.get(this.activeSessionId);
        if (session) {
            session.clearContext();
            this.renderActiveSessionMessages();
            this.showNotification('Context cleared', 'info');
        }
    }

    /**
     * Download the conversation as a text file
     */
    downloadConversation() {
        const session = this.sessions.get(this.activeSessionId);
        if (!session || session.messages.length === 0) {
            this.showNotification('No messages to download', 'error');
            return;
        }

        // Format messages as text
        const lines = [];
        lines.push(`Dragon Conversation - ${session.name}`);
        lines.push(`Exported: ${new Date().toLocaleString()}`);
        lines.push('='.repeat(50));
        lines.push('');

        session.messages.forEach(msg => {
            const role = msg.role.charAt(0).toUpperCase() + msg.role.slice(1);
            const icon = msg.role === 'user' ? '[User]' :
                         msg.role === 'assistant' ? '[Dragon]' :
                         msg.role === 'system' ? '[System]' :
                         msg.role === 'error' ? '[Error]' : `[${role}]`;

            lines.push(icon);
            lines.push(msg.content);
            if (msg.details) {
                lines.push(`Details: ${msg.details}`);
            }
            lines.push('');
            lines.push('-'.repeat(50));
            lines.push('');
        });

        const content = lines.join('\n');

        // Create and trigger download
        const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
        const safeName = session.name.replace(/[^a-z0-9]/gi, '_');
        a.download = `dragon-${safeName}-${timestamp}.txt`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);

        this.showNotification('Conversation downloaded', 'success');
    }

    appendMessageToUI(msg, role) {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return;

        // Clear empty state if present
        const emptyState = messagesContainer.querySelector('.empty-state');
        if (emptyState) {
            emptyState.remove();
        }

        // Guard against empty or undefined content
        if (!msg.content) return;

        // Clear if system reload message
        if (role === 'system' && msg.content.includes('reloaded')) {
            messagesContainer.innerHTML = '';
        }

        const messageEl = document.createElement('div');

        // Special styling for context cleared indicator
        if (role === 'system' && msg.content.includes('Context cleared')) {
            messageEl.className = 'chat-message context-cleared';
            messageEl.style.cssText = `
                background: linear-gradient(135deg, #dbeafe 0%, #bfdbfe 100%);
                border: 1px solid #60a5fa;
                border-left: 4px solid #3b82f6;
                justify-content: center;
            `;
            messageEl.innerHTML = `
                <div class="chat-message-icon" style="color: #3b82f6;">🧹</div>
                <div class="chat-message-content" style="text-align: center;">
                    <div class="chat-message-text" style="color: #1e40af; font-style: italic;">${this.escapeHtml(msg.content)}</div>
                </div>
            `;
        } else {
            messageEl.className = `chat-message ${role}`;
            const icon = role === 'user' ? '👤' : role === 'system' ? 'ℹ️' : '🐉';
            messageEl.innerHTML = `
                <div class="chat-message-icon">${icon}</div>
                <div class="chat-message-content">
                    <div class="chat-message-role">${role}</div>
                    <div class="chat-message-text">${msg.content}</div>
                </div>
            `;
        }

        messagesContainer.appendChild(messageEl);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    /**
     * Append a streaming chunk to the current assistant message.
     * Creates a new message element if one doesn't exist for streaming.
     */
    appendStreamingChunk(chunk) {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return;

        // Clear empty state if present
        const emptyState = messagesContainer.querySelector('.empty-state');
        if (emptyState) {
            emptyState.remove();
        }

        // Look for an existing streaming message (marked with data-streaming attribute)
        let streamingMessage = messagesContainer.querySelector('.chat-message[data-streaming="true"]');
        
        if (!streamingMessage) {
            // Create new streaming message element
            streamingMessage = document.createElement('div');
            streamingMessage.className = 'chat-message assistant';
            streamingMessage.setAttribute('data-streaming', 'true');
            streamingMessage.innerHTML = `
                <div class="chat-message-icon">🐉</div>
                <div class="chat-message-content">
                    <div class="chat-message-role">assistant</div>
                    <div class="chat-message-text"></div>
                    <div class="streaming-cursor">▊</div>
                </div>
            `;
            messagesContainer.appendChild(streamingMessage);
        }

        // Append chunk to the message text
        const textElement = streamingMessage.querySelector('.chat-message-text');
        if (textElement) {
            textElement.textContent += chunk;
        }

        // Auto-scroll to bottom
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    /**
     * Check if a streaming message element currently exists.
     * Used to determine if streaming chunks were actually received.
     */
    hasStreamingElement() {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return false;
        return !!messagesContainer.querySelector('.chat-message[data-streaming="true"]');
    }

    /**
     * Finalize the streaming message display (remove cursor).
     * Note: Message saving is handled by handleMessage to ensure it works even when not on view.
     */
    finalizeStreamingMessage() {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return;

        const streamingMessage = messagesContainer.querySelector('.chat-message[data-streaming="true"]');
        if (streamingMessage) {
            // Remove streaming indicator
            streamingMessage.removeAttribute('data-streaming');
            const cursor = streamingMessage.querySelector('.streaming-cursor');
            if (cursor) {
                cursor.remove();
            }
            // Message is saved by handleMessage, not here - ensures it works when not on view
        }
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

    /**
     * Show a notification message
     * @param {string} message - Message to display
     * @param {string} type - 'success', 'error', or 'info'
     */
    showNotification(message, type = 'info') {
        // Remove any existing notification
        const existing = document.querySelector('.notification');
        if (existing) {
            existing.remove();
        }

        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.textContent = message;
        document.body.appendChild(notification);

        // Trigger animation
        requestAnimationFrame(() => {
            notification.classList.add('show');
        });

        // Auto-remove after 4 seconds
        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => notification.remove(), 300);
        }, 4000);
    }

    /**
     * Show the thinking indicator
     */
    showThinkingIndicator() {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return;

        // Remove existing indicator if present
        this.hideThinkingIndicator();

        // Clear empty state if present
        const emptyState = messagesContainer.querySelector('.empty-state');
        if (emptyState) {
            emptyState.remove();
        }

        const indicator = document.createElement('div');
        indicator.className = 'thinking-indicator';
        indicator.id = 'dragonThinkingIndicator';
        indicator.innerHTML = `
            <div class="thinking-icon">
                <span class="thinking-spinner"></span>
            </div>
            <div class="thinking-content">
                <div class="thinking-label">Dragon is thinking...</div>
                <div class="thinking-details" id="thinkingDetails"></div>
            </div>
        `;
        messagesContainer.appendChild(indicator);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    /**
     * Update the thinking indicator with current processing info
     * @param {string} description - What the agent is doing
     * @param {string|null} toolName - Name of the tool being used (optional)
     */
    updateThinkingIndicator(description, toolName = null) {
        const indicator = document.getElementById('dragonThinkingIndicator');
        if (!indicator) {
            // If indicator doesn't exist yet, create it
            this.showThinkingIndicator();
        }

        const detailsEl = document.getElementById('thinkingDetails');
        if (detailsEl) {
            if (toolName) {
                detailsEl.innerHTML = `<span class="thinking-tool">${this.escapeHtml(toolName)}</span> ${this.escapeHtml(description)}`;
            } else {
                detailsEl.textContent = description;
            }
        }

        // Ensure scroll to bottom
        const messagesContainer = document.getElementById('dragonMessages');
        if (messagesContainer) {
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }
    }

    /**
     * Hide the thinking indicator
     */
    hideThinkingIndicator() {
        const indicator = document.getElementById('dragonThinkingIndicator');
        if (indicator) {
            indicator.remove();
        }
    }

    /**
     * Enable or disable the input textarea and send button
     * @param {boolean} enabled - Whether input should be enabled
     */
    setInputEnabled(enabled) {
        const input = document.getElementById('dragonInput');
        const sendBtn = document.getElementById('dragonSendBtn');

        if (input) {
            input.disabled = !enabled;
            input.placeholder = enabled
                ? 'Type your message here...'
                : 'Dragon is thinking...';
        }
        if (sendBtn) {
            sendBtn.disabled = !enabled;
        }
    }

    /**
     * Show session recovery modal when server session is not found
     * @param {DragonSession} session - The session that needs recovery
     * @param {Object} data - Server response with newSessionId and reason
     */
    showSessionRecoveryModal(session, data) {
        // Remove any existing modal
        const existingModal = document.querySelector('.modal.session-recovery');
        if (existingModal) {
            existingModal.remove();
        }

        const reasonText = data.reason === 'expired'
            ? 'Your session expired due to inactivity.'
            : 'The server may have restarted or the session was cleared.';

        const messageCount = session.messages.filter(m => m.role === 'user' || m.role === 'assistant').length;

        const modal = document.createElement('div');
        modal.className = 'modal session-recovery';
        modal.innerHTML = `
            <div class="modal-content session-recovery-modal">
                <div class="modal-header">
                    <h3>Session Recovery</h3>
                    <button class="modal-close" data-action="dismiss">×</button>
                </div>
                <div class="modal-body">
                    <div class="recovery-warning">
                        <strong>Session not found on server</strong>
                        <p>${reasonText}</p>
                    </div>
                    <p>You have <strong>${messageCount} conversation messages</strong> stored locally.</p>
                    <p style="margin-top: var(--spacing-sm); font-size: 12px; color: var(--text-secondary);">
                        Choose how to proceed:
                    </p>
                    <div class="recovery-options">
                        <button class="btn btn-primary" data-action="replay">
                            Restore Conversation
                            <span style="font-size: 10px; opacity: 0.8; display: block;">AI will remember your conversation</span>
                        </button>
                        <button class="btn btn-secondary" data-action="fresh">
                            Start Fresh
                            <span style="font-size: 10px; opacity: 0.8; display: block;">Clear local history, begin new conversation</span>
                        </button>
                        <button class="btn btn-tertiary" data-action="keep">
                            Keep Local (Read-Only)
                            <span style="font-size: 10px; opacity: 0.8; display: block;">Preserve history, but AI won't remember it</span>
                        </button>
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(modal);

        // Handle button clicks
        modal.addEventListener('click', (e) => {
            const action = e.target.closest('[data-action]')?.dataset.action;
            if (!action) return;

            modal.remove();

            switch (action) {
                case 'replay':
                    // Update to new session ID and replay messages
                    session.sessionId = data.newSessionId;
                    session.ws.setSessionId(data.newSessionId);
                    session.replayMessages();
                    this.showNotification('Restoring conversation...', 'info');
                    break;

                case 'fresh':
                    // Accept new session and clear local messages
                    session.acceptNewSession(data.newSessionId);
                    this.renderActiveSessionMessages();
                    this.showNotification('Started fresh conversation', 'info');
                    break;

                case 'keep':
                    // Keep local history but use new session (AI won't have context)
                    session.sessionId = data.newSessionId;
                    session.ws.setSessionId(data.newSessionId);
                    session.receivedMessageIds.clear();
                    this.saveAllSessions();
                    this.showNotification('Local history preserved (AI has no memory of it)', 'info');
                    break;

                case 'dismiss':
                    // Same as 'keep' - just dismiss the modal
                    session.sessionId = data.newSessionId;
                    session.ws.setSessionId(data.newSessionId);
                    session.receivedMessageIds.clear();
                    this.saveAllSessions();
                    break;
            }
        });

        // Close on backdrop click
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                // Treat as dismiss
                modal.remove();
                session.sessionId = data.newSessionId;
                session.ws.setSessionId(data.newSessionId);
                session.receivedMessageIds.clear();
                this.saveAllSessions();
            }
        });
    }
}
