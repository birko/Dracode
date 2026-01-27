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
    }

    connect() {
        if (this.ws?.readyState === WebSocket.OPEN) {
            return;
        }

        // Construct URL from current config
        const wsUrl = CONFIG.serverUrl || CONFIG.wsUrl;
        const token = CONFIG.authToken;
        this.url = token ? `${wsUrl}${this.endpoint}?token=${token}` : `${wsUrl}${this.endpoint}`;
        
        console.log('Connecting to:', this.url);

        this.setStatus('connecting');
        this.ws = new WebSocket(this.url);

        this.ws.onopen = () => {
            console.log('WebSocket connected');
            this.reconnectAttempts = 0;
            this.setStatus('connected');
        };

        this.ws.onclose = () => {
            console.log('WebSocket closed');
            this.setStatus('disconnected');
            this.attemptReconnect();
        };

        this.ws.onerror = (error) => {
            console.error('WebSocket error:', error);
            this.setStatus('error');
        };

        this.ws.onmessage = (event) => {
            try {
                const message = JSON.parse(event.data);
                this.handleMessage(message);
            } catch (error) {
                console.error('Failed to parse message:', error);
            }
        };
    }

    disconnect() {
        this.reconnectAttempts = this.maxReconnectAttempts;
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }
        this.setStatus('disconnected');
    }

    send(message) {
        if (this.ws?.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify(message));
        } else {
            console.error('WebSocket is not connected');
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
    }

    handleMessage(message) {
        const handlers = this.messageHandlers.get(message.type);
        if (handlers) {
            handlers.forEach(handler => handler(message));
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
