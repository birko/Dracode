import { WebSocketClient } from './websocket.js';

export class DragonView {
    constructor() {
        this.ws = null;
        this.messages = [];
    }

    async render() {
        return `
            <div class="chat-container">
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
            this.addMessage('assistant', data.content || JSON.stringify(data));
        });

        const sendBtn = document.getElementById('dragonSendBtn');
        const input = document.getElementById('dragonInput');

        sendBtn?.addEventListener('click', () => this.sendMessage());
        input?.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
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
            this.ws.send({ type: 'message', content: message });
            input.value = '';
        }
    }

    addMessage(role, content) {
        this.messages.push({ role, content });
        const messagesContainer = document.getElementById('dragonMessages');
        if (messagesContainer) {
            const messageEl = document.createElement('div');
            messageEl.className = `chat-message ${role}`;
            messageEl.innerHTML = `
                <div class="chat-message-icon">${role === 'user' ? '👤' : '🐉'}</div>
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
