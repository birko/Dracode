// Dragon WebSocket client for requirements gathering

class DragonClient {
    constructor() {
        this.ws = null;
        this.sessionId = null;
        this.isConnected = false;
        this.messageQueue = [];
        this.currentProject = null;
        this.currentProvider = null;
        this.projects = [];
        this.providers = [];
        this.config = null; // Will be loaded dynamically
        
        this.initializeElements();
        this.attachEventListeners();
        this.loadConfiguration().then(() => {
            this.loadProjects();
            this.loadProviders();
        });
    }

    initializeElements() {
        this.statusElement = document.getElementById('connectionStatus');
        this.messagesContainer = document.getElementById('chatMessages');
        this.userInput = document.getElementById('userInput');
        this.sendButton = document.getElementById('sendButton');
        this.specificationsList = document.getElementById('specificationsList');
        
        // Project setup elements
        this.projectSetup = document.getElementById('projectSetup');
        this.chatContainer = document.getElementById('chatContainer');
        this.projectSelect = document.getElementById('projectSelect');
        this.providerSelect = document.getElementById('providerSelect');
        this.startChatButton = document.getElementById('startChatButton');
        this.newProjectButton = document.getElementById('newProjectButton');
        this.newProjectForm = document.getElementById('newProjectForm');
        this.projectNameInput = document.getElementById('projectNameInput');
        this.cancelNewProjectButton = document.getElementById('cancelNewProjectButton');
        this.changeProjectButton = document.getElementById('changeProjectButton');
        this.currentProjectName = document.getElementById('currentProjectName');
        this.currentProviderName = document.getElementById('currentProviderName');
    }

    attachEventListeners() {
        this.sendButton.addEventListener('click', () => this.sendMessage());
        
        this.userInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
        });

        // Project setup listeners
        this.startChatButton.addEventListener('click', () => this.startChat());
        this.newProjectButton.addEventListener('click', () => this.showNewProjectForm());
        this.cancelNewProjectButton.addEventListener('click', () => this.hideNewProjectForm());
        this.changeProjectButton.addEventListener('click', () => this.showProjectSetup());
        
        this.projectSelect.addEventListener('change', (e) => {
            if (e.target.value === '__new__') {
                this.showNewProjectForm();
            } else {
                this.hideNewProjectForm();
            }
            this.updateStartButton();
        });
        this.providerSelect.addEventListener('change', () => this.updateStartButton());
        
        this.projectNameInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                this.createNewProject();
            }
        });
        
        this.projectNameInput.addEventListener('input', () => {
            this.updateStartButton();
        });
    }

    async loadConfiguration() {
        try {
            const response = await fetch('/api/config');
            this.config = await response.json();
            console.log('Configuration loaded:', this.config);
        } catch (error) {
            console.error('Failed to load configuration:', error);
            // Fallback to CONFIG from config.js
            this.config = CONFIG;
        }
    }

    async loadProjects() {
        try {
            const response = await fetch('/api/projects');
            this.projects = await response.json();
            this.populateProjectSelect();
            this.updateEmptyStateInfo();
        } catch (error) {
            console.error('Failed to load projects:', error);
            this.projectSelect.innerHTML = '<option value="">Error loading projects</option>';
        }
    }

    async loadProviders() {
        try {
            const response = await fetch('/api/providers');
            const data = await response.json();
            this.providers = data.providers || [];
            this.populateProviderSelect();
        } catch (error) {
            console.error('Failed to load providers:', error);
            this.providerSelect.innerHTML = '<option value="">Error loading providers</option>';
        }
    }

    populateProjectSelect() {
        const options = ['<option value="">-- Select a project --</option>'];
        
        if (this.projects.length > 0) {
            this.projects.forEach(project => {
                options.push(`<option value="${project.id}">${project.name}</option>`);
            });
        } else {
            // Show helpful message when no projects exist
            options.push('<option value="" disabled style="color: #94a3b8;">No projects yet - create your first one below!</option>');
        }
        
        options.push('<option value="__new__">+ Create New Project</option>');
        this.projectSelect.innerHTML = options.join('');
    }

    updateEmptyStateInfo() {
        const infoElement = document.getElementById('emptyStateInfo');
        if (infoElement) {
            if (this.projects.length === 0) {
                infoElement.style.display = 'flex';
            } else {
                infoElement.style.display = 'none';
            }
        }
    }

    populateProviderSelect() {
        const dragonProviders = this.providers.filter(p => 
            p.isEnabled && p.isConfigured && 
            (p.compatibleAgents.includes('dragon') || p.compatibleAgents.includes('all'))
        );
        
        if (dragonProviders.length === 0) {
            this.providerSelect.innerHTML = '<option value="">No providers available</option>';
            return;
        }
        
        const options = dragonProviders.map(provider => 
            `<option value="${provider.name}">${provider.displayName} (${provider.defaultModel})</option>`
        );
        
        this.providerSelect.innerHTML = options.join('');
        this.updateStartButton();
    }

    showNewProjectForm() {
        this.projectSelect.value = '__new__';
        this.newProjectForm.style.display = 'block';
        this.projectNameInput.focus();
        this.updateStartButton();
    }

    hideNewProjectForm() {
        this.newProjectForm.style.display = 'none';
        this.projectNameInput.value = '';
        if (this.projectSelect.value === '__new__') {
            this.projectSelect.value = '';
        }
        this.updateStartButton();
    }

    createNewProject() {
        const projectName = this.projectNameInput.value.trim();
        
        if (!projectName) {
            return;
        }
        
        // Store the new project name for later use
        this.newProjectName = projectName;
        this.updateStartButton();
    }

    updateStartButton() {
        const projectValue = this.projectSelect.value;
        const providerValue = this.providerSelect.value;
        
        // Check if we have a valid project selection or a new project name
        const hasProject = (projectValue && projectValue !== '__new__') || 
                          (projectValue === '__new__' && this.projectNameInput.value.trim());
        const hasProvider = providerValue;
        
        this.startChatButton.disabled = !hasProject || !hasProvider;
    }

    startChat() {
        const projectValue = this.projectSelect.value;
        const providerValue = this.providerSelect.value;
        
        if (!providerValue) {
            return;
        }
        
        // Handle project selection or creation
        if (projectValue === '__new__') {
            const projectName = this.projectNameInput.value.trim();
            if (!projectName) {
                return;
            }
            this.currentProject = {
                id: 'new-' + Date.now(),
                name: projectName,
                isNew: true
            };
        } else if (!projectValue) {
            return;
        } else {
            this.currentProject = this.projects.find(p => p.id === projectValue);
        }
        
        const provider = this.providers.find(p => p.name === providerValue);
        this.currentProvider = provider;
        
        // Update UI
        this.currentProjectName.textContent = this.currentProject?.name || 'Unknown';
        this.currentProviderName.textContent = provider?.displayName || 'Unknown';
        
        // Hide setup, show chat
        this.projectSetup.style.display = 'none';
        this.chatContainer.style.display = 'flex';
        
        // Connect to WebSocket
        this.connect();
    }

    showProjectSetup() {
        // Disconnect if connected
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.close();
        }
        
        // Clear chat
        this.messagesContainer.innerHTML = '';
        this.userInput.value = '';
        
        // Show setup, hide chat
        this.chatContainer.style.display = 'none';
        this.projectSetup.style.display = 'flex';
        
        // Reload projects
        this.loadProjects();
    }

    connect() {
        const config = this.config || CONFIG; // Use loaded config or fallback
        const wsUrl = config.serverUrl + config.endpoints.dragon;
        const url = config.authToken 
            ? `${wsUrl}?token=${encodeURIComponent(config.authToken)}`
            : wsUrl;
        
        console.log('Connecting to WebSocket:', url);
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
