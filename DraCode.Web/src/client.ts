import type { Agent, AgentConfig, LogLevel, Provider, WebSocketMessage, WebSocketResponse } from './types.js';

/**
 * Main application class managing WebSocket connection and multi-agent interface
 */
export class DraCodeClient {
    private ws: WebSocket | null = null;
    private agents: Map<string, Agent> = new Map();
    private activeAgentId: string | null = null;
    private availableProviders: Provider[] = [];

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
            manualConfig: this.getElement('manualConfig')
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
            console.log('   Data type:', typeof response.Data);
            console.log('   Data value:', response.Data);

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
        this.elements.providersGrid.innerHTML = '';

        providers.forEach((provider) => {
            const card = document.createElement('div');
            card.className = 'provider-card';

            const agentId = `agent-${provider.name}-${Date.now()}`;
            const isConnected = Array.from(this.agents.values()).some(
                (a) => a.provider === provider.name
            );

            if (isConnected) {
                card.classList.add('connected');
            }

            card.innerHTML = `
                <div class="provider-name">${this.escapeHtml(provider.name)}</div>
                <div class="provider-model">${this.escapeHtml(provider.model || 'Default model')}</div>
                <div class="provider-status ${provider.configured ? 'available' : 'unavailable'}">
                    ${provider.configured ? '✓ Configured' : '⚠ Not configured'}
                </div>
            `;

            card.addEventListener('click', () => this.connectToProvider(provider.name, agentId));
            this.elements.providersGrid.appendChild(card);
        });
        
        console.log('✅ Provider cards added to grid. Grid element:', this.elements.providersGrid);
    }

    /**
     * Connect to a provider
     */
    public connectToProvider(providerName: string, agentId: string): void {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            alert('Not connected to server');
            return;
        }

        // Check if already connected
        if (Array.from(this.agents.values()).some((a) => a.provider === providerName)) {
            alert('Already connected to this provider');
            return;
        }

        const message: WebSocketMessage = {
            command: 'connect',
            config: { provider: providerName },
            agentId
        };

        this.ws.send(JSON.stringify(message));
        this.createAgentTab(agentId, providerName);
        this.logToAgent(agentId, `Connecting to ${providerName}...`);
    }

    /**
     * Connect to a manually configured provider
     */
    public connectManualProvider(
        provider: string,
        apiKey: string,
        model?: string,
        workingDir?: string
    ): void {
        if (!provider || !apiKey) {
            alert('Provider and API Key are required');
            return;
        }

        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            alert('Not connected to server');
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
    private createAgentTab(agentId: string, providerName: string): void {
        // Create tab button
        const tab = document.createElement('button');
        tab.className = 'tab';
        tab.innerHTML = `${this.escapeHtml(providerName)} <span class="tab-close">×</span>`;

        const closeBtn = tab.querySelector('.tab-close');
        if (closeBtn) {
            closeBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this.closeAgent(agentId);
            });
        }

        tab.addEventListener('click', () => this.switchToAgent(agentId));
        this.elements.tabsContainer.appendChild(tab);

        // Create content div
        const content = document.createElement('div');
        content.className = 'tab-content';
        content.innerHTML = `
            <div class="task-section">
                <h2>📤 Send Task</h2>
                <textarea id="task-${agentId}" placeholder="Enter task for ${this.escapeHtml(providerName)}...">Create a file called hello.txt with the content 'Hello, World!'</textarea>
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
            provider: providerName,
            name: providerName,
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
        // Remove active class from all tabs
        document.querySelectorAll('.tab').forEach((t) => t.classList.remove('active'));
        document.querySelectorAll('.tab-content').forEach((c) => c.classList.remove('active'));

        const agent = this.agents.get(agentId);
        if (agent) {
            agent.tabElement.classList.add('active');
            agent.contentElement.classList.add('active');
            this.activeAgentId = agentId;
        }
    }

    /**
     * Close an agent
     */
    private closeAgent(agentId: string): void {
        if (confirm('Disconnect this agent?')) {
            this.sendToAgent(agentId, { command: 'disconnect', agentId });

            const agent = this.agents.get(agentId);
            if (agent) {
                agent.tabElement.remove();
                agent.contentElement.remove();
                this.agents.delete(agentId);
            }

            // Switch to first available agent or show empty state
            const remainingAgents = Array.from(this.agents.keys());
            if (remainingAgents.length > 0) {
                this.switchToAgent(remainingAgents[0]);
            } else {
                this.elements.agentTabs.style.display = 'none';
                this.elements.emptyState.style.display = 'flex';
                this.activeAgentId = null;
            }
        }
    }

    /**
     * Send task to agent
     */
    private sendTaskToAgent(agentId: string): void {
        const taskInput = document.getElementById(`task-${agentId}`) as HTMLTextAreaElement;
        if (!taskInput) return;

        const task = taskInput.value.trim();
        if (!task) {
            alert('Please enter a task');
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
    private resetAgent(agentId: string): void {
        if (confirm('Reset this agent? This will reinitialize it.')) {
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
     * Log message to agent's activity log
     */
    private logToAgent(agentId: string, message: string, level: LogLevel = 'info'): void {
        const logElement = document.getElementById(`log-${agentId}`);
        if (!logElement) return;

        const entry = document.createElement('div');
        entry.className = `log-entry ${level}`;
        const timestamp = new Date().toLocaleTimeString();
        entry.innerHTML = `<span class="log-time">[${timestamp}]</span> ${message}`;
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
}
