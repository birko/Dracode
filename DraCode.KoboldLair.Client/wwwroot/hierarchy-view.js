import { ApiClient } from './api.js';
import CONFIG from './config.js';

export class HierarchyView {
    constructor(api, onRefresh) {
        this.api = api;
        this.onRefresh = onRefresh;
        this._refreshTimer = null;
        this._lastTreeHtml = '';
    }

    async render() {
        try {
            const data = await this.api.getHierarchy();
            // Store projects with error messages for lookup
            this.projectsData = {};
            if (data.projects) {
                for (const p of data.projects) {
                    this.projectsData[p.id] = p;
                }
            }

            this._lastTreeHtml = `
                ${this.renderDragon(data.hierarchy.dragon)}
                ${data.hierarchy.projects.map(p => this.renderProject(p)).join('')}
            `;

            return `
                <div class="card">
                    <div class="card-header">
                        <h2 class="card-title">System Hierarchy</h2>
                    </div>
                    <div class="card-body">
                        <div class="tree">
                            ${this._lastTreeHtml}
                        </div>
                    </div>
                </div>
            `;
        } catch (error) {
            console.error('Hierarchy error:', error);
            return `<div class="empty-state">
                <div class="empty-state-icon">⚠️</div>
                <div class="empty-state-title">Failed to load hierarchy</div>
                <div class="empty-state-error">${error.message || error.toString()}</div>
            </div>`;
        }
    }

    renderDragon(dragon) {
        return `
            <div class="tree-node">
                <div class="tree-node-content">
                    <span class="tree-node-icon">${dragon.icon}</span>
                    <span>${dragon.name}</span>
                    <span class="badge badge-${dragon.status === 'active' ? 'success' : 'info'}">${dragon.status}</span>
                    ${dragon.activeSessions > 0 ? `<span class="badge badge-info">${dragon.activeSessions} sessions</span>` : ''}
                </div>
            </div>
        `;
    }

    renderProject(project) {
        // Get full project data with error message
        const projectData = this.projectsData?.[project.id] || {};
        const isFailed = project.status === 'failed';
        const errorMessage = projectData.errorMessage;

        return `
            <div class="tree-node">
                <div class="tree-node-content">
                    <span class="tree-node-icon">${project.icon}</span>
                    <span>${project.name}</span>
                    <span class="badge badge-${this.getStatusBadge(project.status)}">${project.status}</span>
                    ${isFailed ? `<button class="btn btn-sm btn-warning retry-btn" data-project-id="${project.id}" title="Retry analysis">🔄 Retry</button>` : ''}
                </div>
                ${isFailed && errorMessage ? `
                    <div class="tree-node-error" style="color: var(--accent-error); margin-left: 24px; font-size: 0.85em; padding: 4px 0;">
                        ⚠️ ${errorMessage}
                    </div>
                ` : ''}
                ${project.wyrm ? `
                    <div class="tree-node-children">
                        <div class="tree-node-content">
                            <span class="tree-node-icon">${project.wyrm.icon}</span>
                            <span>${project.wyrm.name}</span>
                            <span class="badge badge-${project.wyrm.status === 'active' ? 'success' : 'warning'}">${project.wyrm.status}</span>
                            ${project.wyrm.totalTasks ? `<span class="badge badge-info">${project.wyrm.totalTasks} tasks</span>` : ''}
                        </div>
                    </div>
                ` : ''}
            </div>
        `;
    }

    getStatusBadge(status) {
        const map = {
            'analyzed': 'success',
            'processing': 'warning',
            'failed': 'error',
            'created': 'info'
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

    async _autoRefresh() {
        if (!this.api.isConnected()) return;
        try {
            const data = await this.api.getHierarchy();
            if (data.projects) {
                for (const p of data.projects) {
                    this.projectsData[p.id] = p;
                }
            }

            // Build new tree HTML and compare
            const newTreeHtml = `
                ${this.renderDragon(data.hierarchy.dragon)}
                ${data.hierarchy.projects.map(p => this.renderProject(p)).join('')}
            `;

            if (newTreeHtml !== this._lastTreeHtml) {
                this._lastTreeHtml = newTreeHtml;
                const tree = document.querySelector('.tree');
                if (tree) {
                    tree.innerHTML = newTreeHtml;
                    // Re-attach retry button listeners
                    this.attachEventListeners(tree);
                }
            }
        } catch (e) {
            // Silent
        }
    }
}
