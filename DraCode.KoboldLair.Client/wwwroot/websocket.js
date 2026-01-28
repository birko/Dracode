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
    }

    connect() {
        if (this.ws?.readyState === WebSocket.OPEN) {
            return Promise.resolve();
        }

        if (this.connectPromise) {
            return this.connectPromise;
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
            
            this.url = token ? `${fullUrl}?token=${token}` : fullUrl;
            
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
                    
                    // Handle pong responses
                    if (message.type === 'pong') {
                        this.missedPongs = 0;
                        clearTimeout(this.pongTimeout);
                        return;
                    }
                    
                    this.handleMessage(message);
                } catch (error) {
                    console.error('Failed to parse message:', error);
                }
            };
        });

        return this.connectPromise;
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
        
        // Send ping every 20 seconds
        this.pingInterval = setInterval(() => {
            if (this.ws?.readyState === WebSocket.OPEN) {
                try {
                    this.ws.send(JSON.stringify({ action: 'ping' }));
                    
                    // Set timeout for pong response (5 seconds)
                    this.pongTimeout = setTimeout(() => {
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
        if (handlers) {
            handlers.forEach(handler => handler(message));
        }
        
        // Also handle generic 'message' handlers
        const allHandlers = this.messageHandlers.get('message');
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
