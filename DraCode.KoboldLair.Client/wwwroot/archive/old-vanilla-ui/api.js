import { WebSocketClient } from './websocket.js';

export class ApiClient {
    constructor() {
        this.ws = null;
        this.pendingRequests = new Map();
        this.connected = false;
        this.connecting = false;
    }

    async connect() {
        if (this.ws && this.connected) return;
        if (this.connecting) return;

        this.connecting = true;

        try {
            // Clean up previous WebSocket if reconnecting (prevents handler accumulation)
            if (this.ws) {
                this.ws.removeAllHandlers();
                this.ws.disconnect();
                this.ws = null;
            }

            this.ws = new WebSocketClient('/wyvern');

            this.ws.on('response', (data) => {
                const request = this.pendingRequests.get(data.id);
                if (request) {
                    clearTimeout(request.timeout);
                    request.resolve(data.data);
                    this.pendingRequests.delete(data.id);
                }
            });

            this.ws.on('error', (data) => {
                // Only handle message-level errors (not WebSocket connection errors)
                if (data && data.id) {
                    const request = this.pendingRequests.get(data.id);
                    if (request) {
                        clearTimeout(request.timeout);
                        request.reject(new Error(data.error || 'Unknown error'));
                        this.pendingRequests.delete(data.id);
                    }
                }
            });

            this.ws.onStatusChange((status) => {
                const wasConnected = this.connected;
                this.connected = (status === 'connected');
                console.log('API WebSocket status:', status);

                // Fail all pending requests immediately on disconnect
                // (inspired by Birko WebSocketServer OnClientDisconnected pattern)
                if (wasConnected && !this.connected) {
                    this._failPendingRequests('Connection lost');
                }
            });

            await this.ws.connect();
            this.connected = true;
        } finally {
            this.connecting = false;
        }
    }

    /**
     * Fail all pending requests with the given reason.
     * Prevents requests from hanging for 30s after connection drops.
     */
    _failPendingRequests(reason) {
        const pending = Array.from(this.pendingRequests.entries());
        this.pendingRequests.clear();
        for (const [id, request] of pending) {
            clearTimeout(request.timeout);
            request.reject(new Error(reason));
        }
        if (pending.length > 0) {
            console.warn(`ApiClient: Failed ${pending.length} pending request(s): ${reason}`);
        }
    }

    async sendCommand(command, data = null) {
        // Check if connected
        if (!this.connected) {
            throw new Error('Not connected. Please connect first.');
        }

        const requestId = `req_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

        return new Promise((resolve, reject) => {
            // Timeout after 30 seconds (stored so it can be cleared on response/disconnect)
            const timeout = setTimeout(() => {
                if (this.pendingRequests.has(requestId)) {
                    this.pendingRequests.delete(requestId);
                    reject(new Error('Request timeout'));
                }
            }, 30000);

            this.pendingRequests.set(requestId, { resolve, reject, timeout });

            // Send the message
            this.ws.send({
                id: requestId,
                command,
                data
            }).catch(error => {
                clearTimeout(timeout);
                this.pendingRequests.delete(requestId);
                reject(error);
            });
        });
    }

    disconnect() {
        this._failPendingRequests('Client disconnected');
        if (this.ws) {
            this.ws.removeAllHandlers();
            this.ws.disconnect();
            this.ws = null;
        }
        this.connected = false;
        this.connecting = false;
    }

    isConnected() {
        return this.connected;
    }

    async getStats() {
        return this.sendCommand('get_stats');
    }

    async getMetrics(timeRangeHours = 24) {
        return this.sendCommand('get_metrics', { timeRangeHours });
    }

    async getComparison(projectId) {
        return this.sendCommand('get_comparison', { projectId });
    }

    async getProjects() {
        return this.sendCommand('get_projects');
    }

    async getHierarchy() {
        return this.sendCommand('get_hierarchy');
    }

    async getProviders() {
        return this.sendCommand('get_providers');
    }

    async getProjectConfig(projectId) {
        return this.sendCommand('get_project_config', { projectId });
    }

    async updateProjectConfig(projectId, maxParallelKobolds) {
        return this.sendCommand('update_project_config', { projectId, maxParallelKobolds });
    }

    async getProjectProviders(projectId) {
        return this.sendCommand('get_project_providers', { projectId });
    }

    async getProjectAgents(projectId) {
        return this.sendCommand('get_project_agents', { projectId });
    }

    async updateProjectProviders(projectId, agentType, providerName, modelOverride) {
        return this.sendCommand('update_project_providers', {
            projectId,
            agentType,
            providerName,
            modelOverride
        });
    }

    async toggleAgent(projectId, agentType, enabled) {
        return this.sendCommand('toggle_agent', { projectId, agentType, enabled });
    }

    async getAgentStatus(projectId, agentType) {
        return this.sendCommand('get_agent_status', { projectId, agentType });
    }

    async getImplementationSummary(projectId) {
        return this.sendCommand('get_implementation_summary', { projectId });
    }

    async configureProvider(agentType, providerName, modelOverride) {
        return this.sendCommand('configure_provider', { agentType, providerName, modelOverride });
    }

    async validateProvider(providerName) {
        return this.sendCommand('validate_provider', { providerName });
    }

    async getProvidersForAgent(agentType) {
        return this.sendCommand('get_providers_for_agent', { agentType });
    }

    async getAllProjectConfigs() {
        return this.sendCommand('get_all_project_configs');
    }

    async getProjectConfigFull(projectId) {
        return this.sendCommand('get_project_config_full', { projectId });
    }

    async updateProjectConfigFull(projectId, config) {
        return this.sendCommand('update_project_config_full', { projectId, ...config });
    }

    async deleteProjectConfig(projectId) {
        return this.sendCommand('delete_project_config', { projectId });
    }

    async getAgentConfig(projectId, agentType) {
        return this.sendCommand('get_agent_config', { projectId, agentType });
    }

    async updateAgentConfig(projectId, agentType, provider, model, enabled) {
        return this.sendCommand('update_agent_config', { projectId, agentType, provider, model, enabled });
    }

    async retryAnalysis(projectId) {
        return this.sendCommand('retry_analysis', { projectId });
    }
}
