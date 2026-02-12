import CONFIG from './config.js';

export class WebSocketClient {
    constructor(endpoint) {
        this.endpoint = endpoint;
        this.ws = null;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.reconnectDelay = 3000;
        this.messageHandlers = new Map();
        this.statusChangeHandlers = [];
        this.currentStatus = 'disconnected';
        this.connectPromise = null;
        this.pingInterval = null;
        this.pongTimeout = null;
        this.missedPongs = 0;
        this.maxMissedPongs = 3;
        this.pingSequence = 0;         // Tracks which ping we sent
        this.lastPongSequence = 0;     // Tracks which ping was acknowledged
        this.sessionId = null;
        this.receivedMessageIds = new Set();
        this.onSessionNotFound = null;
    }

    /**
     * Connect to the WebSocket server
     * @param {string|null} sessionId - Optional session ID to resume an existing session
     */
    connect(sessionId = null) {
        if (this.ws?.readyState === WebSocket.OPEN) {
            return Promise.resolve();
        }

        if (this.connectPromise) {
            return this.connectPromise;
        }

        // Store sessionId for reconnection attempts
        if (sessionId) {
            this.sessionId = sessionId;
        }

        this.connectPromise = new Promise((resolve, reject) => {
            // Connect directly to backend server using serverUrl from CONFIG
            const serverUrl = CONFIG.serverUrl;
            const token = CONFIG.authToken;

            // Normalize endpoint - remove leading slash
            const endpoint = this.endpoint.replace(/^\/+/, '');

            // Build full URL: serverUrl + endpoint
            const baseUrl = serverUrl.replace(/\/+$/, ''); // Remove trailing slashes
            const fullUrl = `${baseUrl}/${endpoint}`;

            // Build query string with token and sessionId
            const params = new URLSearchParams();
            if (token) params.set('token', token);
            if (this.sessionId) params.set('sessionId', this.sessionId);

            const queryString = params.toString();
            this.url = queryString ? `${fullUrl}?${queryString}` : fullUrl;

            console.log('Connecting to backend:', this.url);

            this.setStatus('connecting');
            this.ws = new WebSocket(this.url);

            const timeout = setTimeout(() => {
                reject(new Error('Connection timeout'));
                this.connectPromise = null;
            }, 10000);

            this.ws.onopen = () => {
                clearTimeout(timeout);
                console.log('WebSocket connected');
                this.reconnectAttempts = 0;
                this.setStatus('connected');
                this.connectPromise = null;
                this.startHeartbeat();
                resolve();
            };

            this.ws.onclose = () => {
                clearTimeout(timeout);
                console.log('WebSocket closed');
                this.stopHeartbeat();
                this.setStatus('disconnected');
                this.connectPromise = null;
                this.attemptReconnect();
            };

            this.ws.onerror = (error) => {
                clearTimeout(timeout);
                console.error('WebSocket error:', error);
                this.stopHeartbeat();
                this.setStatus('error');
                this.connectPromise = null;
                reject(error);
            };

            this.ws.onmessage = (event) => {
                try {
                    const message = JSON.parse(event.data);
                    console.log('[WebSocket] Received raw message:', message.type, message.messageId || '(no id)');

                    // Handle pong responses
                    if (message.type === 'pong') {
                        // Mark that we received a pong for the current ping sequence
                        this.lastPongSequence = this.pingSequence;
                        this.missedPongs = 0;
                        clearTimeout(this.pongTimeout);
                        return;
                    }

                    // Handle session not found - client's session was not on server
                    if (message.type === 'session_not_found') {
                        console.log('Session not found on server:', message);
                        if (this.onSessionNotFound) {
                            this.onSessionNotFound(message);
                        }
                        return;
                    }

                    // Track sessionId from server messages
                    if (message.sessionId && !this.sessionId) {
                        this.sessionId = message.sessionId;
                        console.log('Session ID received:', this.sessionId);
                    }

                    // Deduplicate messages using messageId
                    if (message.messageId) {
                        if (this.receivedMessageIds.has(message.messageId)) {
                            console.log('[WebSocket] Skipping duplicate message:', message.messageId);
                            return;
                        }
                        this.receivedMessageIds.add(message.messageId);

                        // Limit set size to prevent memory bloat
                        if (this.receivedMessageIds.size > 1000) {
                            const entries = Array.from(this.receivedMessageIds);
                            this.receivedMessageIds = new Set(entries.slice(-500));
                        }
                    }

                    console.log('[WebSocket] Dispatching to handlers:', message.type);
                    this.handleMessage(message);
                } catch (error) {
                    console.error('[WebSocket] Failed to parse message:', error, event.data?.substring?.(0, 200));
                }
            };
        });

        return this.connectPromise;
    }

    /**
     * Get the current session ID
     */
    getSessionId() {
        return this.sessionId;
    }

    /**
     * Set the session ID (for resuming sessions)
     */
    setSessionId(sessionId) {
        this.sessionId = sessionId;
    }

    /**
     * Clear received message IDs (for when session is reset)
     */
    clearMessageHistory() {
        this.receivedMessageIds.clear();
    }

    disconnect() {
        this.reconnectAttempts = this.maxReconnectAttempts;
        this.stopHeartbeat();
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }
        this.connectPromise = null;
        this.setStatus('disconnected');
    }

    startHeartbeat() {
        this.stopHeartbeat();
        this.missedPongs = 0;
        this.pingSequence = 0;
        this.lastPongSequence = 0;

        // Send ping every 20 seconds
        this.pingInterval = setInterval(() => {
            if (this.ws?.readyState === WebSocket.OPEN) {
                try {
                    // Increment sequence before sending ping
                    this.pingSequence++;
                    const expectedSequence = this.pingSequence;

                    this.ws.send(JSON.stringify({ action: 'ping' }));

                    // Set timeout for pong response (5 seconds)
                    this.pongTimeout = setTimeout(() => {
                        // Check if pong was received for this specific ping
                        // This handles the race condition where pong arrives just as timeout fires
                        if (this.lastPongSequence >= expectedSequence) {
                            // Pong was received, timeout callback is stale - ignore
                            return;
                        }

                        this.missedPongs++;
                        console.warn(`Missed pong response (${this.missedPongs}/${this.maxMissedPongs})`);

                        if (this.missedPongs >= this.maxMissedPongs) {
                            console.error('Connection appears dead, reconnecting...');
                            this.ws?.close();
                        }
                    }, 5000);
                } catch (error) {
                    console.error('Failed to send ping:', error);
                }
            }
        }, 20000);
    }

    stopHeartbeat() {
        if (this.pingInterval) {
            clearInterval(this.pingInterval);
            this.pingInterval = null;
        }
        if (this.pongTimeout) {
            clearTimeout(this.pongTimeout);
            this.pongTimeout = null;
        }
        this.missedPongs = 0;
        this.pingSequence = 0;
        this.lastPongSequence = 0;
    }

    async send(message) {
        // Wait for connection if not connected
        if (this.ws?.readyState !== WebSocket.OPEN) {
            console.log('WebSocket not open, waiting for connection...');
            await this.connect();
        }

        if (this.ws?.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify(message));
        } else {
            throw new Error('WebSocket is not connected');
        }
    }

    /**
     * Send a session replay request to restore conversation context on the server
     * @param {Array} messages - Array of {role, content} objects to replay
     */
    sendReplayRequest(messages) {
        this.send({
            type: 'session_replay',
            sessionId: this.sessionId,
            messages: messages
        });
    }

    on(type, handler) {
        if (!this.messageHandlers.has(type)) {
            this.messageHandlers.set(type, []);
        }
        this.messageHandlers.get(type).push(handler);
    }

    off(type, handler) {
        const handlers = this.messageHandlers.get(type);
        if (handlers) {
            const index = handlers.indexOf(handler);
            if (index > -1) {
                handlers.splice(index, 1);
            }
        }
    }

    onStatusChange(handler) {
        this.statusChangeHandlers.push(handler);
        handler(this.currentStatus);
    }

    getStatus() {
        return this.currentStatus;
    }

    setStatus(status) {
        this.currentStatus = status;
        this.statusChangeHandlers.forEach(handler => handler(status));
        
        // Also emit as message for backward compatibility
        const handlers = this.messageHandlers.get(status);
        if (handlers) {
            handlers.forEach(handler => handler());
        }
    }

    handleMessage(message) {
        const handlers = this.messageHandlers.get(message.type);
        const handlerCount = handlers?.length || 0;
        console.log(`[WebSocket] handleMessage: type=${message.type}, specific handlers=${handlerCount}`);
        if (handlers) {
            handlers.forEach(handler => handler(message));
        }

        // Also handle generic 'message' handlers
        const allHandlers = this.messageHandlers.get('message');
        const allHandlerCount = allHandlers?.length || 0;
        console.log(`[WebSocket] handleMessage: generic 'message' handlers=${allHandlerCount}`);
        if (allHandlers) {
            allHandlers.forEach(handler => handler(message));
        }
    }

    attemptReconnect() {
        if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            console.log('Max reconnection attempts reached');
            return;
        }

        this.reconnectAttempts++;
        console.log(`Reconnecting... (${this.reconnectAttempts}/${this.maxReconnectAttempts})`);

        setTimeout(() => {
            this.connect();
        }, this.reconnectDelay * this.reconnectAttempts);
    }
}
