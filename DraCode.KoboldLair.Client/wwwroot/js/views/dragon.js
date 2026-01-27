/**
 * Dragon View Module - Chat Interface for Requirements Gathering
 */
export class DragonView {
    constructor(app) {
        this.app = app;
        this.ws = null;
        this.sessionId = null;
        this.isConnected = false;
        this.messageQueue = [];
        this.currentProject = null;
        this.currentProvider = null;
        this.projects = [];
        this.providers = [];
    }

    async render() {
        const container = document.getElementById('viewContainer');
        container.innerHTML = `
            <section class="dragon-view">
                <!-- Project Setup Panel -->
                <div id="projectSetup" class="project-setup">
                    <h2>🎯 Start Your Project</h2>
                    <p class="setup-description">First, select an existing project or create a new one</p>
                    
                    <div id="emptyStateInfo" class="info-banner" style="display: none;">
                        <span class="info-icon">ℹ️</span>
                        <strong>Starting Fresh</strong>
                        <p>No projects exist yet. Create your first project below.</p>
                    </div>
                    
                    <div class="form-group">
                        <label for="projectSelect">Select Project</label>
                        <select id="projectSelect" class="form-select">
                            <option value="">Loading projects...</option>
                        </select>
                        <button id="newProjectButton" class="btn-secondary mt-2">+ Create New Project</button>
                    </div>

                    <div id="newProjectForm" class="new-project-form" style="display: none;">
                        <label for="projectNameInput">New Project Name</label>
                        <input type="text" id="projectNameInput" placeholder="Enter project name..." class="form-input">
                        <button id="cancelNewProjectButton" class="btn-secondary">Cancel</button>
                    </div>
                    
                    <div class="form-group">
                        <label for="providerSelect">Dragon AI Provider</label>
                        <select id="providerSelect" class="form-select">
                            <option value="">Loading providers...</option>
                        </select>
                    </div>
                    
                    <button id="startChatButton" class="btn btn-primary" disabled>Start Chat</button>
                </div>

                <!-- Chat Container -->
                <div id="chatContainer" class="chat-container" style="display: none;">
                    <div class="chat-header">
                        <strong>Project:</strong> <span id="currentProjectName"></span>
                        <strong style="margin-left: 1.5rem;">Provider:</strong> <span id="currentProviderName"></span>
                        <button id="changeProjectButton" class="btn btn-secondary">Change Project</button>
                    </div>
                    <div id="chatMessages" class="chat-messages"></div>
                    <div class="chat-input-container">
                        <textarea 
                            id="userInput" 
                            placeholder="Tell Dragon about your project..."
                            rows="3"
                            disabled
                        ></textarea>
                        <button id="sendButton" class="btn btn-primary" disabled>Send</button>
                    </div>
                </div>
            </section>
        `;

        // Initialize Dragon functionality
        this.initializeElements();
        this.attachEventListeners();
        await this.loadProjects();
        await this.loadProviders();
    }

    initializeElements() {
        this.messagesContainer = document.getElementById('chatMessages');
        this.userInput = document.getElementById('userInput');
        this.sendButton = document.getElementById('sendButton');
        
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
        if (this.sendButton) {
            this.sendButton.addEventListener('click', () => this.sendMessage());
        }
        
        if (this.userInput) {
            this.userInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    this.sendMessage();
                }
            });
        }

        if (this.startChatButton) {
            this.startChatButton.addEventListener('click', () => this.startChat());
        }
        
        if (this.newProjectButton) {
            this.newProjectButton.addEventListener('click', () => this.showNewProjectForm());
        }
        
        if (this.cancelNewProjectButton) {
            this.cancelNewProjectButton.addEventListener('click', () => this.hideNewProjectForm());
        }
        
        if (this.changeProjectButton) {
            this.changeProjectButton.addEventListener('click', () => this.showProjectSetup());
        }
        
        if (this.projectSelect) {
            this.projectSelect.addEventListener('change', (e) => {
                if (e.target.value === '__new__') {
                    this.showNewProjectForm();
                } else {
                    this.hideNewProjectForm();
                }
                this.updateStartButton();
            });
        }
        
        if (this.providerSelect) {
            this.providerSelect.addEventListener('change', () => this.updateStartButton());
        }
        
        if (this.projectNameInput) {
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
    }

    async loadProjects() {
        try {
            const response = await fetch('/api/projects');
            this.projects = await response.json();
            this.populateProjectSelect();
            this.updateEmptyStateInfo();
        } catch (error) {
            console.error('Failed to load projects:', error);
            if (this.projectSelect) {
                this.projectSelect.innerHTML = '<option value="">Error loading projects</option>';
            }
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
            if (this.providerSelect) {
                this.providerSelect.innerHTML = '<option value="">Error loading providers</option>';
            }
        }
    }

    populateProjectSelect() {
        if (!this.projectSelect) return;
        
        const options = ['<option value="">-- Select a project --</option>'];

        if (this.projects.length > 0) {
            this.projects.forEach(project => {
                options.push(`<option value="${project.id}">${this.escapeHtml(project.name)}</option>`);
            });
        } else {
            options.push('<option value="" disabled style="color: #94a3b8;">No projects yet - create your first one below!</option>');
        }

        options.push('<option value="__new__">+ Create New Project</option>');
        this.projectSelect.innerHTML = options.join('');
    }

    updateEmptyStateInfo() {
        const infoElement = document.getElementById('emptyStateInfo');
        if (infoElement) {
            infoElement.style.display = this.projects.length === 0 ? 'flex' : 'none';
        }
    }

    populateProviderSelect() {
        if (!this.providerSelect) return;
        
        // Providers already filtered by Dragon agent type from API
        const dragonProviders = this.providers.filter(p =>
            p.isEnabled && p.isConfigured
        );

        if (dragonProviders.length === 0) {
            this.providerSelect.innerHTML = '<option value="">No providers available</option>';
            return;
        }

        const options = dragonProviders.map(provider =>
            `<option value="${provider.name}">${this.escapeHtml(provider.displayName)} (${this.escapeHtml(provider.defaultModel)})</option>`
        );

        this.providerSelect.innerHTML = options.join('');
        this.updateStartButton();
    }

    showNewProjectForm() {
        if (this.projectSelect) this.projectSelect.value = '__new__';
        if (this.newProjectForm) this.newProjectForm.style.display = 'block';
        if (this.projectNameInput) this.projectNameInput.focus();
        this.updateStartButton();
    }

    hideNewProjectForm() {
        if (this.newProjectForm) this.newProjectForm.style.display = 'none';
        if (this.projectNameInput) this.projectNameInput.value = '';
        if (this.projectSelect && this.projectSelect.value === '__new__') {
            this.projectSelect.value = '';
        }
        this.updateStartButton();
    }

    createNewProject() {
        const projectName = this.projectNameInput?.value.trim();
        if (!projectName) return;
        
        this.newProjectName = projectName;
        this.updateStartButton();
    }

    updateStartButton() {
        if (!this.startChatButton) return;
        
        const projectValue = this.projectSelect?.value;
        const providerValue = this.providerSelect?.value;

        const hasProject = (projectValue && projectValue !== '__new__') ||
                          (projectValue === '__new__' && this.projectNameInput?.value.trim());
        const hasProvider = providerValue;

        this.startChatButton.disabled = !hasProject || !hasProvider;
    }

    startChat() {
        const projectValue = this.projectSelect?.value;
        const providerValue = this.providerSelect?.value;

        if (!providerValue) return;

        if (projectValue === '__new__') {
            const projectName = this.projectNameInput?.value.trim();
            if (!projectName) return;
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

        if (this.currentProjectName) {
            this.currentProjectName.textContent = this.currentProject?.name || 'Unknown';
        }
        if (this.currentProviderName) {
            this.currentProviderName.textContent = provider?.displayName || 'Unknown';
        }

        if (this.projectSetup) this.projectSetup.style.display = 'none';
        if (this.chatContainer) this.chatContainer.style.display = 'flex';

        this.connect();
    }

    showProjectSetup() {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.close();
        }

        if (this.messagesContainer) this.messagesContainer.innerHTML = '';
        if (this.userInput) this.userInput.value = '';

        if (this.chatContainer) this.chatContainer.style.display = 'none';
        if (this.projectSetup) this.projectSetup.style.display = 'flex';

        this.loadProjects();
    }

    connect() {
        const wsUrl = CONFIG.serverUrl + CONFIG.endpoints.dragon;
        const url = CONFIG.authToken
            ? `${wsUrl}?token=${encodeURIComponent(CONFIG.authToken)}`
            : wsUrl;

        console.log('Connecting to Dragon WebSocket:', url);
        this.ws = new WebSocket(url);

        this.ws.onopen = () => {
            this.isConnected = true;
            if (this.sendButton) this.sendButton.disabled = false;
            if (this.userInput) {
                this.userInput.disabled = false;
                this.userInput.focus();
            }

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
            console.error('Dragon WebSocket error:', error);
            this.addSystemMessage('Connection error occurred');
        };

        this.ws.onclose = () => {
            this.isConnected = false;
            if (this.sendButton) this.sendButton.disabled = true;
            if (this.userInput) this.userInput.disabled = true;
        };
    }

    sendMessage() {
        const message = this.userInput?.value.trim();
        if (!message) return;

        this.addUserMessage(message);
        if (this.userInput) this.userInput.value = '';

        const payload = {
            type: 'message',
            content: message,
            project: this.currentProject,
            provider: this.currentProvider
        };

        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify(payload));
        } else {
            this.messageQueue.push(payload);
        }
    }

    handleMessage(data) {
        if (data.type === 'message') {
            this.addAssistantMessage(data.content);
        } else if (data.type === 'error') {
            this.addSystemMessage(`Error: ${data.content}`);
        }
    }

    addUserMessage(content) {
        this.addMessage('user', content);
    }

    addAssistantMessage(content) {
        this.addMessage('assistant', content);
    }

    addSystemMessage(content) {
        this.addMessage('system', content);
    }

    addMessage(role, content) {
        if (!this.messagesContainer) return;
        
        const messageDiv = document.createElement('div');
        messageDiv.className = `chat-message ${role}`;
        
        const icon = role === 'user' ? '👤' : role === 'assistant' ? '🐉' : 'ℹ️';
        messageDiv.innerHTML = `
            <span class="message-icon">${icon}</span>
            <span class="message-content">${this.escapeHtml(content)}</span>
        `;
        
        this.messagesContainer.appendChild(messageDiv);
        this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    cleanup() {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.close();
        }
    }
}

