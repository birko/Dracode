import { ApiClient } from './api.js';
import CONFIG from './config.js';

export class DashboardView {
    constructor(api) {
        this.api = api;
        this._refreshTimer = null;
        this._lastProjectsHtml = '';
    }

    async render() {
        try {
            const stats = await this.api.getStats();
            const projects = await this.api.getProjects();
            this._lastProjectsHtml = this._renderProjectsList(projects);

            return `
                <div class="stats-grid">
                    <div class="stat-card">
                        <div class="stat-card-icon">🐉</div>
                        <div class="stat-card-value" data-stat="dragonSessions">${stats.dragon.activeSessions}</div>
                        <div class="stat-card-label">Dragon Sessions</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-icon">📁</div>
                        <div class="stat-card-value" data-stat="projects">${stats.projects.totalProjects || 0}</div>
                        <div class="stat-card-label">Projects</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-icon">🐲</div>
                        <div class="stat-card-value" data-stat="wyrms">${stats.wyrms}</div>
                        <div class="stat-card-label">Wyrms</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-icon">🔥</div>
                        <div class="stat-card-value" data-stat="drakes">${stats.drakes}</div>
                        <div class="stat-card-label">Drakes</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-icon">⚒️</div>
                        <div class="stat-card-value" data-stat="kobolds">${stats.koboldsWorking}</div>
                        <div class="stat-card-label">Kobolds Working</div>
                    </div>
                </div>

                <div class="card">
                    <div class="card-header">
                        <h2 class="card-title">Recent Projects</h2>
                    </div>
                    <div class="card-body" id="dashboardProjects">
                        ${this._lastProjectsHtml}
                    </div>
                </div>
            `;
        } catch (error) {
            console.error('Dashboard error:', error);
            return `<div class="empty-state">
                <div class="empty-state-icon">⚠️</div>
                <div class="empty-state-title">Failed to load dashboard data</div>
                <div class="empty-state-error">${error.message || error.toString()}</div>
            </div>`;
        }
    }

    _renderProjectsList(projects) {
        if (projects.length === 0) {
            return `<div class="empty-state">
                <div class="empty-state-icon">📁</div>
                <div>No projects yet</div>
            </div>`;
        }
        return `<div class="list">
            ${projects.slice(0, 5).map(p => `
                <div class="list-item">
                    <div class="list-item-main">
                        <span class="list-item-icon">📁</span>
                        <div class="list-item-content">
                            <div class="list-item-title">${p.name}</div>
                            <div class="list-item-subtitle">Created: ${new Date(p.createdAt).toLocaleString()}</div>
                        </div>
                    </div>
                    <span class="badge badge-${this.getStatusBadgeClass(p.status)}">${p.status}</span>
                </div>
            `).join('')}
        </div>`;
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

    async _autoRefresh() {
        if (!this.api.isConnected()) return;
        try {
            const [stats, projects] = await Promise.all([
                this.api.getStats(),
                this.api.getProjects()
            ]);

            // Update stat values in-place (no flicker)
            const updates = {
                dragonSessions: stats.dragon.activeSessions,
                projects: stats.projects.totalProjects || 0,
                wyrms: stats.wyrms,
                drakes: stats.drakes,
                kobolds: stats.koboldsWorking
            };
            for (const [key, value] of Object.entries(updates)) {
                const el = document.querySelector(`[data-stat="${key}"]`);
                if (el && el.textContent !== String(value)) {
                    el.textContent = value;
                    el.classList.add('stat-updated');
                    setTimeout(() => el.classList.remove('stat-updated'), 600);
                }
            }

            // Update projects list only if changed
            const newProjectsHtml = this._renderProjectsList(projects);
            if (newProjectsHtml !== this._lastProjectsHtml) {
                this._lastProjectsHtml = newProjectsHtml;
                const container = document.getElementById('dashboardProjects');
                if (container) container.innerHTML = newProjectsHtml;
            }
        } catch (e) {
            // Silent - don't disrupt UI on background refresh failure
        }
    }

    getStatusBadgeClass(status) {
        const statusMap = {
            'Analyzed': 'success',
            'Processing': 'info',
            'Failed': 'error',
            'Created': 'warning'
        };
        return statusMap[status] || 'info';
    }
}
