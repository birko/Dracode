import { ApiClient } from './api.js';
import CONFIG from './config.js';

export class ProjectsView {
    constructor(api, onRefresh) {
        this.api = api;
        this.onRefresh = onRefresh;
        this.agentStats = new Map(); // Cache agent statistics
        this._refreshTimer = null;
    }

    async render() {
        try {
            const projects = await this.api.getProjects();

            // Fetch agent statistics for all projects in parallel
            const statsPromises = projects.map(p => 
                this.api.getProjectAgents(p.id)
                    .then(stats => ({ id: p.id, stats }))
                    .catch(err => {
                        console.warn(`Failed to fetch agent stats for ${p.name}:`, err);
                        return { id: p.id, stats: null };
                    })
            );
            
            const statsResults = await Promise.all(statsPromises);
            statsResults.forEach(result => {
                if (result.stats) {
                    this.agentStats.set(result.id, result.stats.agents);
                }
            });

            return `
                <div class="card">
                    <div class="card-header">
                        <h2 class="card-title">Projects</h2>
                    </div>
                    <div class="card-body">
                        ${projects.length > 0 ? `
                            <div class="list">
                                ${projects.map(p => this.renderProject(p)).join('')}
                            </div>
                        ` : `
                            <div class="empty-state">
                                <div class="empty-state-icon">📁</div>
                                <div>No projects found</div>
                            </div>
                        `}
                    </div>
                </div>
            `;
        } catch (error) {
            console.error('Projects error:', error);
            return `<div class="empty-state">
                <div class="empty-state-icon">⚠️</div>
                <div class="empty-state-title">Failed to load projects</div>
                <div class="empty-state-error">${error.message || error.toString()}</div>
            </div>`;
        }
    }

    renderProject(project) {
        const isFailed = project.status === 'Failed';
        const agentStats = this.agentStats.get(project.id);
        
        return `
            <div class="list-item">
                <div class="list-item-main">
                    <span class="list-item-icon">📁</span>
                    <div class="list-item-content">
                        <div class="list-item-title">${project.name}</div>
                        <div class="list-item-subtitle">
                            Created: ${new Date(project.createdAt).toLocaleString()}
                            ${project.analyzedAt ? ` • Analyzed: ${new Date(project.analyzedAt).toLocaleString()}` : ''}
                        </div>
                        ${project.errorMessage ? `
                            <div class="list-item-subtitle" style="color: var(--accent-error)">
                                ⚠️ Error: ${project.errorMessage}
                            </div>
                        ` : ''}
                        ${agentStats ? `
                            <div class="list-item-subtitle agent-stats">
                                🐲 Wyverns: ${agentStats.wyverns} • 
                                🐉 Drakes: ${agentStats.drakes} • 
                                👺 Kobolds: ${agentStats.kobolds}
                            </div>
                        ` : ''}
                        ${project.specificationPath ? `
                            <div class="list-item-subtitle">
                                Spec: ${project.specificationPath}
                            </div>
                        ` : ''}
                        ${project.outputPath ? `
                            <div class="list-item-subtitle">
                                Output: ${project.outputPath}
                            </div>
                        ` : ''}
                        ${project.taskFiles && project.taskFiles.length > 0 ? `
                            <div class="list-item-subtitle">
                                Tasks: ${project.taskFiles.length} files
                            </div>
                        ` : ''}
                    </div>
                </div>
                <div class="list-item-actions">
                    ${isFailed ? `
                        <button class="btn btn-sm btn-warning retry-btn" data-project-id="${project.id}" title="Retry analysis">🔄 Retry</button>
                    ` : ''}
                    ${project.maxParallelKobolds ? `
                        <span class="badge badge-info">Max Kobolds: ${project.maxParallelKobolds}</span>
                    ` : ''}
                    <span class="badge badge-${this.getStatusBadge(project.status)}">${project.status}</span>
                </div>
            </div>
        `;
    }

    getStatusBadge(status) {
        const map = {
            'Analyzed': 'success',
            'Processing': 'warning',
            'Failed': 'error',
            'Created': 'info'
        };
        return map[status] || 'info';
    }

    attachEventListeners(container) {
        const retryButtons = container.querySelectorAll('.retry-btn');
        retryButtons.forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.stopPropagation();
                const projectId = btn.dataset.projectId;
                btn.disabled = true;
                btn.textContent = '⏳ Retrying...';
                try {
                    await this.api.retryAnalysis(projectId);
                    if (this.onRefresh) {
                        this.onRefresh();
                    }
                } catch (error) {
                    console.error('Retry failed:', error);
                    alert(`Retry failed: ${error.message}`);
                    btn.disabled = false;
                    btn.textContent = '🔄 Retry';
                }
            });
        });
    }

    onMount() {
        this._refreshTimer = setInterval(() => this._autoRefresh(), CONFIG.refreshInterval);
    }

    onUnmount() {
        if (this._refreshTimer) {
            clearInterval(this._refreshTimer);
            this._refreshTimer = null;
        }
    }

    async refresh() {
        if (!this.api.isConnected()) return;
        await this._autoRefresh();
    }
}

    async _autoRefresh() {
        if (!this.api.isConnected()) return;
        try {
            const projects = await this.api.getProjects();

            // Update each project's status and agent stats in-place
            for (const p of projects) {
                // Update status badge
                const badge = document.querySelector(`.list-item [data-project-id="${p.id}"]`)
                    ?.closest('.list-item')
                    ?.querySelector('.badge:last-child');

                // Find project list item by retry button or by matching title
                const items = document.querySelectorAll('.list-item');
                for (const item of items) {
                    const title = item.querySelector('.list-item-title');
                    if (title && title.textContent === p.name) {
                        // Update status badge
                        const statusBadge = item.querySelector('.list-item-actions .badge:last-child');
                        if (statusBadge) {
                            const newClass = `badge badge-${this.getStatusBadge(p.status)}`;
                            if (statusBadge.className !== newClass || statusBadge.textContent !== p.status) {
                                statusBadge.className = newClass;
                                statusBadge.textContent = p.status;
                            }
                        }

                        // Update agent stats
                        try {
                            const agentData = await this.api.getProjectAgents(p.id);
                            if (agentData?.agents) {
                                const statsEl = item.querySelector('.agent-stats');
                                const newStats = `🐲 Wyverns: ${agentData.agents.wyverns} • 🐉 Drakes: ${agentData.agents.drakes} • 👺 Kobolds: ${agentData.agents.kobolds}`;
                                if (statsEl) {
                                    if (statsEl.textContent.trim() !== newStats.trim()) {
                                        statsEl.innerHTML = newStats;
                                    }
                                } else {
                                    // Add agent stats if not present yet
                                    const subtitle = item.querySelector('.list-item-content');
                                    if (subtitle) {
                                        const div = document.createElement('div');
                                        div.className = 'list-item-subtitle agent-stats';
                                        div.innerHTML = newStats;
                                        subtitle.appendChild(div);
                                    }
                                }
                            }
                        } catch (e) { /* silent */ }

                        break;
                    }
                }
            }
        } catch (e) {
            // Silent
        }
    }
}
