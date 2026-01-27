// Dragon WebSocket client for requirements gathering

class DragonClient {
    constructor() {
        this.ws = null;
        this.sessionId = null;
        this.isConnected = false;
        this.messageQueue = [];
        
        this.initializeElements();
        this.attachEventListeners();
        this.connect();
    }

    initializeElements() {
        this.statusElement = document.getElementById('connectionStatus');
        this.messagesContainer = document.getElementById('chatMessages');
        this.userInput = document.getElementById('userInput');
        this.sendButton = document.getElementById('sendButton');
        this.specificationsList = document.getElementById('specificationsList');
    }

    attachEventListeners() {
        this.sendButton.addEventListener('click', () => this.sendMessage());
        
        this.userInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
        });
    }

    connect() {
        const wsUrl = CONFIG.serverUrl + CONFIG.endpoints.dragon;
        const url = CONFIG.authToken 
            ? `${wsUrl}?token=${encodeURIComponent(CONFIG.authToken)}`
            : wsUrl;
        
        this.ws = new WebSocket(url);
        
        this.ws.onopen = () => {
            this.isConnected = true;
            this.updateStatus('connected');
            this.sendButton.disabled = false;
            this.userInput.disabled = false;
            this.userInput.focus();
            
            // Process queued messages
            while (this.messageQueue.length > 0) {
                const msg = this.messageQueue.shift();
                this.ws.send(JSON.stringify(msg));
            }
        };
        
        this.ws.onmessage = (event) => {
            const data = JSON.parse(event.data);
            this.handleMessage(data);
        };
        
        this.ws.onerror = (error) => {
            console.error('WebSocket error:', error);
            this.addSystemMessage('Connection error occurred');
        };
        
        this.ws.onclose = () => {
            this.isConnected = false;
            this.updateStatus('disconnected');
            this.sendButton.disabled = true;
            
            // Attempt reconnect after 3 seconds
            setTimeout(() => this.connect(), 3000);
        };
    }

    updateStatus(status) {
        if (status === 'connected') {
            this.statusElement.textContent = 'Connected';
            this.statusElement.className = 'status-connected';
        } else {
            this.statusElement.textContent = 'Disconnected';
            this.statusElement.className = 'status-disconnected';
        }
    }

    handleMessage(data) {
        switch (data.type) {
            case 'dragon_message':
                this.sessionId = data.sessionId;
                this.removeTypingIndicator();
                this.addDragonMessage(data.message);
                break;
                
            case 'dragon_typing':
                this.addTypingIndicator();
                break;
                
            case 'specification_created':
                this.removeTypingIndicator();
                this.addSpecification(data.filename, data.path);
                this.addSystemMessage(`✅ Specification created: ${data.filename}`);
                break;
                
            case 'error':
                this.removeTypingIndicator();
                this.addSystemMessage(`❌ Error: ${data.message}`);
                break;
                
            default:
                console.log('Unknown message type:', data.type);
        }
    }

    sendMessage() {
        const message = this.userInput.value.trim();
        
        if (!message) {
            return;
        }
        
        // Add user message to chat
        this.addUserMessage(message);
        
        // Clear input
        this.userInput.value = '';
        
        // Send to server
        const payload = {
            message: message,
            sessionId: this.sessionId
        };
        
        if (this.isConnected) {
            this.ws.send(JSON.stringify(payload));
        } else {
            this.messageQueue.push(payload);
            this.addSystemMessage('Message queued, reconnecting...');
        }
    }

    addDragonMessage(content) {
        const messageDiv = document.createElement('div');
        messageDiv.className = 'message message-dragon';
        
        messageDiv.innerHTML = `
            <div class="avatar">🐉</div>
            <div class="content">${this.formatMessage(content)}</div>
        `;
        
        this.messagesContainer.appendChild(messageDiv);
        this.scrollToBottom();
    }

    addUserMessage(content) {
        const messageDiv = document.createElement('div');
        messageDiv.className = 'message message-user';
        
        messageDiv.innerHTML = `
            <div class="avatar">👤</div>
            <div class="content">${this.escapeHtml(content)}</div>
        `;
        
        this.messagesContainer.appendChild(messageDiv);
        this.scrollToBottom();
    }

    addSystemMessage(content) {
        const messageDiv = document.createElement('div');
        messageDiv.className = 'message message-system';
        messageDiv.textContent = content;
        
        this.messagesContainer.appendChild(messageDiv);
        this.scrollToBottom();
    }

    addTypingIndicator() {
        // Remove existing indicator if present
        this.removeTypingIndicator();
        
        const typingDiv = document.createElement('div');
        typingDiv.className = 'message message-typing';
        typingDiv.id = 'typingIndicator';
        
        typingDiv.innerHTML = `
            <div class="avatar">🐉</div>
            <div class="typing-indicator">
                <span></span>
                <span></span>
                <span></span>
            </div>
        `;
        
        this.messagesContainer.appendChild(typingDiv);
        this.scrollToBottom();
    }

    removeTypingIndicator() {
        const indicator = document.getElementById('typingIndicator');
        if (indicator) {
            indicator.remove();
        }
    }

    addSpecification(filename, path) {
        const specDiv = document.createElement('div');
        specDiv.className = 'spec-item';
        
        const now = new Date();
        const timeString = now.toLocaleTimeString();
        
        specDiv.innerHTML = `
            <div class="icon">📄</div>
            <div class="info">
                <div class="name">${this.escapeHtml(filename)}</div>
                <div class="time">${timeString}</div>
            </div>
        `;
        
        this.specificationsList.appendChild(specDiv);
    }

    formatMessage(content) {
        // Basic markdown-like formatting
        let formatted = this.escapeHtml(content);
        
        // Bold
        formatted = formatted.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
        
        // Code blocks
        formatted = formatted.replace(/`(.+?)`/g, '<code>$1</code>');
        
        // Line breaks
        formatted = formatted.replace(/\n/g, '<br>');
        
        return formatted;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    scrollToBottom() {
        this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    new DragonClient();
});
