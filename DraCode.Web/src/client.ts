import type { Agent, AgentConfig, LogLevel, Provider, WebSocketMessage, WebSocketResponse } from './types.js';

/**
 * Main application class managing WebSocket connection and multi-agent interface
 */
export class DraCodeClient {
    private ws: WebSocket | null = null;
    private agents: Map<string, Agent> = new Map();
    private activeAgentId: string | null = null;
    private availableProviders: Provider[] = [];
    private providerFilter: 'configured' | 'all' | 'notConfigured' = 'configured';

    // DOM element references
    private readonly elements: {
        status: HTMLElement;
        connectBtn: HTMLButtonElement;
        disconnectBtn: HTMLButtonElement;
        listBtn: HTMLButtonElement;
        providersSection: HTMLElement;
        providersGrid: HTMLElement;
        wsUrl: HTMLInputElement;
        tabsContainer: HTMLElement;
        tabContents: HTMLElement;
        agentTabs: HTMLElement;
        emptyState: HTMLElement;
        manualConfig: HTMLElement;
        connectionModal: HTMLElement;
        connectionTabName: HTMLInputElement;
        connectionWorkingDir: HTMLInputElement;
        connectionModalConnect: HTMLButtonElement;
        connectionModalCancel: HTMLButtonElement;
        generalModal: HTMLElement;
        generalModalTitle: HTMLElement;
        generalModalMessage: HTMLElement;
        generalModalInput: HTMLInputElement;
        generalModalInputContainer: HTMLElement;
        generalModalInputLabel: HTMLElement;
        generalModalConfirm: HTMLButtonElement;
        generalModalCancel: HTMLButtonElement;
    };

    constructor() {
        this.elements = {
            status: this.getElement('status'),
            connectBtn: this.getElement('connectBtn') as HTMLButtonElement,
            disconnectBtn: this.getElement('disconnectBtn') as HTMLButtonElement,
            listBtn: this.getElement('listBtn') as HTMLButtonElement,
            providersSection: this.getElement('providersSection'),
            providersGrid: this.getElement('providersGrid'),
            wsUrl: this.getElement('wsUrl') as HTMLInputElement,
            tabsContainer: this.getElement('tabsContainer'),
            tabContents: this.getElement('tabContents'),
            agentTabs: this.getElement('agentTabs'),
            emptyState: this.getElement('emptyState'),
            manualConfig: this.getElement('manualConfig'),
            connectionModal: this.getElement('connectionModal'),
            connectionTabName: this.getElement('connectionTabName') as HTMLInputElement,
            connectionWorkingDir: this.getElement('connectionWorkingDir') as HTMLInputElement,
            connectionModalConnect: this.getElement('connectionModalConnect') as HTMLButtonElement,
            connectionModalCancel: this.getElement('connectionModalCancel') as HTMLButtonElement,
            generalModal: this.getElement('generalModal'),
            generalModalTitle: this.getElement('generalModalTitle'),
            generalModalMessage: this.getElement('generalModalMessage'),
            generalModalInput: this.getElement('generalModalInput') as HTMLInputElement,
            generalModalInputContainer: this.getElement('generalModalInputContainer'),
            generalModalInputLabel: this.getElement('generalModalInputLabel'),
            generalModalConfirm: this.getElement('generalModalConfirm') as HTMLButtonElement,
            generalModalCancel: this.getElement('generalModalCancel') as HTMLButtonElement
        };

        this.setupEventListeners();
    }

    private getElement(id: string): HTMLElement {
        const element = document.getElementById(id);
        if (!element) {
            throw new Error(`Element with id '${id}' not found`);
        }
        return element;
    }

    private setupEventListeners(): void {
        this.elements.connectBtn.addEventListener('click', () => this.connectToServer());
        this.elements.disconnectBtn.addEventListener('click', () => this.disconnectFromServer());
        this.elements.listBtn.addEventListener('click', () => this.listProviders());

        // Provider filter listeners
        const filterConfigured = document.getElementById('filterConfigured') as HTMLInputElement;
        const filterAll = document.getElementById('filterAll') as HTMLInputElement;
        const filterNotConfigured = document.getElementById('filterNotConfigured') as HTMLInputElement;

        if (filterConfigured) {
            filterConfigured.addEventListener('change', () => {
                this.providerFilter = 'configured';
                this.displayProviders(this.availableProviders);
            });
        }

        if (filterAll) {
            filterAll.addEventListener('change', () => {
                this.providerFilter = 'all';
                this.displayProviders(this.availableProviders);
            });
        }

        if (filterNotConfigured) {
            filterNotConfigured.addEventListener('change', () => {
                this.providerFilter = 'notConfigured';
                this.displayProviders(this.availableProviders);
            });
        }
    }

    /**
     * Connect to WebSocket server
     */
    public connectToServer(): void {
        const url = this.elements.wsUrl.value;

        try {
            this.ws = new WebSocket(url);

            this.ws.onopen = () => {
                this.logToConsole('✅ Connected to WebSocket server', 'success');
                this.updateServerStatus(true);
            };

            this.ws.onmessage = (event) => this.handleServerMessage(event);

            this.ws.onerror = () => {
                this.logToConsole('❌ WebSocket error', 'error');
            };

            this.ws.onclose = () => {
                this.logToConsole('Connection closed', 'error');
                this.updateServerStatus(false);
            };
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Unknown error';
            this.logToConsole(`❌ Failed to connect: ${message}`, 'error');
        }
    }

    /**
     * Disconnect from WebSocket server
     */
    public disconnectFromServer(): void {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            // Disconnect all agents first
            this.agents.forEach((_, agentId) => {
                this.sendToAgent(agentId, { command: 'disconnect', agentId });
            });

            this.ws.close();
            this.ws = null;
            this.agents.clear();
            this.updateAgentTabs();
        }
    }

    /**
     * Request list of available providers from server
     */
    public listProviders(): void {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            console.warn('⚠️ Cannot list providers - WebSocket not open. State:', this.ws?.readyState);
            return;
        }

        const message = { command: 'list' };
        console.log('📤 Sending list command:', message);
        this.ws.send(JSON.stringify(message));
        this.logToConsole('📋 Requesting provider list...');
    }

    /**
     * Update server connection status UI
     */
    private updateServerStatus(connected: boolean): void {
        console.log('🔌 Server connection status:', connected);
        
        if (connected) {
            this.elements.status.className = 'status connected';
            this.elements.status.textContent = '🟢 Status: Connected to Server';
            this.elements.connectBtn.disabled = true;
            this.elements.disconnectBtn.disabled = false;
            this.elements.listBtn.disabled = false;
            this.elements.providersSection.style.display = 'block';
            
            console.log('✅ Provider section displayed:', this.elements.providersSection.style.display);

            // Auto-list providers on connect
            setTimeout(() => {
                console.log('⏰ Auto-requesting provider list...');
                this.listProviders();
            }, 500);
        } else {
            this.elements.status.className = 'status disconnected';
            this.elements.status.textContent = '⚫ Status: Disconnected';
            this.elements.connectBtn.disabled = false;
            this.elements.disconnectBtn.disabled = true;
            this.elements.listBtn.disabled = true;
            this.elements.providersSection.style.display = 'none';
        }
    }

    /**
     * Handle incoming WebSocket messages
     */
    private handleServerMessage(event: MessageEvent): void {
        console.log('=== RAW MESSAGE RECEIVED ===');
        console.log('Type:', typeof event.data);
        console.log('Content:', event.data);
        console.log('First 200 chars:', event.data.substring(0, 200));
        
        try {
            const response: WebSocketResponse = JSON.parse(event.data);
            console.log('📨 Parsed response:', response);
            console.log('   Status:', response.Status);
            console.log('   Message:', response.Message);
            console.log('   MessageType:', response.MessageType);
            console.log('   Data type:', typeof response.Data);
            console.log('   Data value:', response.Data);

            // Handle streaming messages
            if (response.Status === 'stream') {
                this.handleStreamMessage(response);
                return;
            }

            // Handle interactive prompts
            if (response.Status === 'prompt') {
                this.handlePromptMessage(response);
                return;
            }

            // Check if this is a provider list response
            // Provider list has Status=success, Message with "provider", and Data array
            if (response.Status === 'success' && 
                response.Message && 
                response.Data && 
                (response.Message.toLowerCase().includes('provider') || 
                 response.Message.toLowerCase().includes('configured'))) {
                console.log('✅ Detected as provider list response');
                console.log('   Calling handleProviderList...');
                this.handleProviderList(response);
                return;
            }

            console.log('❌ Not detected as provider list. Routing to console/agent log.');

            // Route to specific agent log
            const agentId = response.AgentId || this.activeAgentId;
            if (agentId && this.agents.has(agentId)) {
                this.logToAgent(
                    agentId,
                    this.formatResponse(response),
                    response.Status === 'error' ? 'error' : 'success'
                );
                
                // Refresh provider grid when agent status changes (connected/disconnected/reset)
                if (response.Status === 'success' || 
                    response.Status === 'error' || 
                    response.Status === 'connected' || 
                    response.Status === 'disconnected' || 
                    response.Status === 'reset') {
                    if (this.availableProviders.length > 0) {
                        this.displayProviders(this.availableProviders);
                    }
                }
            } else {
                this.logToConsole(
                    this.formatResponse(response),
                    response.Status === 'error' ? 'error' : 'success'
                );
            }
        } catch (error) {
            console.log('❌ Error parsing message:', error);
            console.log('Raw message:', event.data);
            this.logToConsole(`Message: ${event.data}`);
        }
    }

    /**
     * Format WebSocket response for display
     */
    private formatResponse(response: WebSocketResponse): string {
        let msg = `<strong>${(response.Status || 'info').toUpperCase()}</strong>`;
        if (response.Message) msg += `: ${response.Message}`;
        if (response.Data) msg += `<br><pre>${this.escapeHtml(response.Data)}</pre>`;
        if (response.Error) msg += `<br><span class="error">${this.escapeHtml(response.Error)}</span>`;
        return msg;
    }

    /**
     * Escape HTML to prevent XSS
     */
    private escapeHtml(text: string): string {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Handle streaming messages from agent
     */
    private handleStreamMessage(response: WebSocketResponse): void {
        const agentId = response.AgentId;
        if (!agentId || !this.agents.has(agentId)) {
            console.warn('Stream message for unknown agent:', agentId);
            return;
        }

        const messageType = response.MessageType || 'info';
        const content = response.Message || '';

        // Map message types to log levels
        let logLevel: LogLevel = 'info';
        let icon = 'ℹ️';

        switch (messageType) {
            case 'error':
                logLevel = 'error';
                icon = '❌';
                break;
            case 'warning':
                logLevel = 'warning';
                icon = '⚠️';
                break;
            case 'tool_call':
                logLevel = 'info';
                icon = '🔧';
                break;
            case 'tool_result':
                logLevel = 'success';
                icon = '📋';
                break;
            case 'assistant':
                logLevel = 'info';
                icon = '💬';
                break;
            case 'display':
                logLevel = 'info';
                icon = '📄';
                break;
            default:
                logLevel = 'info';
                icon = 'ℹ️';
        }

        this.logToAgent(agentId, `${icon} ${this.escapeHtml(content)}`, logLevel);
    }

    /**
     * Handle interactive prompt from agent
     */
    private async handlePromptMessage(response: WebSocketResponse): Promise<void> {
        const agentId = response.AgentId;
        const promptId = response.PromptId;

        if (!agentId || !promptId || !this.agents.has(agentId)) {
            console.warn('Prompt message for unknown agent:', agentId);
            return;
        }

        const question = response.Message || '';
        const context = response.Data || '';

        // Display prompt in agent log
        const promptHtml = context 
            ? `<div class="prompt-message">
                <div class="prompt-context">💡 Context: ${this.escapeHtml(context)}</div>
                <div class="prompt-question">❓ ${this.escapeHtml(question)}</div>
               </div>`
            : `<div class="prompt-message">
                <div class="prompt-question">❓ ${this.escapeHtml(question)}</div>
               </div>`;

        this.logToAgent(agentId, promptHtml, 'info');

        // Show prompt input
        const promptTitle = context ? 'Agent Question' : 'Agent Question';
        const promptMessage = context ? `${context}\n\n${question}` : question;
        const promptResponse = await this.showPrompt(promptTitle, promptMessage, '');

        if (promptResponse !== null) {
            // Send response back to agent
            if (this.ws && this.ws.readyState === WebSocket.OPEN) {
                const message: WebSocketMessage = {
                    command: 'prompt_response',
                    agentId: agentId,
                    promptId: promptId,
                    data: promptResponse
                };
                this.ws.send(JSON.stringify(message));

                // Log user's response
                this.logToAgent(agentId, `✅ Your answer: ${this.escapeHtml(promptResponse)}`, 'success');
            }
        } else {
            // User cancelled
            if (this.ws && this.ws.readyState === WebSocket.OPEN) {
                const message: WebSocketMessage = {
                    command: 'prompt_response',
                    agentId: agentId,
                    promptId: promptId,
                    data: ''
                };
                this.ws.send(JSON.stringify(message));

                this.logToAgent(agentId, '❌ Prompt cancelled', 'warning');
            }
        }
    }

    /**
     * Handle provider list response
     */
    private handleProviderList(response: WebSocketResponse): void {
        console.log('=== HANDLE PROVIDER LIST ===');
        console.log('response.Data exists?', !!response.Data);
        console.log('response.Data type:', typeof response.Data);
        console.log('response.Data value:', response.Data);
        
        try {
            if (!response.Data) {
                throw new Error('No Data in provider list response');
            }
            
            // response.Data might be a string (JSON) or already an array
            let providers: Provider[];
            
            if (typeof response.Data === 'string') {
                console.log('📝 Data is string, parsing as JSON...');
                providers = JSON.parse(response.Data);
            } else if (Array.isArray(response.Data)) {
                console.log('📝 Data is already an array');
                providers = response.Data;
            } else {
                console.log('❌ Data is unexpected type:', typeof response.Data);
                throw new Error('Provider data is neither string nor array');
            }
            
            console.log('📋 Parsed providers:', providers);
            console.log('📋 Provider count:', providers.length);
            
            this.availableProviders = providers;
            this.displayProviders(this.availableProviders);
            this.logToConsole(`📋 Found ${this.availableProviders.length} providers`, 'success');
        } catch (error) {
            console.error('❌ Error parsing provider list:', error);
            console.error('   Error message:', (error as Error).message);
            console.error('   Stack:', (error as Error).stack);
            this.logToConsole('❌ Failed to parse provider list', 'error');
        }
    }

    /**
     * Display providers in grid
     */
    private displayProviders(providers: Provider[]): void {
        console.log('🎨 Displaying providers:', providers);
        console.log('🔍 Current filter:', this.providerFilter);
        this.elements.providersGrid.innerHTML = '';

        // Filter providers based on selection
        let filteredProviders: Provider[];
        
        switch (this.providerFilter) {
            case 'configured':
                filteredProviders = providers.filter(p => p.configured);
                break;
            case 'notConfigured':
                filteredProviders = providers.filter(p => !p.configured);
                break;
            case 'all':
            default:
                filteredProviders = providers;
                break;
        }

        console.log(`📋 Showing ${filteredProviders.length} of ${providers.length} providers`);

        if (filteredProviders.length === 0) {
            const emptyMessage = document.createElement('div');
            emptyMessage.style.gridColumn = '1 / -1';
            emptyMessage.style.textAlign = 'center';
            emptyMessage.style.padding = 'var(--spacing-xl)';
            emptyMessage.style.color = 'var(--text-secondary)';
            emptyMessage.textContent = this.providerFilter === 'configured' 
                ? 'No configured providers found. Configure providers in appsettings.json or use manual configuration.'
                : this.providerFilter === 'notConfigured'
                ? 'All providers are configured.'
                : 'No providers available.';
            this.elements.providersGrid.appendChild(emptyMessage);
            return;
        }

        filteredProviders.forEach((provider) => {
            const card = document.createElement('div');
            card.className = 'provider-card';

            const connectionCount = Array.from(this.agents.values())
                .filter((a) => a.provider === provider.name).length;

            if (connectionCount > 0) {
                card.classList.add('connected');
            }

            const connectionStatus = connectionCount > 0
                ? `🔗 ${connectionCount} active connection${connectionCount > 1 ? 's' : ''}`
                : '○ Not connected';

            card.innerHTML = `
                <div class="provider-name">${this.escapeHtml(provider.name)}</div>
                <div class="provider-model">${this.escapeHtml(provider.model || 'Default model')}</div>
                <div class="provider-status ${provider.configured ? 'available' : 'unavailable'}">
                    ${provider.configured ? '✓ Configured' : '⚠ Not configured'}
                </div>
                <div class="provider-connections">${connectionStatus}</div>
            `;

            card.addEventListener('click', () => {
                // Generate a unique agentId when clicking, not when rendering
                const agentId = `agent-${provider.name}-${Date.now()}`;
                this.connectToProvider(provider.name, agentId);
            });
            this.elements.providersGrid.appendChild(card);
        });
        
        console.log('✅ Provider cards added to grid. Grid element:', this.elements.providersGrid);
    }

    /**
     * Connect to a provider
     */
    public async connectToProvider(providerName: string, agentId: string): Promise<void> {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            await this.showAlert('Connection Error', 'Not connected to server');
            return;
        }

        // Count existing connections to this provider
        const existingConnections = Array.from(this.agents.values())
            .filter((a) => a.provider === providerName).length;
        
        // Create a default display name with instance number if multiple connections
        const defaultDisplayName = existingConnections > 0 
            ? `${providerName} #${existingConnections + 1}`
            : providerName;

        // Show modal to get configuration
        this.showConnectionModal(defaultDisplayName, (tabName, workingDir) => {
            // Build config
            const config: AgentConfig = { provider: providerName };
            if (workingDir && workingDir.trim()) {
                config.workingDirectory = workingDir.trim();
            }

            const message: WebSocketMessage = {
                command: 'connect',
                config: config,
                agentId
            };

            this.ws!.send(JSON.stringify(message));
            this.createAgentTab(agentId, tabName, providerName);
            this.logToAgent(agentId, `Connecting to ${providerName}...`);
        });
    }

    /**
     * Show connection configuration modal
     */
    private showConnectionModal(defaultTabName: string, onConnect: (tabName: string, workingDir: string) => void): void {
        // Set default values
        this.elements.connectionTabName.value = defaultTabName;
        this.elements.connectionWorkingDir.value = '';

        // Show modal
        this.elements.connectionModal.classList.add('active');

        // Focus on tab name input
        setTimeout(() => this.elements.connectionTabName.focus(), 100);

        // Handle connect button
        const handleConnect = async () => {
            const tabName = this.elements.connectionTabName.value.trim();
            const workingDir = this.elements.connectionWorkingDir.value.trim();

            if (!tabName) {
                await this.showAlert('Validation Error', 'Tab name is required');
                return;
            }

            // Hide modal
            this.elements.connectionModal.classList.remove('active');

            // Clean up event listeners
            cleanup();

            // Call callback
            onConnect(tabName, workingDir);
        };

        // Handle cancel button
        const handleCancel = () => {
            this.elements.connectionModal.classList.remove('active');
            cleanup();
        };

        // Handle Enter key in inputs
        const handleKeyPress = (e: KeyboardEvent) => {
            if (e.key === 'Enter') {
                handleConnect();
            } else if (e.key === 'Escape') {
                handleCancel();
            }
        };

        // Handle click outside modal
        const handleClickOutside = (e: MouseEvent) => {
            if (e.target === this.elements.connectionModal) {
                handleCancel();
            }
        };

        // Add event listeners
        this.elements.connectionModalConnect.addEventListener('click', handleConnect);
        this.elements.connectionModalCancel.addEventListener('click', handleCancel);
        this.elements.connectionTabName.addEventListener('keypress', handleKeyPress);
        this.elements.connectionWorkingDir.addEventListener('keypress', handleKeyPress);
        this.elements.connectionModal.addEventListener('click', handleClickOutside);

        // Cleanup function to remove event listeners
        const cleanup = () => {
            this.elements.connectionModalConnect.removeEventListener('click', handleConnect);
            this.elements.connectionModalCancel.removeEventListener('click', handleCancel);
            this.elements.connectionTabName.removeEventListener('keypress', handleKeyPress);
            this.elements.connectionWorkingDir.removeEventListener('keypress', handleKeyPress);
            this.elements.connectionModal.removeEventListener('click', handleClickOutside);
        };
    }

    /**
     * Connect to a manually configured provider
     */
    public async connectManualProvider(
        provider: string,
        apiKey: string,
        model?: string,
        workingDir?: string
    ): Promise<void> {
        if (!provider || !apiKey) {
            await this.showAlert('Validation Error', 'Provider and API Key are required');
            return;
        }

        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            await this.showAlert('Connection Error', 'Not connected to server');
            return;
        }

        const agentId = `agent-manual-${provider}-${Date.now()}`;
        const config: AgentConfig = {
            provider,
            apiKey,
            verbose: 'false'
        };

        if (model) config.model = model;
        if (workingDir) config.workingDirectory = workingDir;

        const message: WebSocketMessage = {
            command: 'connect',
            config,
            agentId
        };

        this.ws.send(JSON.stringify(message));
        this.createAgentTab(agentId, `${provider} (manual)`);
    }

    /**
     * Create a new agent tab
     */
    private createAgentTab(agentId: string, displayName: string, providerName?: string): void {
        // Use displayName for tab, providerName for internal tracking (default to displayName if not provided)
        const provider = providerName || displayName;
        
        // Create tab button
        const tab = document.createElement('button');
        tab.type = 'button';
        tab.className = 'tab';
        tab.innerHTML = `${this.escapeHtml(displayName)} <span class="tab-close">×</span>`;

        const closeBtn = tab.querySelector('.tab-close');
        if (closeBtn) {
            closeBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this.closeAgent(agentId);
            });
        }

        tab.addEventListener('click', () => {
            console.log('🖱️ Tab clicked:', displayName, 'agentId:', agentId);
            this.switchToAgent(agentId);
        });
        this.elements.tabsContainer.appendChild(tab);

        // Create content div
        const content = document.createElement('div');
        content.className = 'tab-content';
        content.innerHTML = `
            <div class="task-section">
                <h2>📤 Send Task</h2>
                <textarea id="task-${agentId}" placeholder="Enter task for ${this.escapeHtml(displayName)}...">Create a file called hello.txt with the content 'Hello, World!'</textarea>
                <div class="button-group">
                    <button class="send-task-btn">Send Task</button>
                    <button class="reset-agent-btn secondary">Reset Agent</button>
                </div>
            </div>
            <div class="task-section">
                <h2>📋 Activity Log</h2>
                <div class="log" id="log-${agentId}"></div>
                <button class="clear-log-btn secondary">Clear Log</button>
            </div>
        `;

        // Add event listeners
        const sendBtn = content.querySelector('.send-task-btn');
        const resetBtn = content.querySelector('.reset-agent-btn');
        const clearBtn = content.querySelector('.clear-log-btn');

        if (sendBtn) {
            sendBtn.addEventListener('click', () => this.sendTaskToAgent(agentId));
        }
        if (resetBtn) {
            resetBtn.addEventListener('click', () => this.resetAgent(agentId));
        }
        if (clearBtn) {
            clearBtn.addEventListener('click', () => this.clearAgentLog(agentId));
        }

        this.elements.tabContents.appendChild(content);

        this.agents.set(agentId, {
            provider: provider,
            name: displayName,
            tabElement: tab as HTMLButtonElement,
            contentElement: content as HTMLDivElement
        });

        // Show tabs and hide empty state
        this.elements.agentTabs.style.display = 'block';
        this.elements.emptyState.style.display = 'none';

        this.switchToAgent(agentId);
    }

    /**
     * Switch to agent tab
     */
    private switchToAgent(agentId: string): void {
        console.log('🔄 Switching to agent:', agentId);
        console.log('   Available agents:', Array.from(this.agents.keys()));
        
        // Remove active class from all tabs
        document.querySelectorAll('.tab').forEach((t) => t.classList.remove('active'));
        document.querySelectorAll('.tab-content').forEach((c) => c.classList.remove('active'));

        const agent = this.agents.get(agentId);
        if (agent) {
            console.log('   ✅ Agent found:', agent.name);
            agent.tabElement.classList.add('active');
            agent.contentElement.classList.add('active');
            this.activeAgentId = agentId;
        } else {
            console.log('   ❌ Agent not found in agents map!');
        }
    }

    /**
     * Close an agent
     */
    private async closeAgent(agentId: string): Promise<void> {
        const confirmed = await this.showConfirm('Disconnect Agent', 'Are you sure you want to disconnect this agent?');
        if (confirmed) {
            console.log('🗑️ Closing agent:', agentId);
            console.log('   Total agents before:', this.agents.size);
            console.log('   Agent IDs:', Array.from(this.agents.keys()));
            
            this.sendToAgent(agentId, { command: 'disconnect', agentId });

            const agent = this.agents.get(agentId);
            if (agent) {
                console.log('   Removing tab and content for:', agent.name);
                agent.tabElement.remove();
                agent.contentElement.remove();
                this.agents.delete(agentId);
            } else {
                console.log('   ⚠️ Agent not found in map!');
            }

            console.log('   Total agents after:', this.agents.size);
            console.log('   Remaining agent IDs:', Array.from(this.agents.keys()));

            // Refresh provider grid to update connection counts
            if (this.availableProviders.length > 0) {
                this.displayProviders(this.availableProviders);
            }

            // Switch to first available agent or show empty state
            const remainingAgents = Array.from(this.agents.keys());
            if (remainingAgents.length > 0) {
                console.log('   Switching to first remaining agent:', remainingAgents[0]);
                this.switchToAgent(remainingAgents[0]);
            } else {
                console.log('   No remaining agents, hiding tabs section');
                this.elements.agentTabs.style.display = 'none';
                this.elements.emptyState.style.display = 'flex';
                this.activeAgentId = null;
            }
        }
    }

    /**
     * Send task to agent
     */
    private async sendTaskToAgent(agentId: string): Promise<void> {
        const taskInput = document.getElementById(`task-${agentId}`) as HTMLTextAreaElement;
        if (!taskInput) return;

        const task = taskInput.value.trim();
        if (!task) {
            await this.showAlert('Validation Error', 'Please enter a task');
            return;
        }

        this.sendToAgent(agentId, {
            command: 'send',
            data: task,
            agentId
        });

        this.logToAgent(agentId, `📤 Sent: ${task}`, 'info');
        taskInput.value = '';
    }

    /**
     * Reset agent
     */
    private async resetAgent(agentId: string): Promise<void> {
        const confirmed = await this.showConfirm('Reset Agent', 'Are you sure you want to reset this agent? This will reinitialize it.');
        if (confirmed) {
            const agent = this.agents.get(agentId);
            if (!agent) return;

            this.sendToAgent(agentId, {
                command: 'reset',
                config: { provider: agent.provider },
                agentId
            });

            this.logToAgent(agentId, '🔄 Resetting agent...', 'info');
        }
    }

    /**
     * Clear agent log
     */
    private clearAgentLog(agentId: string): void {
        const logElement = document.getElementById(`log-${agentId}`);
        if (logElement) {
            logElement.innerHTML = '';
        }
    }

    /**
     * Send message to agent
     */
    private sendToAgent(_agentId: string, message: WebSocketMessage): void {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify(message));
        }
    }

    /**
     * Convert URLs in text to clickable links
     */
    private linkifyUrls(text: string): string {
        // Regex to match URLs (http, https, ws, wss, ftp)
        const urlPattern = /(\b(https?|wss?|ftp):\/\/[-A-Z0-9+&@#\/%?=~_|!:,.;]*[-A-Z0-9+&@#\/%=~_|])/gim;
        
        return text.replace(urlPattern, (url) => {
            return `<a href="${url}" target="_blank" rel="noopener noreferrer">${url}</a>`;
        });
    }

    /**
     * Log message to agent's activity log
     */
    private logToAgent(agentId: string, message: string, level: LogLevel = 'info'): void {
        const logElement = document.getElementById(`log-${agentId}`);
        if (!logElement) return;

        const entry = document.createElement('div');
        entry.className = `log-entry ${level}`;
        const timestamp = new Date().toLocaleTimeString();
        
        // Convert URLs to clickable links
        const linkedMessage = this.linkifyUrls(message);
        
        entry.innerHTML = `<span class="log-time">[${timestamp}]</span> ${linkedMessage}`;
        logElement.appendChild(entry);
        logElement.scrollTop = logElement.scrollHeight;
    }

    /**
     * Log message to console (for global messages)
     */
    private logToConsole(message: string, level: LogLevel = 'info'): void {
        // For now, just use browser console
        console.log(`[${level.toUpperCase()}] ${message}`);
    }

    /**
     * Update agent tabs display
     */
    private updateAgentTabs(): void {
        if (this.agents.size === 0) {
            this.elements.agentTabs.style.display = 'none';
            this.elements.emptyState.style.display = 'flex';
        }
    }

    /**
     * Toggle manual configuration panel
     */
    public toggleManualConfig(): void {
        const display = this.elements.manualConfig.style.display;
        this.elements.manualConfig.style.display = display === 'none' || display === '' ? 'block' : 'none';
    }

    /**
     * Show alert dialog (replaces window.alert)
     */
    private async showAlert(title: string, message: string): Promise<void> {
        return new Promise((resolve) => {
            this.elements.generalModalTitle.textContent = title;
            this.elements.generalModalMessage.textContent = message;
            this.elements.generalModalInputContainer.style.display = 'none';
            this.elements.generalModalCancel.style.display = 'none';
            this.elements.generalModalConfirm.textContent = 'OK';
            this.elements.generalModal.classList.add('active');

            const handleConfirm = () => {
                this.elements.generalModal.classList.remove('active');
                cleanup();
                resolve();
            };

            const handleKeyPress = (e: KeyboardEvent) => {
                if (e.key === 'Enter' || e.key === 'Escape') {
                    handleConfirm();
                }
            };

            const handleClickOutside = (e: MouseEvent) => {
                if (e.target === this.elements.generalModal) {
                    handleConfirm();
                }
            };

            this.elements.generalModalConfirm.addEventListener('click', handleConfirm);
            document.addEventListener('keydown', handleKeyPress);
            this.elements.generalModal.addEventListener('click', handleClickOutside);

            const cleanup = () => {
                this.elements.generalModalConfirm.removeEventListener('click', handleConfirm);
                document.removeEventListener('keydown', handleKeyPress);
                this.elements.generalModal.removeEventListener('click', handleClickOutside);
            };

            setTimeout(() => this.elements.generalModalConfirm.focus(), 100);
        });
    }

    /**
     * Show confirm dialog (replaces window.confirm)
     */
    private async showConfirm(title: string, message: string): Promise<boolean> {
        return new Promise((resolve) => {
            this.elements.generalModalTitle.textContent = title;
            this.elements.generalModalMessage.textContent = message;
            this.elements.generalModalInputContainer.style.display = 'none';
            this.elements.generalModalCancel.style.display = 'inline-block';
            this.elements.generalModalCancel.textContent = 'Cancel';
            this.elements.generalModalConfirm.textContent = 'Confirm';
            this.elements.generalModal.classList.add('active');

            const handleConfirm = () => {
                this.elements.generalModal.classList.remove('active');
                cleanup();
                resolve(true);
            };

            const handleCancel = () => {
                this.elements.generalModal.classList.remove('active');
                cleanup();
                resolve(false);
            };

            const handleKeyPress = (e: KeyboardEvent) => {
                if (e.key === 'Enter') {
                    handleConfirm();
                } else if (e.key === 'Escape') {
                    handleCancel();
                }
            };

            const handleClickOutside = (e: MouseEvent) => {
                if (e.target === this.elements.generalModal) {
                    handleCancel();
                }
            };

            this.elements.generalModalConfirm.addEventListener('click', handleConfirm);
            this.elements.generalModalCancel.addEventListener('click', handleCancel);
            document.addEventListener('keydown', handleKeyPress);
            this.elements.generalModal.addEventListener('click', handleClickOutside);

            const cleanup = () => {
                this.elements.generalModalConfirm.removeEventListener('click', handleConfirm);
                this.elements.generalModalCancel.removeEventListener('click', handleCancel);
                document.removeEventListener('keydown', handleKeyPress);
                this.elements.generalModal.removeEventListener('click', handleClickOutside);
            };

            setTimeout(() => this.elements.generalModalConfirm.focus(), 100);
        });
    }

    /**
     * Show prompt dialog (replaces window.prompt)
     */
    private async showPrompt(title: string, message: string, defaultValue: string = ''): Promise<string | null> {
        return new Promise((resolve) => {
            this.elements.generalModalTitle.textContent = title;
            this.elements.generalModalMessage.textContent = message;
            this.elements.generalModalInputContainer.style.display = 'block';
            this.elements.generalModalInputLabel.textContent = message;
            this.elements.generalModalInput.value = defaultValue;
            this.elements.generalModalCancel.style.display = 'inline-block';
            this.elements.generalModalCancel.textContent = 'Cancel';
            this.elements.generalModalConfirm.textContent = 'OK';
            this.elements.generalModal.classList.add('active');

            const handleConfirm = () => {
                const value = this.elements.generalModalInput.value;
                this.elements.generalModal.classList.remove('active');
                cleanup();
                resolve(value);
            };

            const handleCancel = () => {
                this.elements.generalModal.classList.remove('active');
                cleanup();
                resolve(null);
            };

            const handleKeyPress = (e: KeyboardEvent) => {
                if (e.key === 'Enter') {
                    handleConfirm();
                } else if (e.key === 'Escape') {
                    handleCancel();
                }
            };

            const handleClickOutside = (e: MouseEvent) => {
                if (e.target === this.elements.generalModal) {
                    handleCancel();
                }
            };

            this.elements.generalModalConfirm.addEventListener('click', handleConfirm);
            this.elements.generalModalCancel.addEventListener('click', handleCancel);
            this.elements.generalModalInput.addEventListener('keypress', handleKeyPress);
            document.addEventListener('keydown', handleKeyPress);
            this.elements.generalModal.addEventListener('click', handleClickOutside);

            const cleanup = () => {
                this.elements.generalModalConfirm.removeEventListener('click', handleConfirm);
                this.elements.generalModalCancel.removeEventListener('click', handleCancel);
                this.elements.generalModalInput.removeEventListener('keypress', handleKeyPress);
                document.removeEventListener('keydown', handleKeyPress);
                this.elements.generalModal.removeEventListener('click', handleClickOutside);
            };

            setTimeout(() => this.elements.generalModalInput.focus(), 100);
        });
    }
}
