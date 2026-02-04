import { ApiClient } from './api.js';

export class ProjectsView {
    constructor(api, onRefresh) {
        this.api = api;
        this.onRefresh = onRefresh;
    }

    async render() {
        try {
            const projects = await this.api.getProjects();

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
}
