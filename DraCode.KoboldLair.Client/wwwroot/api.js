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
            this.ws = new WebSocketClient('/wyvern');

            this.ws.on('response', (data) => {
                const request = this.pendingRequests.get(data.id);
                if (request) {
                    request.resolve(data.data);
                    this.pendingRequests.delete(data.id);
                }
            });

            this.ws.on('error', (data) => {
                // Only handle message-level errors (not WebSocket connection errors)
                if (data && data.id) {
                    const request = this.pendingRequests.get(data.id);
                    if (request) {
                        request.reject(new Error(data.error || 'Unknown error'));
                        this.pendingRequests.delete(data.id);
                    }
                }
            });

            this.ws.onStatusChange((status) => {
                this.connected = (status === 'connected');
                console.log('API WebSocket status:', status);
            });

            await this.ws.connect();
            this.connected = true;
        } finally {
            this.connecting = false;
        }
    }

    async sendCommand(command, data = null) {
        // Check if connected
        if (!this.connected) {
            throw new Error('Not connected. Please connect first.');
        }

        const requestId = `req_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

        return new Promise((resolve, reject) => {
            this.pendingRequests.set(requestId, { resolve, reject });

            // Send the message
            this.ws.send({
                id: requestId,
                command,
                data
            }).catch(error => {
                this.pendingRequests.delete(requestId);
                reject(error);
            });

            // Timeout after 30 seconds
            setTimeout(() => {
                if (this.pendingRequests.has(requestId)) {
                    this.pendingRequests.delete(requestId);
                    reject(new Error('Request timeout'));
                }
            }, 30000);
        });
    }

    disconnect() {
        if (this.ws) {
            this.ws.disconnect();
            this.connected = false;
            this.connecting = false;
        }
    }

    isConnected() {
        return this.connected;
    }

    async getStats() {
        return this.sendCommand('get_stats');
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

    disconnect() {
        if (this.ws) {
            this.ws.disconnect();
            this.ws = null;
            this.connected = false;
        }
    }
}
