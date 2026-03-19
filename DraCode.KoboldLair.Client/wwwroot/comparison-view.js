import CONFIG from './config.js';

export class ComparisonView {
    constructor(api) {
        this.api = api;
        this._refreshTimer = null;
        this._selectedProject = null;
        this._sortField = 'status';
        this._sortAsc = true;
    }

    async render() {
        if (!this.api.isConnected()) {
            return '<div class="card"><div class="card-body"><p>Connect to a server to view comparisons.</p></div></div>';
        }

        try {
            const projects = await this.api.getProjects();
            return this._buildHtml(projects, null);
        } catch (e) {
            return `<div class="card"><div class="card-body"><p>Error: ${e.message}</p></div></div>`;
        }
    }

    _buildHtml(projects, comparisonData) {
        const projectOptions = (projects || [])
            .map(p => `<option value="${p.id}" ${this._selectedProject === p.id ? 'selected' : ''}>${p.name}</option>`)
            .join('');

        return `
            <div class="comparison-view">
                <h2 style="margin-bottom: 16px;">Task Comparison</h2>

                <div class="card" style="margin-bottom: 16px;">
                    <div class="card-body" style="display: flex; gap: 12px; align-items: center;">
                        <label style="font-size: 13px; font-weight: 600;">Project:</label>
                        <select id="comparison-project" style="background: var(--bg-tertiary); color: var(--text-primary); border: 1px solid var(--border-color); border-radius: 4px; padding: 6px 10px; font-size: 13px; flex: 1; max-width: 400px;">
                            <option value="">Select a project...</option>
                            ${projectOptions}
                        </select>
                        <button id="comparison-load" class="btn" style="background: var(--accent-primary); color: white; border: none; padding: 6px 16px; border-radius: 4px; font-size: 13px; cursor: pointer;">Load</button>
                    </div>
                </div>

                <div id="comparison-content">
                    ${comparisonData ? this._buildComparisonContent(comparisonData) : '<div class="card"><div class="card-body"><p style="color: var(--text-secondary); font-size: 13px;">Select a project and click Load to compare task executions.</p></div></div>'}
                </div>
            </div>
        `;
    }

    _buildComparisonContent(data) {
        if (!data.tasks || data.tasks.length === 0) {
            return '<div class="card"><div class="card-body"><p>No tasks found for this project.</p></div></div>';
        }

        const tasks = this._sortTasks(data.tasks);

        // Summary stats
        const done = tasks.filter(t => t.status === 'Done').length;
        const failed = tasks.filter(t => t.status === 'Failed').length;
        const working = tasks.filter(t => t.status === 'Working').length;
        const totalIterations = tasks.reduce((s, t) => s + (t.plan?.totalIterations ?? 0), 0);
        const totalTokens = tasks.reduce((s, t) => s + (t.plan?.totalTokens ?? 0), 0);
        const avgSuccess = tasks.filter(t => t.plan).length > 0
            ? (tasks.filter(t => t.plan).reduce((s, t) => s + (t.plan?.successRate ?? 0), 0) / tasks.filter(t => t.plan).length)
            : 0;

        // Count by agent type
        const agentCounts = {};
        tasks.forEach(t => { agentCounts[t.agentType] = (agentCounts[t.agentType] || 0) + 1; });

        // Count by provider
        const providerCounts = {};
        tasks.forEach(t => { providerCounts[t.provider] = (providerCounts[t.provider] || 0) + 1; });

        return `
            <div class="stats-grid" style="margin-bottom: 16px;">
                <div class="stat-card">
                    <span class="stat-card-icon">TSK</span>
                    <span class="stat-card-value">${tasks.length}</span>
                    <span class="stat-card-label">Total Tasks</span>
                </div>
                <div class="stat-card">
                    <span class="stat-card-value" style="color: var(--accent-success);">${done}</span>
                    <span class="stat-card-label">Done / ${failed} Failed / ${working} Working</span>
                </div>
                <div class="stat-card">
                    <span class="stat-card-icon">ITR</span>
                    <span class="stat-card-value">${totalIterations.toLocaleString()}</span>
                    <span class="stat-card-label">Total Iterations</span>
                </div>
                <div class="stat-card">
                    <span class="stat-card-icon">TKN</span>
                    <span class="stat-card-value">${totalTokens.toLocaleString()}</span>
                    <span class="stat-card-label">Total Tokens</span>
                </div>
            </div>

            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px;">
                <div class="card">
                    <div class="card-header"><span class="card-title">By Agent Type</span></div>
                    <div class="card-body">
                        ${Object.entries(agentCounts).sort((a, b) => b[1] - a[1]).map(([type, count]) =>
                            `<div style="display: flex; justify-content: space-between; font-size: 13px; padding: 2px 0;">
                                <span class="badge badge-info">${type}</span>
                                <span>${count} task${count !== 1 ? 's' : ''}</span>
                            </div>`
                        ).join('')}
                    </div>
                </div>
                <div class="card">
                    <div class="card-header"><span class="card-title">By Provider</span></div>
                    <div class="card-body">
                        ${Object.entries(providerCounts).sort((a, b) => b[1] - a[1]).map(([prov, count]) =>
                            `<div style="display: flex; justify-content: space-between; font-size: 13px; padding: 2px 0;">
                                <span class="badge badge-info">${prov}</span>
                                <span>${count} task${count !== 1 ? 's' : ''}</span>
                            </div>`
                        ).join('')}
                    </div>
                </div>
            </div>

            <div class="card">
                <div class="card-header">
                    <span class="card-title">Task Execution Details</span>
                    <span style="font-size: 11px; color: var(--text-secondary);">Click column headers to sort</span>
                </div>
                <div class="card-body">
                    <div class="files-table-container">
                        <table class="files-table" id="comparison-table">
                            <thead><tr>
                                <th style="cursor:pointer;" data-sort="description">Task</th>
                                <th style="cursor:pointer;" data-sort="status">Status</th>
                                <th style="cursor:pointer;" data-sort="agentType">Agent</th>
                                <th style="cursor:pointer;" data-sort="provider">Provider</th>
                                <th style="cursor:pointer;" data-sort="steps">Steps</th>
                                <th style="cursor:pointer;" data-sort="iterations">Iterations</th>
                                <th style="cursor:pointer;" data-sort="tokens">Tokens</th>
                                <th style="cursor:pointer;" data-sort="duration">Duration</th>
                                <th style="cursor:pointer;" data-sort="successRate">Success</th>
                                <th>Files</th>
                            </tr></thead>
                            <tbody>
                                ${tasks.map(t => this._buildTaskRow(t)).join('')}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
    }

    _buildTaskRow(t) {
        const statusBadge = {
            'Done': 'badge-success',
            'Failed': 'badge-error',
            'Working': 'badge-warning',
            'Unassigned': 'badge-info',
            'BlockedByFailure': 'badge-error',
            'NotInitialized': 'badge-info'
        }[t.status] || 'badge-info';

        const p = t.plan;
        const steps = p ? `${p.completedSteps}/${p.totalSteps}` : '-';
        const iterations = p ? p.totalIterations.toLocaleString() : '-';
        const tokens = p ? p.totalTokens.toLocaleString() : '-';
        const duration = p && p.durationSeconds > 0 ? this._formatDuration(p.durationSeconds) : '-';
        const success = p ? `${Math.round(p.successRate)}%` : '-';
        const files = t.outputFileCount > 0 ? t.outputFileCount : '-';

        const retryBadge = t.retryCount > 0 ? ` <span class="badge badge-warning">${t.retryCount}x</span>` : '';
        const escalationBadge = p && p.escalationCount > 0 ? ` <span class="badge badge-error">${p.escalationCount} esc</span>` : '';

        return `
            <tr class="file-row" title="${this._escapeHtml(t.description)}">
                <td style="max-width: 250px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${this._escapeHtml(t.description)}</td>
                <td><span class="badge ${statusBadge}">${t.status}</span>${retryBadge}</td>
                <td><span class="badge badge-info">${t.agentType}</span></td>
                <td>${t.provider}</td>
                <td>${steps}</td>
                <td>${iterations}${escalationBadge}</td>
                <td>${tokens}</td>
                <td>${duration}</td>
                <td>${success}</td>
                <td>${files}${t.commitSha ? ` <span style="font-size:10px;color:var(--text-secondary);">${t.commitSha}</span>` : ''}</td>
            </tr>
        `;
    }

    _sortTasks(tasks) {
        const field = this._sortField;
        const asc = this._sortAsc;

        return [...tasks].sort((a, b) => {
            let va, vb;
            switch (field) {
                case 'status': va = a.status; vb = b.status; break;
                case 'agentType': va = a.agentType; vb = b.agentType; break;
                case 'provider': va = a.provider; vb = b.provider; break;
                case 'steps': va = a.plan?.completedSteps ?? -1; vb = b.plan?.completedSteps ?? -1; break;
                case 'iterations': va = a.plan?.totalIterations ?? -1; vb = b.plan?.totalIterations ?? -1; break;
                case 'tokens': va = a.plan?.totalTokens ?? -1; vb = b.plan?.totalTokens ?? -1; break;
                case 'duration': va = a.plan?.durationSeconds ?? -1; vb = b.plan?.durationSeconds ?? -1; break;
                case 'successRate': va = a.plan?.successRate ?? -1; vb = b.plan?.successRate ?? -1; break;
                default: va = a.description; vb = b.description;
            }
            if (typeof va === 'string') return asc ? va.localeCompare(vb) : vb.localeCompare(va);
            return asc ? va - vb : vb - va;
        });
    }

    _formatDuration(seconds) {
        if (seconds < 60) return `${Math.round(seconds)}s`;
        if (seconds < 3600) return `${Math.round(seconds / 60)}m`;
        return `${(seconds / 3600).toFixed(1)}h`;
    }

    _escapeHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    attachEventListeners(container) {
        const loadBtn = container.querySelector('#comparison-load');
        const select = container.querySelector('#comparison-project');

        if (loadBtn && select) {
            loadBtn.addEventListener('click', async () => {
                const projectId = select.value;
                if (!projectId) return;
                this._selectedProject = projectId;
                await this._loadComparison(container, projectId);
            });
        }

        // Delegate sort clicks on table headers
        container.addEventListener('click', async (e) => {
            const th = e.target.closest('th[data-sort]');
            if (th) {
                const field = th.dataset.sort;
                if (this._sortField === field) {
                    this._sortAsc = !this._sortAsc;
                } else {
                    this._sortField = field;
                    this._sortAsc = true;
                }
                // Re-render table with new sort
                if (this._lastData) {
                    const contentEl = container.querySelector('#comparison-content');
                    if (contentEl) {
                        contentEl.innerHTML = this._buildComparisonContent(this._lastData);
                    }
                }
            }
        });
    }

    async _loadComparison(container, projectId) {
        const contentEl = container.querySelector('#comparison-content');
        if (!contentEl) return;

        contentEl.innerHTML = '<div class="card"><div class="card-body"><p>Loading...</p></div></div>';

        try {
            const data = await this.api.getComparison(projectId);
            this._lastData = data;
            contentEl.innerHTML = this._buildComparisonContent(data);
        } catch (e) {
            contentEl.innerHTML = `<div class="card"><div class="card-body"><p>Error: ${e.message}</p></div></div>`;
        }
    }

    onMount() {
        // No auto-refresh for comparison view (user-triggered loads)
    }

    onUnmount() {
        if (this._refreshTimer) {
            clearInterval(this._refreshTimer);
            this._refreshTimer = null;
        }
        this._lastData = null;
    }

    async refresh() {
        // Manual refresh not needed for comparison view
    }
}
