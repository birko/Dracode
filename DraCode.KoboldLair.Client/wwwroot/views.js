import { ApiClient } from './api.js';

export class DashboardView {
    constructor(api) {
        this.api = api;
    }

    async render() {
        try {
            const stats = await this.api.getStats();
            const projects = await this.api.getProjects();

            return `
                <div class="stats-grid">
                    <div class="stat-card">
                        <div class="stat-card-icon">🐉</div>
                        <div class="stat-card-value">${stats.dragon.activeSessions}</div>
                        <div class="stat-card-label">Dragon Sessions</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-icon">📁</div>
                        <div class="stat-card-value">${stats.projects.totalProjects || 0}</div>
                        <div class="stat-card-label">Projects</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-icon">🐲</div>
                        <div class="stat-card-value">${stats.wyrms}</div>
                        <div class="stat-card-label">Wyrms</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-icon">🔥</div>
                        <div class="stat-card-value">${stats.drakes}</div>
                        <div class="stat-card-label">Drakes</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-card-icon">⚒️</div>
                        <div class="stat-card-value">${stats.koboldsWorking}</div>
                        <div class="stat-card-label">Kobolds Working</div>
                    </div>
                </div>

                <div class="card">
                    <div class="card-header">
                        <h2 class="card-title">Recent Projects</h2>
                    </div>
                    <div class="card-body">
                        ${projects.length > 0 ? `
                            <div class="list">
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
                            </div>
                        ` : `
                            <div class="empty-state">
                                <div class="empty-state-icon">📁</div>
                                <div>No projects yet</div>
                            </div>
                        `}
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
