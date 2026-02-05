import { ApiClient } from './api.js';

export class ProjectConfigView {
    constructor(api) {
        this.api = api;
        this.configs = [];
        this.defaults = {};
        this.providers = [];
        this.selectedProjectId = null;
    }

    async render() {
        try {
            const [configData, providerData] = await Promise.all([
                this.api.getAllProjectConfigs(),
                this.api.getProviders()
            ]);

            this.configs = configData.projects || [];
            this.defaults = configData.defaults || {};
            this.providers = providerData.providers || [];

            return `
                <div class="card">
                    <div class="card-header">
                        <h2 class="card-title">Project Configurations</h2>
                    </div>
                    <div class="card-body">
                        <div class="config-defaults">
                            <div class="config-defaults-title">Default Limits</div>
                            <div class="config-defaults-grid">
                                <span class="badge badge-info">Kobolds: ${this.defaults.maxParallelKobolds || 1}</span>
                                <span class="badge badge-info">Drakes: ${this.defaults.maxParallelDrakes || 1}</span>
                                <span class="badge badge-info">Wyrms: ${this.defaults.maxParallelWyrms || 1}</span>
                                <span class="badge badge-info">Wyverns: ${this.defaults.maxParallelWyverns || 1}</span>
                            </div>
                        </div>
                        ${this.configs.length > 0 ? `
                            <div class="list">
                                ${this.configs.map(c => this.renderConfigItem(c)).join('')}
                            </div>
                        ` : `
                            <div class="empty-state">
                                <div class="empty-state-icon">Settings</div>
                                <div>No project configurations found</div>
                                <div class="empty-state-subtitle">Create a project via Dragon to add configurations</div>
                            </div>
                        `}
                    </div>
                </div>
                <div id="configModal"></div>
            `;
        } catch (error) {
            console.error('Project configs error:', error);
            return `<div class="empty-state">
                <div class="empty-state-icon">Warning</div>
                <div class="empty-state-title">Failed to load configurations</div>
                <div class="empty-state-error">${error.message || error.toString()}</div>
            </div>`;
        }
    }

    renderConfigItem(config) {
        // Use nested structure: config.project, config.agents
        const projectId = config.project?.id || '';
        const projectName = config.project?.name || projectId;
        const agents = config.agents || {};

        const wyrmStatus = agents.wyrm?.enabled ? 'success' : 'error';
        const wyvernStatus = agents.wyvern?.enabled ? 'success' : 'error';
        const drakeStatus = agents.drake?.enabled ? 'success' : 'error';
        const plannerStatus = agents.koboldPlanner?.enabled ? 'success' : 'error';
        const koboldStatus = agents.kobold?.enabled ? 'success' : 'error';

        return `
            <div class="list-item config-item" data-project-id="${projectId}">
                <div class="list-item-main">
                    <span class="list-item-icon">Folder</span>
                    <div class="list-item-content">
                        <div class="list-item-title">${projectName}</div>
                        <div class="list-item-subtitle">
                            Limits: ${agents.kobold?.maxParallel || 1} Kobolds,
                            ${agents.drake?.maxParallel || 1} Drakes,
                            ${agents.wyrm?.maxParallel || 1} Wyrms,
                            ${agents.wyvern?.maxParallel || 1} Wyverns
                        </div>
                        <div class="config-agents">
                            <span class="badge badge-${wyrmStatus}" title="Wyrm: ${agents.wyrm?.provider || 'default'}">Wyrm</span>
                            <span class="badge badge-${wyvernStatus}" title="Wyvern: ${agents.wyvern?.provider || 'default'}">Wyvern</span>
                            <span class="badge badge-${drakeStatus}" title="Drake: ${agents.drake?.provider || 'default'}">Drake</span>
                            <span class="badge badge-${plannerStatus}" title="Planner: ${agents.koboldPlanner?.provider || 'default'}">Planner</span>
                            <span class="badge badge-${koboldStatus}" title="Kobold: ${agents.kobold?.provider || 'default'}">Kobold</span>
                        </div>
                    </div>
                </div>
                <div class="list-item-actions">
                    <button class="btn btn-primary btn-sm config-edit-btn" data-project-id="${projectId}">Edit</button>
                    <button class="btn btn-secondary btn-sm config-delete-btn" data-project-id="${projectId}">Delete</button>
                </div>
            </div>
        `;
    }

    onMount() {
        document.querySelectorAll('.config-edit-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const projectId = e.target.dataset.projectId;
                this.openEditModal(projectId);
            });
        });

        document.querySelectorAll('.config-delete-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const projectId = e.target.dataset.projectId;
                this.deleteConfig(projectId);
            });
        });
    }

    async openEditModal(projectId) {
        this.selectedProjectId = projectId;
        const config = this.configs.find(c => c.project?.id === projectId);
        if (!config) return;

        const modalContainer = document.getElementById('configModal');
        modalContainer.innerHTML = this.renderEditModal(config);

        this.setupModalEventListeners();
    }

    renderEditModal(config) {
        const projectId = config.project?.id || '';
        const projectName = config.project?.name || projectId;
        const agents = config.agents || {};

        return `
            <div class="modal" id="editConfigModal">
                <div class="modal-content modal-large">
                    <div class="modal-header">
                        <h3>Edit Configuration: ${projectName}</h3>
                        <button class="modal-close" id="closeModal">X</button>
                    </div>
                    <div class="modal-body">
                        <div class="config-section">
                            <h4 class="config-section-title">Wyrm Configuration</h4>
                            ${this.renderAgentConfig('wyrm', agents.wyrm)}
                        </div>

                        <div class="config-section">
                            <h4 class="config-section-title">Wyvern Configuration</h4>
                            ${this.renderAgentConfig('wyvern', agents.wyvern)}
                        </div>

                        <div class="config-section">
                            <h4 class="config-section-title">Drake Configuration</h4>
                            ${this.renderAgentConfig('drake', agents.drake)}
                        </div>

                        <div class="config-section">
                            <h4 class="config-section-title">Kobold Planner Configuration</h4>
                            ${this.renderAgentConfig('koboldPlanner', agents.koboldPlanner)}
                        </div>

                        <div class="config-section">
                            <h4 class="config-section-title">Kobold Configuration</h4>
                            ${this.renderAgentConfig('kobold', agents.kobold)}
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button class="btn btn-secondary" id="cancelConfig">Cancel</button>
                        <button class="btn btn-primary" id="saveConfig">Save Changes</button>
                    </div>
                </div>
            </div>
        `;
    }

    renderAgentConfig(agentType, agentConfig) {
        const config = agentConfig || {};
        const provider = config.provider || '';
        const model = config.model || '';
        const enabled = config.enabled !== false;
        const maxParallel = config.maxParallel || 1;
        const timeout = config.timeout || 0;

        const providerOptions = this.providers
            .filter(p => p.isEnabled || p.name === provider)
            .map(p => `<option value="${p.name}" ${p.name === provider ? 'selected' : ''}>${p.displayName}</option>`)
            .join('');

        return `
            <div class="agent-config-row">
                <div class="form-group agent-toggle">
                    <label class="toggle-label">
                        <input type="checkbox" id="${agentType}Enabled" ${enabled ? 'checked' : ''}>
                        <span class="toggle-text">Enabled</span>
                    </label>
                </div>
                <div class="form-group">
                    <label for="${agentType}Provider">Provider</label>
                    <select class="form-select" id="${agentType}Provider">
                        <option value="">Use Default</option>
                        ${providerOptions}
                    </select>
                </div>
                <div class="form-group">
                    <label for="${agentType}Model">Model Override</label>
                    <input type="text" class="form-control" id="${agentType}Model"
                           value="${model}" placeholder="Use provider default">
                </div>
                <div class="form-group">
                    <label for="${agentType}MaxParallel">Max Parallel</label>
                    <input type="number" class="form-control" id="${agentType}MaxParallel"
                           value="${maxParallel}" min="1" max="10">
                </div>
                <div class="form-group">
                    <label for="${agentType}Timeout">Timeout (sec)</label>
                    <input type="number" class="form-control" id="${agentType}Timeout"
                           value="${timeout}" min="0" placeholder="0 = no timeout">
                </div>
            </div>
        `;
    }

    setupModalEventListeners() {
        const modal = document.getElementById('editConfigModal');
        const closeBtn = document.getElementById('closeModal');
        const cancelBtn = document.getElementById('cancelConfig');
        const saveBtn = document.getElementById('saveConfig');

        const closeModal = () => {
            modal.remove();
            this.selectedProjectId = null;
        };

        closeBtn.addEventListener('click', closeModal);
        cancelBtn.addEventListener('click', closeModal);
        modal.addEventListener('click', (e) => {
            if (e.target === modal) closeModal();
        });

        saveBtn.addEventListener('click', () => this.saveConfig());
    }

    getAgentConfigFromForm(agentType) {
        return {
            enabled: document.getElementById(`${agentType}Enabled`).checked,
            provider: document.getElementById(`${agentType}Provider`).value || null,
            model: document.getElementById(`${agentType}Model`).value || null,
            maxParallel: parseInt(document.getElementById(`${agentType}MaxParallel`).value) || 1,
            timeout: parseInt(document.getElementById(`${agentType}Timeout`).value) || 0
        };
    }

    async saveConfig() {
        if (!this.selectedProjectId) return;

        // Find existing config to preserve project name and other data
        const existingConfig = this.configs.find(c => c.project?.id === this.selectedProjectId);
        const projectName = existingConfig?.project?.name || this.selectedProjectId;

        // Build nested config structure matching server's ProjectConfig model
        const config = {
            projectId: this.selectedProjectId,
            project: {
                id: this.selectedProjectId,
                name: projectName
            },
            agents: {
                wyrm: this.getAgentConfigFromForm('wyrm'),
                wyvern: this.getAgentConfigFromForm('wyvern'),
                drake: this.getAgentConfigFromForm('drake'),
                koboldPlanner: this.getAgentConfigFromForm('koboldPlanner'),
                kobold: this.getAgentConfigFromForm('kobold')
            },
            security: existingConfig?.security || {
                allowedExternalPaths: [],
                sandboxMode: 'workspace'
            },
            metadata: {
                lastUpdated: new Date().toISOString(),
                createdAt: existingConfig?.metadata?.createdAt || new Date().toISOString()
            }
        };

        try {
            await this.api.updateProjectConfigFull(this.selectedProjectId, config);
            this.showNotification('Configuration saved successfully', 'success');

            const modal = document.getElementById('editConfigModal');
            if (modal) modal.remove();
            this.selectedProjectId = null;

            // Refresh the view
            const content = document.getElementById('content');
            if (content) {
                content.innerHTML = await this.render();
                this.onMount();
            }
        } catch (error) {
            console.error('Failed to save config:', error);
            this.showNotification('Failed to save configuration: ' + error.message, 'error');
        }
    }

    async deleteConfig(projectId) {
        if (!confirm(`Delete configuration for project ${projectId}?`)) {
            return;
        }

        try {
            await this.api.deleteProjectConfig(projectId);
            this.showNotification('Configuration deleted', 'success');

            // Refresh the view
            const content = document.getElementById('content');
            if (content) {
                content.innerHTML = await this.render();
                this.onMount();
            }
        } catch (error) {
            console.error('Failed to delete config:', error);
            this.showNotification('Failed to delete configuration: ' + error.message, 'error');
        }
    }

    showNotification(message, type = 'info') {
        const existing = document.querySelector('.notification');
        if (existing) existing.remove();

        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.textContent = message;
        document.body.appendChild(notification);

        requestAnimationFrame(() => notification.classList.add('show'));

        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => notification.remove(), 150);
        }, 3000);
    }

    onUnmount() {
        const modal = document.getElementById('editConfigModal');
        if (modal) modal.remove();
    }
}
