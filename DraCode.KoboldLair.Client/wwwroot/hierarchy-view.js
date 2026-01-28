import { ApiClient } from './api.js';

export class HierarchyView {
    constructor(api) {
        this.api = api;
    }

    async render() {
        try {
            const data = await this.api.getHierarchy();

            return `
                <div class="card">
                    <div class="card-header">
                        <h2 class="card-title">System Hierarchy</h2>
                    </div>
                    <div class="card-body">
                        <div class="tree">
                            ${this.renderDragon(data.hierarchy.dragon)}
                            ${data.hierarchy.projects.map(p => this.renderProject(p)).join('')}
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
        return `
            <div class="tree-node">
                <div class="tree-node-content">
                    <span class="tree-node-icon">${project.icon}</span>
                    <span>${project.name}</span>
                    <span class="badge badge-${this.getStatusBadge(project.status)}">${project.status}</span>
                </div>
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
}
