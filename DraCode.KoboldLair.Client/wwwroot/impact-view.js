import { ApiClient } from './api.js';
import CONFIG from './config.js';

export class ImpactView {
    constructor(api) {
        this.api = api;
        this.currentProject = null;
        this.impactData = null;
        this._refreshTimer = null;
        this._selectedProjectId = null;
    }

    async render() {
        const projects = await this.api.getProjects();

        let projectOptions = projects.map(p =>
            `<option value="${p.id}">${p.name}</option>`
        ).join('');

        return `
            <div class="impact-view">
                <div class="impact-header">
                    <div class="impact-controls">
                        <label>
                            Project:
                            <select id="projectSelect" class="select">
                                <option value="">Select a project...</option>
                                ${projectOptions}
                            </select>
                        </label>
                        <button id="refreshImpact" class="btn btn-secondary" title="Refresh Impact Data">
                            <span>🔄</span> Refresh
                        </button>
                    </div>
                </div>

                <div id="impactContent" class="impact-content">
                    <div class="empty-state">
                        <div class="empty-state-icon">🎯</div>
                        <div class="empty-state-title">Select a project</div>
                        <div class="empty-state-description">
                            Choose a project to view feature-to-code impact analysis
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    attachEventListeners(container) {
        const projectSelect = container.querySelector('#projectSelect');
        const refreshBtn = container.querySelector('#refreshImpact');

        projectSelect?.addEventListener('change', async () => {
            const projectId = projectSelect.value;
            this._selectedProjectId = projectId || null;
            if (projectId) {
                await this.loadImpactData(projectId);
            } else {
                this.showEmptyState();
            }
        });

        refreshBtn?.addEventListener('click', async () => {
            const projectId = projectSelect.value;
            if (projectId) {
                await this.loadImpactData(projectId);
            }
        });
    }

    onMount() {
        this._refreshTimer = setInterval(() => this._autoRefresh(), CONFIG.refreshInterval);
    }

    async loadImpactData(projectId) {
        const content = document.getElementById('impactContent');
        if (!content) return;

        content.innerHTML = '<div class="loading">⏳ Loading impact data...</div>';

        try {
            const summary = await this.api.getImplementationSummary(projectId);

            if (!summary) {
                content.innerHTML = `
                    <div class="empty-state">
                        <div class="empty-state-icon">📭</div>
                        <div class="empty-state-title">No impact data available</div>
                        <div class="empty-state-description">
                            This project has no implementation summary yet.
                            Complete some tasks to generate impact tracking data.
                        </div>
                    </div>
                `;
                return;
            }

            this.currentProject = summary;
            this.renderImpactSummary(summary, content);
        } catch (error) {
            console.error('Error loading impact data:', error);
            content.innerHTML = `
                <div class="error-state">
                    <div class="error-icon">⚠️</div>
                    <div class="error-title">Failed to load impact data</div>
                    <div class="error-message">${error.message || 'Unknown error'}</div>
                </div>
            `;
        }
    }

    renderImpactSummary(summary, container) {
        const progressPercent = summary.overallProgress ? summary.overallProgress.toFixed(0) : '0';
        const completedTasks = summary.completedTasks || 0;
        const totalTasks = summary.totalTasks || 0;
        const featureImplementations = summary.featureImplementations || {};
        const fileImpacts = summary.fileImpacts || {};
        const areaSummaries = summary.areaSummaries || [];

        let html = `
            <div class="impact-summary">
                <div class="impact-overview">
                    <div class="overview-stats">
                        <div class="stat-card">
                            <div class="stat-icon">📊</div>
                            <div class="stat-content">
                                <div class="stat-label">Progress</div>
                                <div class="stat-value">${progressPercent}%</div>
                                <div class="stat-detail">${completedTasks}/${totalTasks} tasks</div>
                            </div>
                        </div>
                        <div class="stat-card">
                            <div class="stat-icon">🎯</div>
                            <div class="stat-content">
                                <div class="stat-label">Features</div>
                                <div class="stat-value">${Object.keys(featureImplementations).length}</div>
                                <div class="stat-detail">In specification</div>
                            </div>
                        </div>
                        <div class="stat-card">
                            <div class="stat-icon">📁</div>
                            <div class="stat-content">
                                <div class="stat-label">Files</div>
                                <div class="stat-value">${Object.keys(fileImpacts).length}</div>
                                <div class="stat-detail">Tracked</div>
                            </div>
                        </div>
                        <div class="stat-card">
                            <div class="stat-icon">📋</div>
                            <div class="stat-content">
                                <div class="stat-label">Spec Version</div>
                                <div class="stat-value">v${summary.specificationVersion || 1}</div>
                                <div class="stat-detail">${(summary.specificationContentHash || '').substring(0, 8)}...</div>
                            </div>
                        </div>
                    </div>
                </div>

                ${this.renderFeaturesSection(summary, featureImplementations, fileImpacts)}
                ${this.renderFilesSection(summary, fileImpacts)}
                ${this.renderAreasSection(summary, areaSummaries)}
            </div>
        `;

        container.innerHTML = html;
    }

    renderFeaturesSection(summary, featureImplementations, fileImpacts) {
        const features = Object.values(featureImplementations || {});

        if (features.length === 0) {
            return '';
        }

        const statusIcons = {
            'NotStarted': '⏳',
            'InProgress': '🔨',
            'Completed': '✅',
            'Failed': '❌',
            'Blocked': '🚫'
        };

        let featuresHtml = `
            <div class="impact-section">
                <h2 class="section-title">🎯 Features</h2>
                <div class="features-grid">
        `;

        features.forEach(feature => {
            const progress = (feature.progressPercentage || 0).toFixed(0);
            const statusIcon = statusIcons[feature.status] || '❓';
            const filesCreated = Object.keys(feature.filesCreated || {});
            const filesModified = Object.keys(feature.filesModified || {});

            featuresHtml += `
                <div class="feature-card" data-feature-id="${feature.featureId}">
                    <div class="feature-header">
                        <span class="feature-status">${statusIcon}</span>
                        <h3 class="feature-name">${feature.featureName || feature.featureId}</h3>
                        <span class="feature-progress">${progress}%</span>
                    </div>
                    <div class="feature-stats">
                        <div class="feature-stat">
                            <span class="stat-label">Steps:</span>
                            <span class="stat-value">${feature.completedSteps || 0}/${feature.totalSteps || 0}</span>
                        </div>
                        <div class="feature-stat">
                            <span class="stat-label">Files:</span>
                            <span class="stat-value">${filesCreated.length + filesModified.length}</span>
                        </div>
                    </div>
                    ${filesCreated.length > 0 || filesModified.length > 0 ? `
                        <div class="feature-files">
                            <div class="files-label">Files:</div>
                            ${filesCreated.slice(0, 5).map(f =>
                                `<span class="file-badge file-created">+ ${this.shortenPath(f)}</span>`
                            ).join('')}
                            ${filesModified.slice(0, 5).map(f =>
                                `<span class="file-badge file-modified">~ ${this.shortenPath(f)}</span>`
                            ).join('')}
                            ${(filesCreated.length + filesModified.length) > 10 ? '<span class="file-badge">+ more</span>' : ''}
                        </div>
                    ` : ''}
                </div>
            `;
        });

        featuresHtml += `
                </div>
            </div>
        `;

        return featuresHtml;
    }

    renderFilesSection(summary, fileImpacts) {
        const files = Object.values(fileImpacts || {});

        if (files.length === 0) {
            return '';
        }

        let filesHtml = `
            <div class="impact-section">
                <h2 class="section-title">📁 File Impact Matrix</h2>
                <div class="files-table-container">
                    <table class="files-table">
                        <thead>
                            <tr>
                                <th>File</th>
                                <th>Type</th>
                                <th>Features</th>
                                <th>Created By</th>
                                <th>Modified By</th>
                            </tr>
                        </thead>
                        <tbody>
        `;

        files.forEach(file => {
            const features = Array.from(file.relatedFeatureIds || []);
            const createdTasks = Object.keys(file.createdByTasks || {});
            const modifiedTasks = Object.keys(file.modifiedByTasks || {});

            filesHtml += `
                <tr class="file-row" data-file="${this.escapeHtml(file.filePath)}">
                    <td class="file-path">
                        <code>${this.escapeHtml(file.filePath)}</code>
                    </td>
                    <td class="file-category">${file.category || 'Unknown'}</td>
                    <td class="file-features">
                        ${features.length > 0 ? features.map(f =>
                            `<span class="feature-badge">${this.escapeHtml(f)}</span>`
                        ).join('') : '<span class="text-muted">None</span>'}
                    </td>
                    <td class="file-created-by">
                        ${createdTasks.length > 0 ? createdTasks.map(t =>
                            `<span class="task-badge">${this.escapeHtml(t.substring(0, 8))}</span>`
                        ).join(' ') : '<span class="text-muted">-</span>'}
                    </td>
                    <td class="file-modified-by">
                        ${modifiedTasks.length > 0 ? modifiedTasks.map(t =>
                            `<span class="task-badge">${this.escapeHtml(t.substring(0, 8))}</span>`
                        ).join(' ') : '<span class="text-muted">-</span>'}
                    </td>
                </tr>
            `;
        });

        filesHtml += `
                        </tbody>
                    </table>
                </div>
            </div>
        `;

        return filesHtml;
    }

    renderAreasSection(summary, areaSummaries) {
        if (!areaSummaries || areaSummaries.length === 0) {
            return '';
        }

        let areasHtml = `
            <div class="impact-section">
                <h2 class="section-title">📋 Area Progress</h2>
                <div class="areas-grid">
        `;

        areaSummaries.forEach(area => {
            const progress = (area.progressPercentage || 0).toFixed(0);

            areasHtml += `
                <div class="area-card">
                    <h3 class="area-name">${area.areaName}</h3>
                    <div class="area-progress-bar">
                        <div class="area-progress-fill" style="width: ${progress}%"></div>
                    </div>
                    <div class="area-stats">
                        <span class="area-stat">${area.completedTasks || 0}/${area.totalTasks || 0} tasks</span>
                        <span class="area-stat">${progress}% complete</span>
                    </div>
                </div>
            `;
        });

        areasHtml += `
                </div>
            </div>
        `;

        return areasHtml;
    }

    showEmptyState() {
        const content = document.getElementById('impactContent');
        if (!content) return;

        content.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">🎯</div>
                <div class="empty-state-title">Select a project</div>
                <div class="empty-state-description">
                    Choose a project to view feature-to-code impact analysis
                </div>
            </div>
        `;
    }

    shortenPath(path) {
        if (path.length > 30) {
            const parts = path.split('/');
            if (parts.length > 2) {
                return `.../${parts[parts.length - 2]}/${parts[parts.length - 1]}`;
            }
            return path.substring(0, 27) + '...';
        }
        return path;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    onUnmount() {
        if (this._refreshTimer) {
            clearInterval(this._refreshTimer);
            this._refreshTimer = null;
        }
        this._selectedProjectId = null;
    }

    async _autoRefresh() {
        if (!this.api.isConnected() || !this._selectedProjectId) return;
        try {
            const summary = await this.api.getImplementationSummary(this._selectedProjectId);
            if (!summary) return;

            // Compare key metrics to decide if update needed
            const prev = this.currentProject;
            const changed = !prev
                || prev.overallProgress !== summary.overallProgress
                || prev.completedTasks !== summary.completedTasks
                || prev.totalTasks !== summary.totalTasks
                || Object.keys(summary.fileImpacts || {}).length !== Object.keys(prev.fileImpacts || {}).length;

            if (changed) {
                this.currentProject = summary;
                const content = document.getElementById('impactContent');
                if (content) {
                    this.renderImpactSummary(summary, content);
                }
            }
        } catch (e) {
            // Silent
        }
    }
}
