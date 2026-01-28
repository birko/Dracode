/**
 * DraCode KoboldLair - Project Configuration Client
 * 
 * JavaScript client library for managing project configurations
 * via the KoboldLair API endpoints.
 */

class ProjectConfigClient {
    constructor(baseUrl = '') {
        this.baseUrl = baseUrl || window.location.origin;
    }

    /**
     * Helper method for making API requests
     * @private
     */
    async _request(url, options = {}) {
        const response = await fetch(`${this.baseUrl}${url}`, {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            }
        });

        if (!response.ok) {
            const error = await response.json().catch(() => ({ error: response.statusText }));
            throw new Error(error.error || error.message || 'Request failed');
        }

        return response.json();
    }

    // === Project Configuration Endpoints ===

    /**
     * Get all project configurations
     * @returns {Promise<{defaults: {maxParallelKobolds: number}, projects: Array}>}
     */
    async getAllConfigs() {
        return this._request('/api/project-configs');
    }

    /**
     * Get default configuration values
     * @returns {Promise<{maxParallelKobolds: number}>}
     */
    async getDefaults() {
        return this._request('/api/project-configs/defaults');
    }

    /**
     * Get specific project configuration
     * @param {string} projectId - Project identifier
     * @returns {Promise<Object>}
     */
    async getConfig(projectId) {
        return this._request(`/api/project-configs/${projectId}`);
    }

    /**
     * Create or fully update project configuration
     * @param {string} projectId - Project identifier
     * @param {Object} config - Full configuration object
     * @returns {Promise<{success: boolean, message: string, config: Object}>}
     */
    async updateConfig(projectId, config) {
        return this._request(`/api/project-configs/${projectId}`, {
            method: 'PUT',
            body: JSON.stringify(config)
        });
    }

    /**
     * Partially update project configuration
     * @param {string} projectId - Project identifier
     * @param {Object} partialConfig - Partial configuration object
     * @returns {Promise<{success: boolean, message: string, config: Object}>}
     */
    async patchConfig(projectId, partialConfig) {
        return this._request(`/api/project-configs/${projectId}`, {
            method: 'PATCH',
            body: JSON.stringify(partialConfig)
        });
    }

    /**
     * Delete project configuration
     * @param {string} projectId - Project identifier
     * @returns {Promise<{success: boolean, message: string}>}
     */
    async deleteConfig(projectId) {
        return this._request(`/api/project-configs/${projectId}`, {
            method: 'DELETE'
        });
    }

    // === Agent-Specific Configuration ===

    /**
     * Get agent-specific settings for a project
     * @param {string} projectId - Project identifier
     * @param {string} agentType - Agent type (wyrm, drake, kobold)
     * @returns {Promise<{provider: string, model: string, enabled: boolean}>}
     */
    async getAgentConfig(projectId, agentType) {
        return this._request(`/api/project-configs/${projectId}/agents/${agentType}`);
    }

    /**
     * Update agent-specific settings for a project
     * @param {string} projectId - Project identifier
     * @param {string} agentType - Agent type (wyrm, drake, kobold)
     * @param {Object} config - Agent configuration {provider?, model?, enabled?}
     * @returns {Promise<{success: boolean, message: string}>}
     */
    async updateAgentConfig(projectId, agentType, config) {
        return this._request(`/api/project-configs/${projectId}/agents/${agentType}`, {
            method: 'PUT',
            body: JSON.stringify(config)
        });
    }

    // === Convenience Methods ===

    /**
     * Set maximum parallel Kobolds for a project
     * @param {string} projectId - Project identifier
     * @param {number} maxKobolds - Maximum parallel Kobolds
     * @returns {Promise<{success: boolean, message: string, config: Object}>}
     */
    async setMaxParallelKobolds(projectId, maxKobolds) {
        return this.patchConfig(projectId, { maxParallelKobolds: maxKobolds });
    }

    /**
     * Enable or disable an agent for a project
     * @param {string} projectId - Project identifier
     * @param {string} agentType - Agent type (wyrm, drake, kobold)
     * @param {boolean} enabled - Whether to enable the agent
     * @returns {Promise<{success: boolean, message: string}>}
     */
    async toggleAgent(projectId, agentType, enabled) {
        return this.updateAgentConfig(projectId, agentType, { enabled });
    }

    /**
     * Set provider and model for an agent
     * @param {string} projectId - Project identifier
     * @param {string} agentType - Agent type (wyrm, drake, kobold)
     * @param {string} provider - Provider name
     * @param {string} [model] - Optional model override
     * @returns {Promise<{success: boolean, message: string}>}
     */
    async setAgentProvider(projectId, agentType, provider, model = null) {
        return this.updateAgentConfig(projectId, agentType, { provider, model });
    }
}

// Export for use in modules or make available globally
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ProjectConfigClient;
} else {
    window.ProjectConfigClient = ProjectConfigClient;
}
