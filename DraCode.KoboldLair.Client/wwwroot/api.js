import CONFIG from './config.js';

export class ApiClient {
    constructor(baseUrl = CONFIG.apiUrl) {
        this.baseUrl = baseUrl;
    }

    async fetch(endpoint, options = {}) {
        try {
            const response = await fetch(`${this.baseUrl}${endpoint}`, {
                ...options,
                headers: {
                    'Content-Type': 'application/json',
                    ...options.headers
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            return await response.json();
        } catch (error) {
            console.error(`API Error (${endpoint}):`, error);
            throw error;
        }
    }

    async getStats() {
        return this.fetch('/api/stats');
    }

    async getProjects() {
        return this.fetch('/api/projects');
    }

    async getHierarchy() {
        return this.fetch('/api/hierarchy');
    }

    async getProviders() {
        return this.fetch('/api/providers');
    }

    async getProjectConfig(projectId) {
        return this.fetch(`/api/projects/${projectId}/config`);
    }

    async updateProjectConfig(projectId, maxParallelKobolds) {
        return this.fetch(`/api/projects/${projectId}/config`, {
            method: 'POST',
            body: JSON.stringify({ maxParallelKobolds })
        });
    }

    async getProjectProviders(projectId) {
        return this.fetch(`/api/projects/${projectId}/providers`);
    }

    async updateProjectProviders(projectId, agentType, providerName, modelOverride) {
        return this.fetch(`/api/projects/${projectId}/providers`, {
            method: 'POST',
            body: JSON.stringify({ agentType, providerName, modelOverride })
        });
    }

    async toggleAgent(projectId, agentType, enabled) {
        return this.fetch(`/api/projects/${projectId}/agents/${agentType}/toggle`, {
            method: 'POST',
            body: JSON.stringify({ enabled })
        });
    }

    async getAgentStatus(projectId, agentType) {
        return this.fetch(`/api/projects/${projectId}/agents/${agentType}/status`);
    }

    async configureProvider(agentType, providerName, modelOverride) {
        return this.fetch('/api/providers/configure', {
            method: 'POST',
            body: JSON.stringify({ agentType, providerName, modelOverride })
        });
    }

    async validateProvider(providerName) {
        return this.fetch(`/api/providers/validate/${providerName}`);
    }

    async getProvidersForAgent(agentType) {
        return this.fetch(`/api/providers/agents/${agentType}`);
    }
}
