import type { Agent, AgentConfig, LogLevel, Provider, WebSocketMessage, WebSocketResponse } from './types.js';
import { createProviderCard } from './provider-card-poc.js';
import { modals, ConnectionModal } from './modal-poc.js';
import { toast } from 'birko-web-components';

/**
 * Main application class managing WebSocket connection and multi-agent interface
 * MIGRATED: Now using Birko.Web components for modals, cards, and notifications
 */
export class DraCodeClient {
    private ws: WebSocket | null = null;
    private agents: Map<string, Agent> = new Map();
    private activeAgentId: string | null = null;
    private availableProviders: Provider[] = [];
    private providerFilter: 'configured' | 'all' | 'notConfigured' = 'configured';
    private connectionModal: ConnectionModal;

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

        // Initialize connection modal (Birko component)
        this.connectionModal = new ConnectionModal(modals);

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
     * MIGRATED: Now uses toast notifications instead of console.log
     */
    public connectToServer(): void {
        const url = this.elements.wsUrl.value;

        try {
            this.ws = new WebSocket(url);

            this.ws.onopen = () => {
                this.logToConsole('✅ Connected to WebSocket server', 'success');
                this.updateServerStatus(true);
                toast.success('Connected to WebSocket server', { duration: 3000 });
            };

            this.ws.onmessage = (event) => this.handleServerMessage(event);

            this.ws.onerror = () => {
                this.logToConsole('❌ WebSocket error', 'error');
                toast.error('WebSocket connection error');
            };

            this.ws.onclose = () => {
                this.logToConsole('Connection closed', 'error');
                this.updateServerStatus(false);
                toast.warning('Disconnected from server', { duration: 5000 });
            };
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Unknown error';
            this.logToConsole(`❌ Failed to connect: ${message}`, 'error');
            toast.error(`Failed to connect: ${message}`);
        }
    }

    /**
     * Disconnect from WebSocket server
     * MIGRATED: Now uses toast notifications
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
            toast.info('Disconnected from server');
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
     * MIGRATED: Uses toast for status messages
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

            // Show toast for server responses
            if (response.Status === 'success' && response.Message) {
                toast.success(response.Message, { duration: 2000 });
            } else if (response.Status === 'error') {
                toast.error(response.Message || 'An error occurred');
            }

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

                // Refresh provider grid when agent status changes
                if (['success', 'error', 'connected', 'disconnected', 'reset'].includes(response.Status)) {
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
     * Sanitize string for use as HTML ID or CSS selector
     */
    private sanitizeId(text: string): string {
        return text.replace(/[^a-zA-Z0-9-]/g, '-').replace(/-+/g, '-');
    }

    /**
     * Auto-collapse server connection and providers sections
     */
    private autoCollapseConfigSections(): void {
        ['serverConnection', 'providersContent'].forEach(sectionId => {
            const content = document.getElementById(sectionId);
            const icon = document.getElementById(`${sectionId}Icon`);
            const header = icon?.closest('.collapsible-header');

            if (content && icon && header && !content.classList.contains('collapsed')) {
                content.classList.add('collapsed');
                icon.classList.add('collapsed');
                header.classList.add('collapsed');
                localStorage.setItem(`section_${sectionId}_collapsed`, 'true');

                if (sectionId === 'providersContent') {
                    const providerFilter = document.querySelector('.provider-filter') as HTMLElement;
                    if (providerFilter) {
                        providerFilter.style.display = 'none';
                    }

                    if (this.availableProviders.length > 0) {
                        this.updateProviderCountBadge(header as HTMLElement, this.availableProviders.length);
                    }
                }
            }
        });
    }

    /**
     * Update provider count badge on collapsed header
     */
    private updateProviderCountBadge(header: HTMLElement, count: number): void {
        const existingBadge = header.querySelector('.provider-count-badge');
        if (existingBadge) {
            existingBadge.remove();
        }

        if (count > 0) {
            const badge = document.createElement('span');
            badge.className = 'provider-count-badge';
            badge.textContent = `${count}`;
            badge.title = `${count} provider(s) available`;
            header.appendChild(badge);
        }
    }

    /**
     * Handle streaming messages from agent
     * MIGRATED: Uses toast for important events
     */
    private handleStreamMessage(response: WebSocketResponse): void {
        const agentId = response.AgentId;
        if (!agentId || !this.agents.has(agentId)) {
            console.warn('Stream message for unknown agent:', agentId);
            return;
        }

        const messageType = response.MessageType || 'info';
        const content = response.Message || '';

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

        // Show toast for tool calls and results
        if (messageType === 'tool_call') {
            toast.info(`Tool: ${content.split(' ')[0]}`, { duration: 1500 });
        } else if (messageType === 'error') {
            toast.error(content);
        }
    }

    /**
     * Handle interactive prompt from agent
     * MIGRATED: Uses Birko modal for prompts
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

        // Use Birko modal for prompt
        const promptMessage = context ? `${context}\n\n${question}` : question;
        const promptResponse = await modals.prompt('Agent Question', promptMessage, '');

        if (promptResponse !== null) {
            if (this.ws && this.ws.readyState === WebSocket.OPEN) {
                const message: WebSocketMessage = {
                    command: 'prompt_response',
                    agentId: agentId,
                    promptId: promptId,
                    data: promptResponse
                };
                this.ws.send(JSON.stringify(message));
                this.logToAgent(agentId, `✅ Your answer: ${this.escapeHtml(promptResponse)}`, 'success');
            } else {
                console.error('❌ Cannot send prompt_response: WebSocket not open.');
            }
        } else {
            if (this.ws && this.ws.readyState === WebSocket.OPEN) {
                const message: WebSocketMessage = {
                    command: 'prompt_response',
                    agentId: agentId,
                    promptId: promptId,
                    data: ''
                };
                this.ws.send(JSON.stringify(message));
                this.logToAgent(agentId, '❌ Prompt cancelled', 'warning');
            } else {
                console.error('❌ Cannot send prompt_response: WebSocket not open.');
            }
        }
    }

    /**
     * Handle provider list response
     * MIGRATED: Uses Birko provider cards
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
            toast.info(`Found ${this.availableProviders.length} providers`);
        } catch (error) {
            console.error('❌ Error parsing provider list:', error);
            this.logToConsole('❌ Failed to parse provider list', 'error');
            toast.error('Failed to parse provider list');
        }
    }

    /**
     * Display providers in grid
     * MIGRATED: Now uses Birko provider cards
     */
    private displayProviders(providers: Provider[]): void {
        console.log('🎨 Displaying providers:', providers);
        console.log('🔍 Current filter:', this.providerFilter);
        this.elements.providersGrid.innerHTML = '';

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
            const empty = document.createElement('div');
            empty.style.gridColumn = '1 / -1';
            empty.style.textAlign = 'center';
            empty.style.padding = 'var(--spacing-xl)';
            empty.style.color = 'var(--text-secondary)';
            empty.textContent = this.providerFilter === 'configured'
                ? 'No configured providers found. Configure providers in appsettings.json or use manual configuration.'
                : this.providerFilter === 'notConfigured'
                ? 'All providers are configured.'
                : 'No providers available.';
            this.elements.providersGrid.appendChild(empty);
            return;
        }

        filteredProviders.forEach((provider) => {
            const connectionCount = Array.from(this.agents.values())
                .filter((a) => a.provider === provider.name).length;

            const card = createProviderCard({
                ...provider,
                connectionCount
            });

            if (connectionCount > 0) {
                card.setAttribute('connected', '');
            }

            card.addEventListener('connect', async () => {
                const result = await this.connectionModal.show({
                    defaultTabName: provider.name
                });

                if (result) {
                    const agentId = `agent-${this.sanitizeId(provider.name)}-${Date.now()}`;
                    this.connectToProviderWithName(provider.name, agentId, result.tabName, result.workingDir);
                }
            });

            this.elements.providersGrid.appendChild(card);
        });

        console.log('✅ Provider cards added to grid. Grid element:', this.elements.providersGrid);
    }

    /**
     * Connect to a provider
     * MIGRATED: Uses Birko connection modal
     */
    public async connectToProvider(providerName: string, agentId: string): Promise<void> {
        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            await modals.alert('Connection Error', 'Not connected to server');
            return;
        }

        const existingConnections = Array.from(this.agents.values())
            .filter((a) => a.provider === providerName).length;

        const defaultDisplayName = existingConnections > 0
            ? `${providerName} #${existingConnections + 1}`
            : providerName;

        const result = await this.connectionModal.show({ defaultTabName: defaultDisplayName });

        if (result) {
            this.connectToProviderWithName(providerName, agentId, result.tabName, result.workingDir);
        }
    }

    /**
     * Internal method to connect with given configuration
     */
    private connectToProviderWithName(providerName: string, agentId: string, tabName: string, workingDir: string): void {
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
        toast.info(`Connecting to ${providerName}...`);
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
            await modals.alert('Validation Error', 'Provider and API Key are required');
            return;
        }

        if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
            await modals.alert('Connection Error', 'Not connected to server');
            return;
        }

        const agentId = `agent-manual-${this.sanitizeId(provider)}-${Date.now()}`;
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
        toast.success(`Connecting to ${provider}...`);
    }

    /**
     * Create a new agent tab
     */
    private createAgentTab(agentId: string, displayName: string, providerName?: string): void {
        const provider = providerName || displayName;

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

        const content = document.createElement('div');
        content.className = 'tab-content';
        content.innerHTML = `
            <div class="task-section">
                <h2>📤 Send Task</h2>
                <textarea id="task-${agentId}" placeholder="Enter task for ${this.escapeHtml(displayName)}..."></textarea>
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

        const sendBtn = content.querySelector('.send-task-btn');
        const resetBtn = content.querySelector('.reset-agent-btn');
        const clearBtn = content.querySelector('.clear-log-btn');
        const taskTextarea = content.querySelector(`#task-${agentId}`) as HTMLTextAreaElement;

        if (sendBtn) {
            sendBtn.addEventListener('click', () => this.sendTaskToAgent(agentId));
        }
        if (resetBtn) {
            resetBtn.addEventListener('click', () => this.resetAgent(agentId));
        }
        if (clearBtn) {
            clearBtn.addEventListener('click', () => this.clearAgentLog(agentId));
        }
        if (taskTextarea) {
            taskTextarea.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    this.sendTaskToAgent(agentId);
                }
            });
        }

        this.elements.tabContents.appendChild(content);

        this.agents.set(agentId, {
            provider: provider,
            name: displayName,
            tabElement: tab as HTMLButtonElement,
            contentElement: content as HTMLDivElement
        });

        this.elements.agentTabs.style.display = 'block';
        this.elements.emptyState.style.display = 'none';

        if (this.agents.size === 1) {
            this.autoCollapseConfigSections();
        }

        this.switchToAgent(agentId);
    }

    /**
     * Switch to agent tab
     */
    private switchToAgent(agentId: string): void {
        console.log('🔄 Switching to agent:', agentId);
        console.log('   Available agents:', Array.from(this.agents.keys()));

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
     * MIGRATED: Uses Birko confirm dialog
     */
    private async closeAgent(agentId: string): Promise<void> {
        const confirmed = await modals.confirm('Disconnect Agent', 'Are you sure you want to disconnect this agent?');
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

            if (this.availableProviders.length > 0) {
                this.displayProviders(this.availableProviders);
            }

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

            toast.info('Agent disconnected');
        }
    }

    /**
     * Send task to agent
     * MIGRATED: Uses Birko alert for validation
     */
    private async sendTaskToAgent(agentId: string): Promise<void> {
        const taskInput = document.getElementById(`task-${agentId}`) as HTMLTextAreaElement;
        if (!taskInput) return;

        const task = taskInput.value.trim();
        if (!task) {
            await modals.alert('Validation Error', 'Please enter a task');
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
     * MIGRATED: Uses Birko confirm dialog
     */
    private async resetAgent(agentId: string): Promise<void> {
        const confirmed = await modals.confirm('Reset Agent', 'Are you sure you want to reset this agent? This will reinitialize it.');
        if (confirmed) {
            const agent = this.agents.get(agentId);
            if (!agent) return;

            this.sendToAgent(agentId, {
                command: 'reset',
                config: { provider: agent.provider },
                agentId
            });

            this.logToAgent(agentId, '🔄 Resetting agent...', 'info');
            toast.info(`Resetting ${agent.name}...`);
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

        const linkedMessage = this.linkifyUrls(message);

        entry.innerHTML = `<span class="log-time">[${timestamp}]</span> ${linkedMessage}`;
        logElement.appendChild(entry);
        logElement.scrollTop = logElement.scrollHeight;
    }

    /**
     * Log message to console (for global messages)
     */
    private logToConsole(message: string, level: LogLevel = 'info'): void {
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
     * Sanitize a string to be used as a directory name
     */
    private sanitizeDirectoryName(name: string): string {
        if (!name) return '';

        const normalized = name.normalize('NFD').replace(/[\u0300-\u036f]/g, '');

        let sanitized = normalized
            .toLowerCase()
            .replace(/[^a-z0-9_-]+/g, '-')
            .replace(/^-+|-+$/g, '')
            .replace(/-+/g, '-');

        if (!sanitized) {
            sanitized = 'workspace';
        }

        return sanitized;
    }
}
