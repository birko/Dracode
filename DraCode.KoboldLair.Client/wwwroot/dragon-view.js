import { WebSocketClient } from './websocket.js';
import notificationStore from './notification-store.js';

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
        // Clean up previous WebSocket handlers to prevent accumulation on reconnect
        // (Birko WebSocketServer pattern: OnClientDisconnected cleans up before new connection)
        if (this.ws) {
            this.ws.removeAllHandlers();
            this.ws.disconnect();
            this.ws = null;
        }

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
        // Transient messages (no messageId) skip dedup and sessionId tracking
        const isTransient = !data.messageId;

        if (!isTransient) {
            console.log(`[Session ${this.id}] Received:`, data.type, data.messageId);
        }

        // Track sessionId from server (skip for transient messages that don't carry session context)
        if (data.sessionId && data.sessionId !== this.sessionId) {
            console.log(`[Session ${this.id}] Session ID changed: ${this.sessionId} -> ${data.sessionId}`);
            this.sessionId = data.sessionId;
            this.ws.setSessionId(this.sessionId);
        }

        // Deduplicate only tracked messages (those with messageId)
        if (!isTransient) {
            if (this.receivedMessageIds.has(data.messageId)) {
                return; // Duplicate
            }
            this.receivedMessageIds.add(data.messageId);

            // Prune old IDs to prevent unbounded growth (keep last 500)
            if (this.receivedMessageIds.size > 500) {
                const iter = this.receivedMessageIds.values();
                for (let i = 0; i < 100; i++) iter.next();
                // Delete the first 100 (oldest) entries
                const toDelete = [];
                const allIter = this.receivedMessageIds.values();
                for (let i = 0; i < 100; i++) toDelete.push(allIter.next().value);
                toDelete.forEach(id => this.receivedMessageIds.delete(id));
            }
        }

        switch (data.type) {
            // --- Session lifecycle ---
            case 'session_resumed':
                console.log(`[Session ${this.id}] Resumed, ${data.messageCount} messages on server`);
                return;

            case 'session_replay_complete':
                this.view.showNotification(`Conversation restored (${data.messagesProcessed} messages)`, 'success');
                return;

            case 'session_replay_error':
                console.error(`[Session ${this.id}] Replay error:`, data.error);
                this.view.showNotification(`Failed to restore conversation: ${data.error}`, 'error');
                return;

            // --- Dragon response (final message) ---
            case 'dragon_message': {
                const streamingElementExists = this.view.hasStreamingElement();

                if (data.isStreamed && streamingElementExists) {
                    // Finalize the streaming display with the complete content
                    this.view.finalizeStreamingMessage(data.message ?? data.content ?? '');
                }

                const content = data.message ?? data.content ?? '';
                if (content) {
                    if (data.isStreamed && streamingElementExists) {
                        // Streamed: save to history only (UI already shows it)
                        const msg = { role: 'assistant', content };
                        if (data.messageId) msg.messageId = data.messageId;
                        this.messages.push(msg);
                    } else {
                        // Non-streamed or no chunks arrived: render to UI
                        this.addMessage('assistant', content, data.messageId);
                    }
                } else {
                    console.warn(`[Session ${this.id}] dragon_message with no content`);
                }

                // Response complete — reset processing state
                this.isProcessing = false;
                this.view.hideThinkingIndicator();
                this.view.setInputEnabled(true);
                return;
            }

            // --- Streaming chunks ---
            case 'dragon_stream':
                // Hide thinking indicator on first streaming chunk (response is arriving)
                if (!this.view.hasStreamingElement()) {
                    this.view.hideThinkingIndicator();
                }
                this.view.appendStreamingChunk(data.chunk);
                return;

            // --- Agent reloaded ---
            case 'dragon_reloaded':
                this.isProcessing = false;
                this.view.hideThinkingIndicator();
                this.view.setInputEnabled(true);
                this.clearMessages();
                this.sessionId = data.sessionId;
                this.ws.setSessionId(this.sessionId);
                this.addMessage('system', data.message, data.messageId);
                return;

            // --- Processing indicators (transient, no messageId) ---
            case 'dragon_typing':
                // Server confirmed it received our message — only act if we haven't already
                // (sendMessage() already shows indicator optimistically)
                if (!this.isProcessing) {
                    this.isProcessing = true;
                    this.view.showThinkingIndicator();
                    this.view.setInputEnabled(false);
                }
                return;

            case 'dragon_thinking':
                this.view.updateThinkingIndicator(data.description, data.toolName, data.category, data.agent);
                return;

            case 'dragon_status':
                // When status is complete, hide the thinking indicator
                if (data.statusType === 'complete') {
                    this.view.hideThinkingIndicator();
                } else {
                    this.view.updateThinkingIndicator(data.message, null, 'status', null);
                }
                return;

            // --- System notifications ---
            case 'context_cleared':
                this.addMessage('context_cleared', data.message || 'Conversation context cleared.', data.messageId);
                return;

            case 'specification_created': {
                const projectName = data.projectFolder ? data.projectFolder.split(/[/\\]/).pop() : 'project';
                let message = `✅ Specification created for project: ${projectName} (${data.filename})`;

                // Add git status information
                if (data.gitStatus === 'not_installed') {
                    message += `\n\n⚠️ **Git not installed** - Version control is not available. Install git for automatic commit tracking.`;
                } else if (data.gitStatus === 'not_initialized') {
                    message += `\n\n⚠️ **Git not initialized** - The project folder was created but git initialization failed.`;
                } else if (data.gitStatus === 'initialized') {
                    message += `\n\n🔀 **Git initialized** - Automatic version control is enabled for this project.`;
                }

                this.addMessage('system', message, data.messageId);
                this.view.showNotification(`Specification created: ${projectName}`, 'success');
                return;
            }

            case 'project_notification': {
                // Display project notifications with appropriate treatment based on type
                const isEscalation = data.notificationType === 'escalation';
                const notifIcon = data.notificationType === 'feature_branch_ready' ? '🔀' :
                                  data.notificationType === 'project_complete' ? '🎉' :
                                  isEscalation ? '⚠️' : '📢';

                if (isEscalation) {
                    // Escalation notifications get prominent treatment
                    const escalationType = data.metadata?.escalationType || 'Unknown';
                    const resolution = data.metadata?.resolution || '';
                    this.addMessage('system',
                        `${notifIcon} **ESCALATION: ${escalationType}**\n${data.message}` +
                        (resolution ? `\n_Resolution: ${resolution}_` : ''),
                        data.messageId);
                    this.view.showNotification(`⚠️ Escalation: ${data.message}`, 'warning');
                    this.view.incrementNotificationBadge({
                        type: escalationType,
                        message: data.message,
                        taskId: data.metadata?.taskId,
                        resolution: data.metadata?.resolution,
                        timestamp: data.timestamp || new Date().toISOString()
                    });
                } else {
                    this.addMessage('system', `${notifIcon} ${data.message}`, data.messageId);
                    this.view.showNotification(data.message, 'info');
                }
                return;
            }

            // --- Errors ---
            case 'error':
                this.isProcessing = false;
                this.view.hideThinkingIndicator();
                this.view.setInputEnabled(true);
                this.addErrorMessage(data);
                return;

            // --- Ignore echoed user messages ---
            case 'user_message':
                return;

            // --- Unknown types ---
            default:
                if (data.content) {
                    this.addMessage('assistant', data.content, data.messageId);
                } else {
                    console.warn(`[Session ${this.id}] Unknown message type:`, data.type);
                }
        }
    }

    addMessage(role, content, messageId = null) {
        console.log(`[Session ${this.id}] addMessage called:`, { role, contentLength: content?.length, messageId });
        const msg = { role, content };
        if (messageId) {
            msg.messageId = messageId;
        }
        this.messages.push(msg);

        // Only update UI if this is the active session
        if (this.view.activeSessionId === this.id) {
            console.log(`[Session ${this.id}] Calling appendMessageToUI (active session)`);
            this.view.appendMessageToUI(msg, role);
        } else {
            console.log(`[Session ${this.id}] Not active session (active: ${this.view.activeSessionId}), skipping UI update`);
        }
    }

    addErrorMessage(errorData) {
        const errorType = errorData.errorType || 'general';
        const message = errorData.message || 'An error occurred';
        const details = errorData.details || '';

        this.messages.push({ role: 'error', content: message, details, errorType });

        if (this.view.activeSessionId === this.id) {
            this.view.appendErrorToUI(errorData);
        }
    }

    clearMessages() {
        console.log(`[Session ${this.id}] Clearing messages and receivedMessageIds (had ${this.messages.length} messages, ${this.receivedMessageIds.size} IDs)`);
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
        // Clear local messages - server will send context_cleared confirmation
        this.clearMessages();
    }

    disconnect() {
        if (this.ws) {
            this.ws.removeAllHandlers();
            this.ws.disconnect();
            this.ws = null;
        }
        this.isConnected = false;
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
        this.pendingEscalations = []; // Track unread escalation notifications

        // Create initial session (server replays history on connect)
        this.createNewSession();

        // Setup cleanup for page unload (not view switching)
        this._cleanupOnUnload = () => this.cleanup();
        window.addEventListener('beforeunload', this._cleanupOnUnload);
    }

    /**
     * Create a new session
     */
    createNewSession() {
        const id = this.nextSessionId++;
        const session = new DragonSession(id, this);
        this.sessions.set(id, session);
        this.activeSessionId = id;
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
            if (msg.role === 'context_cleared') {
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

    /**
     * Get icon and title for an error type.
     */
    getErrorMeta(errorType) {
        const icons = { llm_connection: '🔌', llm_timeout: '⏱️', llm_error: '🤖', llm_response: '📄' };
        const titles = { llm_connection: 'Connection Error', llm_timeout: 'Timeout Error', llm_error: 'LLM Provider Error', llm_response: 'Response Error', startup_error: 'Startup Error' };
        return { icon: icons[errorType] || '⚠️', title: titles[errorType] || 'Error' };
    }

    renderErrorMessageHtml(msg) {
        const { icon, title } = this.getErrorMeta(msg.errorType || 'general');
        return `
            <div class="chat-message error" style="background: linear-gradient(135deg, #fee2e2 0%, #fecaca 100%); border: 1px solid #f87171; border-left: 4px solid #dc2626;">
                <div class="chat-message-icon" style="color: #dc2626;">${icon}</div>
                <div class="chat-message-content">
                    <div class="chat-message-role" style="color: #dc2626; font-weight: bold;">${title}</div>
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
        // Don't disconnect WebSocket connections when switching views
        // This preserves in-flight LLM requests and session state
        // WebSocket will be properly cleaned up on page unload
    }

    async refresh() {
        // DragonView uses real-time WebSocket updates, so manual refresh is not needed
        // But we can re-render messages and scroll to bottom as a convenience
        this.renderActiveSessionMessages();
        const messagesContainer = document.getElementById('dragonMessages');
        if (messagesContainer) {
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }
    }

    /**
     * Cleanup all sessions and remove page unload handler
     * Called only when the page is actually unloaded, not when switching views
     */
    cleanup() {
        window.removeEventListener('beforeunload', this._cleanupOnUnload);
        this.sessions.forEach(session => {
            session.disconnect();
        });
        this.sessions.clear();
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
        if (!messagesContainer) {
            console.warn('[DragonView] appendMessageToUI: dragonMessages container NOT FOUND!');
            return;
        }
        console.log('[DragonView] appendMessageToUI: container found, appending message');

        // Clear empty state if present
        const emptyState = messagesContainer.querySelector('.empty-state');
        if (emptyState) {
            console.log('[DragonView] Removing empty state');
            emptyState.remove();
        }

        // Guard against empty or undefined content
        if (!msg.content) {
            return;
        }

        const messageEl = document.createElement('div');

        if (role === 'context_cleared') {
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
        console.log('[DragonView] appendStreamingChunk:', { chunk, hasContainer: !!messagesContainer });
        if (!messagesContainer) {
            console.warn('[DragonView] appendStreamingChunk: NO CONTAINER FOUND');
            return;
        }

        // Clear empty state if present
        const emptyState = messagesContainer.querySelector('.empty-state');
        if (emptyState) {
            console.log('[DragonView] Removing empty state for streaming');
            emptyState.remove();
        }

        // Look for an existing streaming message (marked with data-streaming attribute)
        let streamingMessage = messagesContainer.querySelector('.chat-message[data-streaming="true"]');
        console.log('[DragonView] Existing streaming message:', !!streamingMessage);

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
            console.log('[DragonView] Appended chunk, current length:', textElement.textContent.length);
        } else {
            console.warn('[DragonView] No text element found in streaming message');
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
     * Finalize the streaming message display (remove cursor) and optionally update content.
     * If finalContent is provided, updates the text element to ensure complete content is displayed.
     * Note: Message saving is handled by handleMessage to ensure it works even when not on view.
     * @param {string|null} finalContent - The final message content from server (optional)
     */
    finalizeStreamingMessage(finalContent = null) {
        const messagesContainer = document.getElementById('dragonMessages');
        if (!messagesContainer) return;

        const streamingMessage = messagesContainer.querySelector('.chat-message[data-streaming="true"]');
        if (streamingMessage) {
            // Update content if final content provided - ensures complete message is displayed
            // even if streaming chunks were incomplete or not rendered properly
            if (finalContent) {
                const textElement = streamingMessage.querySelector('.chat-message-text');
                if (textElement) {
                    console.log('[DragonView] Updating streaming content with final message, length:', finalContent.length);
                    textElement.textContent = finalContent;
                }
            }

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

        // Reuse renderErrorMessageHtml to avoid duplicated icon/title logic
        const msg = {
            role: 'error',
            content: errorData.message || 'An error occurred',
            details: errorData.details || '',
            errorType: errorData.errorType || 'general'
        };
        const html = this.renderErrorMessageHtml(msg);
        const wrapper = document.createElement('div');
        wrapper.innerHTML = html.trim();
        messagesContainer.appendChild(wrapper.firstChild);
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
     * @param {string} type - 'success', 'error', 'info', or 'warning'
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

        // Warning/escalation notifications stay longer (10s), others auto-dismiss at 4s
        const duration = type === 'warning' ? 10000 : 4000;
        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => notification.remove(), 300);
        }, duration);
    }

    /**
     * Track an escalation and increment the notification badge on the Dragon nav item
     * @param {object} [escalation] - Optional escalation data to track
     */
    incrementNotificationBadge(escalation) {
        if (escalation) {
            this.pendingEscalations.push(escalation);
            notificationStore.addEscalation(escalation);
        }

        const dragonNav = document.querySelector('.nav-item[data-view="dragon"]');
        if (!dragonNav) return;

        let badge = dragonNav.querySelector('.nav-badge');
        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'nav-badge';
            badge.textContent = '0';
            dragonNav.appendChild(badge);
        }
        const count = parseInt(badge.textContent || '0', 10) + 1;
        badge.textContent = count.toString();
        badge.style.display = '';
    }

    /**
     * Clear the notification badge on the Dragon nav item
     */
    clearNotificationBadge() {
        this.pendingEscalations = [];
        notificationStore.clearEscalations();
        const badge = document.querySelector('.nav-item[data-view="dragon"] .nav-badge');
        if (badge) {
            badge.textContent = '0';
            badge.style.display = 'none';
        }
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
                <div class="thinking-label" id="thinkingLabel">Dragon is thinking...</div>
                <div class="thinking-details" id="thinkingDetails"></div>
                <div class="thinking-activity-log" id="thinkingActivityLog"></div>
            </div>
        `;
        messagesContainer.appendChild(indicator);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    /**
     * Update the thinking indicator with current processing info and activity log.
     * @param {string} description - What the agent is doing
     * @param {string|null} toolName - Name of the tool being used (optional)
     * @param {string|null} category - Category: tool, thinking, delegation, warning, status
     * @param {string|null} agent - Which agent is active (Dragon, Sage, Seeker, etc.)
     */
    updateThinkingIndicator(description, toolName = null, category = null, agent = null) {
        const indicator = document.getElementById('dragonThinkingIndicator');
        if (!indicator) {
            this.showThinkingIndicator();
        }

        // Update the main label with agent name
        const labelEl = document.getElementById('thinkingLabel');
        if (labelEl && agent) {
            const agentIcons = { Dragon: '🐉', Sage: '📜', Seeker: '🔍', Sentinel: '🛡️', Warden: '⚙️' };
            const icon = agentIcons[agent] || '';
            labelEl.textContent = `${icon} ${agent} is working...`;
        }

        // Update the current step description
        const detailsEl = document.getElementById('thinkingDetails');
        if (detailsEl) {
            if (toolName) {
                detailsEl.innerHTML = `<span class="thinking-tool">${this.escapeHtml(toolName)}</span> ${this.escapeHtml(description)}`;
            } else {
                detailsEl.textContent = description;
            }
        }

        // Append to activity log (keeps last 6 entries)
        const logEl = document.getElementById('thinkingActivityLog');
        if (logEl && description) {
            const entry = document.createElement('div');
            entry.className = `thinking-log-entry thinking-log-${category || 'thinking'}`;
            const prefix = category === 'tool' ? '⚡' : category === 'warning' ? '⚠️' : '·';
            entry.textContent = `${prefix} ${description}`;
            logEl.appendChild(entry);

            // Keep only last 6 entries
            while (logEl.children.length > 6) {
                logEl.removeChild(logEl.firstChild);
            }

            // Fade in new entry
            requestAnimationFrame(() => entry.classList.add('visible'));
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
                    this.showNotification('Local history preserved (AI has no memory of it)', 'info');
                    break;

                case 'dismiss':
                    // Same as 'keep' - just dismiss the modal
                    session.sessionId = data.newSessionId;
                    session.ws.setSessionId(data.newSessionId);
                    session.receivedMessageIds.clear();
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
            }
        });
    }
}
