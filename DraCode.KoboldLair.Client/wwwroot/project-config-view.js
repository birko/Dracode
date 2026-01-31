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
        const wyrmStatus = config.wyrmEnabled ? 'success' : 'error';
        const wyvernStatus = config.wyvernEnabled ? 'success' : 'error';
        const drakeStatus = config.drakeEnabled ? 'success' : 'error';
        const koboldStatus = config.koboldEnabled ? 'success' : 'error';

        return `
            <div class="list-item config-item" data-project-id="${config.projectId}">
                <div class="list-item-main">
                    <span class="list-item-icon">Folder</span>
                    <div class="list-item-content">
                        <div class="list-item-title">${config.projectName || config.projectId}</div>
                        <div class="list-item-subtitle">
                            Limits: ${config.maxParallelKobolds || 1} Kobolds,
                            ${config.maxParallelDrakes || 1} Drakes,
                            ${config.maxParallelWyrms || 1} Wyrms,
                            ${config.maxParallelWyverns || 1} Wyverns
                        </div>
                        <div class="config-agents">
                            <span class="badge badge-${wyrmStatus}" title="Wyrm: ${config.wyrmProvider || 'default'}">Wyrm</span>
                            <span class="badge badge-${wyvernStatus}" title="Wyvern: ${config.wyvernProvider || 'default'}">Wyvern</span>
                            <span class="badge badge-${drakeStatus}" title="Drake: ${config.drakeProvider || 'default'}">Drake</span>
                            <span class="badge badge-${koboldStatus}" title="Kobold: ${config.koboldProvider || 'default'}">Kobold</span>
                        </div>
                    </div>
                </div>
                <div class="list-item-actions">
                    <button class="btn btn-primary btn-sm config-edit-btn" data-project-id="${config.projectId}">Edit</button>
                    <button class="btn btn-secondary btn-sm config-delete-btn" data-project-id="${config.projectId}">Delete</button>
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
        const config = this.configs.find(c => c.projectId === projectId);
        if (!config) return;

        const modalContainer = document.getElementById('configModal');
        modalContainer.innerHTML = this.renderEditModal(config);

        this.setupModalEventListeners();
    }

    renderEditModal(config) {
        return `
            <div class="modal" id="editConfigModal">
                <div class="modal-content modal-large">
                    <div class="modal-header">
                        <h3>Edit Configuration: ${config.projectName || config.projectId}</h3>
                        <button class="modal-close" id="closeModal">X</button>
                    </div>
                    <div class="modal-body">
                        <div class="config-section">
                            <h4 class="config-section-title">Resource Limits</h4>
                            <div class="config-grid">
                                <div class="form-group">
                                    <label for="maxParallelKobolds">Max Parallel Kobolds</label>
                                    <input type="number" class="form-control" id="maxParallelKobolds"
                                           value="${config.maxParallelKobolds || 1}" min="1" max="10">
                                </div>
                                <div class="form-group">
                                    <label for="maxParallelDrakes">Max Parallel Drakes</label>
                                    <input type="number" class="form-control" id="maxParallelDrakes"
                                           value="${config.maxParallelDrakes || 1}" min="1" max="5">
                                </div>
                                <div class="form-group">
                                    <label for="maxParallelWyrms">Max Parallel Wyrms</label>
                                    <input type="number" class="form-control" id="maxParallelWyrms"
                                           value="${config.maxParallelWyrms || 1}" min="1" max="5">
                                </div>
                                <div class="form-group">
                                    <label for="maxParallelWyverns">Max Parallel Wyverns</label>
                                    <input type="number" class="form-control" id="maxParallelWyverns"
                                           value="${config.maxParallelWyverns || 1}" min="1" max="5">
                                </div>
                            </div>
                        </div>

                        <div class="config-section">
                            <h4 class="config-section-title">Wyrm Configuration</h4>
                            ${this.renderAgentConfig('wyrm', config.wyrmProvider, config.wyrmModel, config.wyrmEnabled)}
                        </div>

                        <div class="config-section">
                            <h4 class="config-section-title">Wyvern Configuration</h4>
                            ${this.renderAgentConfig('wyvern', config.wyvernProvider, config.wyvernModel, config.wyvernEnabled)}
                        </div>

                        <div class="config-section">
                            <h4 class="config-section-title">Drake Configuration</h4>
                            ${this.renderAgentConfig('drake', config.drakeProvider, config.drakeModel, config.drakeEnabled)}
                        </div>

                        <div class="config-section">
                            <h4 class="config-section-title">Kobold Configuration</h4>
                            ${this.renderAgentConfig('kobold', config.koboldProvider, config.koboldModel, config.koboldEnabled)}
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

    renderAgentConfig(agentType, provider, model, enabled) {
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
                           value="${model || ''}" placeholder="Use provider default">
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

    async saveConfig() {
        if (!this.selectedProjectId) return;

        const config = {
            projectId: this.selectedProjectId,
            maxParallelKobolds: parseInt(document.getElementById('maxParallelKobolds').value) || 1,
            maxParallelDrakes: parseInt(document.getElementById('maxParallelDrakes').value) || 1,
            maxParallelWyrms: parseInt(document.getElementById('maxParallelWyrms').value) || 1,
            maxParallelWyverns: parseInt(document.getElementById('maxParallelWyverns').value) || 1,
            wyrmProvider: document.getElementById('wyrmProvider').value || null,
            wyrmModel: document.getElementById('wyrmModel').value || null,
            wyrmEnabled: document.getElementById('wyrmEnabled').checked,
            wyvernProvider: document.getElementById('wyvernProvider').value || null,
            wyvernModel: document.getElementById('wyvernModel').value || null,
            wyvernEnabled: document.getElementById('wyvernEnabled').checked,
            drakeProvider: document.getElementById('drakeProvider').value || null,
            drakeModel: document.getElementById('drakeModel').value || null,
            drakeEnabled: document.getElementById('drakeEnabled').checked,
            koboldProvider: document.getElementById('koboldProvider').value || null,
            koboldModel: document.getElementById('koboldModel').value || null,
            koboldEnabled: document.getElementById('koboldEnabled').checked
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
