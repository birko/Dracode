/**
 * Projects View Module - Project Management
 */
export class ProjectsView {
    constructor(app) {
        this.app = app;
        this.projects = [];
        this.selectedProject = null;
    }

    async render() {
        const container = document.getElementById('viewContainer');
        container.innerHTML = `
            <section class="projects-view">
                <div class="section-header">
                    <h2>⚙️ Project Management</h2>
                    <button id="refreshProjectsBtn" class="btn btn-secondary">🔄 Refresh</button>
                </div>

                <div id="projectsContainer" class="projects-container">
                    <p class="loading-message">Loading projects...</p>
                </div>

                <!-- Project Configuration Modal -->
                <div id="projectConfigModal" class="modal" style="display: none;">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h3>Project Configuration</h3>
                            <button class="modal-close">&times;</button>
                        </div>
                        <div class="modal-body">
                            <div id="projectConfigContent"></div>
                        </div>
                    </div>
                </div>
            </section>
        `;

        this.setupEventListeners();
        await this.loadProjects();
    }

    setupEventListeners() {
        const refreshBtn = document.getElementById('refreshProjectsBtn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => this.loadProjects());
        }

        // Close modal
        document.addEventListener('click', (e) => {
            if (e.target.classList.contains('modal-close') || e.target.classList.contains('modal')) {
                this.closeModal();
            }
        });
    }

    async loadProjects() {
        try {
            const response = await fetch('/api/projects');
            this.projects = await response.json();
            this.renderProjects();
        } catch (error) {
            console.error('Failed to load projects:', error);
            this.renderError();
        }
    }

    renderProjects() {
        const container = document.getElementById('projectsContainer');
        if (!container) return;

        if (this.projects.length === 0) {
            container.innerHTML = `
                <p class="placeholder-info">No projects yet. Create your first project in the Dragon view.</p>
            `;
            return;
        }

        container.innerHTML = this.projects.map(project => `
            <div class="project-card">
                <div class="project-header">
                    <h3>📁 ${this.escapeHtml(project.name)}</h3>
                    <span class="project-status ${project.status?.toLowerCase() || 'new'}">
                        ${project.status || 'New'}
                    </span>
                </div>
                <div class="project-details">
                    <div class="project-info">
                        <strong>ID:</strong> ${this.escapeHtml(project.id)}
                    </div>
                    <div class="project-info">
                        <strong>Created:</strong> ${new Date(project.createdAt).toLocaleString()}
                    </div>
                    ${project.outputPath ? `
                        <div class="project-info">
                            <strong>Output Path:</strong> ${this.escapeHtml(project.outputPath)}
                        </div>
                    ` : ''}
                    ${project.wyrmId ? `
                        <div class="project-info">
                            <strong>Wyrm:</strong> ${this.escapeHtml(project.wyrmId)}
                        </div>
                    ` : ''}
                    ${project.maxParallelKobolds ? `
                        <div class="project-info">
                            <strong>Max Parallel Kobolds:</strong> ${project.maxParallelKobolds}
                        </div>
                    ` : ''}
                    ${project.errorMessage ? `
                        <div class="project-info error-info">
                            <strong>Error:</strong> ${this.escapeHtml(project.errorMessage)}
                        </div>
                    ` : ''}
                    <div class="agent-status-container" id="agentStatus-${project.id}">
                        <div class="loading-indicator">Loading agent status...</div>
                    </div>
                </div>
                <div class="project-actions">
                    <button class="btn btn-secondary config-btn" data-project-id="${project.id}">
                        ⚙️ Configure
                    </button>
                    <button class="btn btn-secondary providers-btn" data-project-id="${project.id}">
                        🔧 Providers
                    </button>
                </div>
            </div>
        `).join('');

        // Load agent status for each project
        this.projects.forEach(project => {
            this.loadAgentStatus(project.id);
        });

        // Add event listeners
        container.querySelectorAll('.config-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const projectId = e.target.dataset.projectId;
                this.showProjectConfig(projectId);
            });
        });

        container.querySelectorAll('.providers-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const projectId = e.target.dataset.projectId;
                this.showProjectProviders(projectId);
            });
        });
    }

    async loadAgentStatus(projectId) {
        const container = document.getElementById(`agentStatus-${projectId}`);
        if (!container) return;

        try {
            const agentTypes = ['wyrm', 'drake', 'kobold'];
            const statusPromises = agentTypes.map(type =>
                fetch(`/api/projects/${projectId}/agents/${type}/status`)
                    .then(r => r.json())
                    .then(status => ({ type, ...status }))
                    .catch(() => ({ type, enabled: false, error: true }))
            );

            const statuses = await Promise.all(statusPromises);
            
            const statusHtml = statuses.map(status => {
                const icons = { wyrm: '🐲', drake: '🦎', kobold: '⚙️' };
                const icon = icons[status.type];
                const statusIndicator = status.error ? '❓' :
                                       status.enabled ? '✓' : '✗';
                const statusClass = status.error ? 'status-unknown' :
                                   status.enabled ? 'status-active' : 'status-inactive';
                
                return `
                    <div class="agent-status-item ${statusClass}">
                        <span>${icon} ${status.type.charAt(0).toUpperCase() + status.type.slice(1)}</span>
                        <span class="status-badge">${statusIndicator}</span>
                    </div>
                `;
            }).join('');

            container.innerHTML = `
                <div class="agent-status-label">Agents:</div>
                <div class="agent-status-list">${statusHtml}</div>
            `;
        } catch (error) {
            console.error('Failed to load agent status:', error);
            container.innerHTML = '<div class="error-text">Failed to load agent status</div>';
        }
    }

    async showProjectConfig(projectId) {
        const project = this.projects.find(p => p.id === projectId);
        if (!project) return;

        try {
            const response = await fetch(`/api/projects/${projectId}/config`);
            const config = await response.json();

            const modal = document.getElementById('projectConfigModal');
            const content = document.getElementById('projectConfigContent');
            
            content.innerHTML = `
                <h4>${this.escapeHtml(project.name)}</h4>
                <div class="form-group">
                    <label for="maxParallelKobolds">Max Parallel Kobolds:</label>
                    <input type="number" id="maxParallelKobolds" class="form-input" 
                           value="${config.maxParallelKobolds || 3}" min="1" max="20">
                    <p class="help-text">Maximum number of Kobolds that can work on this project simultaneously</p>
                </div>
                <button class="btn btn-primary" id="saveConfigBtn">Save Configuration</button>
            `;

            modal.style.display = 'flex';

            // Save button
            document.getElementById('saveConfigBtn')?.addEventListener('click', async () => {
                const maxParallel = parseInt(document.getElementById('maxParallelKobolds').value);
                await this.saveProjectConfig(projectId, maxParallel);
            });
        } catch (error) {
            console.error('Failed to load project config:', error);
            this.app.ui.addLog('error', 'Failed to load project configuration');
        }
    }

    async showProjectProviders(projectId) {
        const project = this.projects.find(p => p.id === projectId);
        if (!project) return;

        try {
            const response = await fetch(`/api/projects/${projectId}/providers`);
            const data = await response.json();

            const modal = document.getElementById('projectConfigModal');
            const content = document.getElementById('projectConfigContent');
            
            content.innerHTML = `
                <h4>${this.escapeHtml(project.name)} - Provider Configuration</h4>
                
                <div class="form-group">
                    <label>Wyrm Provider:</label>
                    <select id="wyrmProvider" class="form-select">
                        ${data.availableProviders
                            .filter(p => p.compatibleAgents.includes('wyrm') || p.compatibleAgents.includes('all'))
                            .map(p => `
                                <option value="${p.name}" ${data.providers.wyrmProvider === p.name ? 'selected' : ''}>
                                    ${this.escapeHtml(p.displayName)} (${this.escapeHtml(p.defaultModel)})
                                </option>
                            `).join('')}
                    </select>
                    <label class="checkbox-label">
                        <input type="checkbox" id="wyrmEnabled" ${data.providers.wyrmEnabled ? 'checked' : ''}>
                        Enable Wyrm for this project
                    </label>
                </div>

                <div class="form-group">
                    <label>Drake Provider:</label>
                    <select id="drakeProvider" class="form-select">
                        ${data.availableProviders
                            .filter(p => p.compatibleAgents.includes('drake') || p.compatibleAgents.includes('all'))
                            .map(p => `
                                <option value="${p.name}" ${data.providers.drakeProvider === p.name ? 'selected' : ''}>
                                    ${this.escapeHtml(p.displayName)} (${this.escapeHtml(p.defaultModel)})
                                </option>
                            `).join('')}
                    </select>
                    <label class="checkbox-label">
                        <input type="checkbox" id="drakeEnabled" ${data.providers.drakeEnabled ? 'checked' : ''}>
                        Enable Drake for this project
                    </label>
                </div>

                <div class="form-group">
                    <label>Kobold Provider:</label>
                    <select id="koboldProvider" class="form-select">
                        ${data.availableProviders
                            .filter(p => p.compatibleAgents.includes('kobold') || p.compatibleAgents.includes('all'))
                            .map(p => `
                                <option value="${p.name}" ${data.providers.koboldProvider === p.name ? 'selected' : ''}>
                                    ${this.escapeHtml(p.displayName)} (${this.escapeHtml(p.defaultModel)})
                                </option>
                            `).join('')}
                    </select>
                    <label class="checkbox-label">
                        <input type="checkbox" id="koboldEnabled" ${data.providers.koboldEnabled ? 'checked' : ''}>
                        Enable Kobold for this project
                    </label>
                </div>

                <button class="btn btn-primary" id="saveProvidersBtn">Save Provider Settings</button>
            `;

            modal.style.display = 'flex';

            // Save button
            document.getElementById('saveProvidersBtn')?.addEventListener('click', async () => {
                await this.saveProjectProviders(projectId);
            });
        } catch (error) {
            console.error('Failed to load project providers:', error);
            this.app.ui.addLog('error', 'Failed to load project providers');
        }
    }

    async saveProjectConfig(projectId, maxParallelKobolds) {
        try {
            const response = await fetch(`/api/projects/${projectId}/config`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ maxParallelKobolds })
            });

            const result = await response.json();
            
            if (result.success) {
                this.app.ui.addLog('success', 'Project configuration updated');
                this.closeModal();
                await this.loadProjects();
            } else {
                this.app.ui.addLog('error', `Failed: ${result.message}`);
            }
        } catch (error) {
            console.error('Failed to save config:', error);
            this.app.ui.addLog('error', 'Failed to save configuration');
        }
    }

    async saveProjectProviders(projectId) {
        try {
            const updates = [
                { agentType: 'wyrm', provider: document.getElementById('wyrmProvider').value, enabled: document.getElementById('wyrmEnabled').checked },
                { agentType: 'drake', provider: document.getElementById('drakeProvider').value, enabled: document.getElementById('drakeEnabled').checked },
                { agentType: 'kobold', provider: document.getElementById('koboldProvider').value, enabled: document.getElementById('koboldEnabled').checked }
            ];

            for (const update of updates) {
                // Update provider
                await fetch(`/api/projects/${projectId}/providers`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        agentType: update.agentType,
                        providerName: update.provider,
                        modelOverride: null
                    })
                });

                // Update enabled status
                await fetch(`/api/projects/${projectId}/agents/${update.agentType}/toggle`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ enabled: update.enabled })
                });
            }

            this.app.ui.addLog('success', 'Provider settings updated');
            this.closeModal();
            await this.loadProjects();
        } catch (error) {
            console.error('Failed to save providers:', error);
            this.app.ui.addLog('error', 'Failed to save provider settings');
        }
    }

    closeModal() {
        const modal = document.getElementById('projectConfigModal');
        if (modal) {
            modal.style.display = 'none';
        }
    }

    renderError() {
        const container = document.getElementById('projectsContainer');
        if (container) {
            container.innerHTML = `
                <p class="error-message">Failed to load projects. Please check your connection.</p>
            `;
        }
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    cleanup() {
        this.closeModal();
    }
}

