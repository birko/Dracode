import CONFIG from './config.js';

export class MetricsView {
    constructor(api) {
        this.api = api;
        this._refreshTimer = null;
        this._lastData = null;
    }

    async render() {
        if (!this.api.isConnected()) {
            return '<div class="card"><div class="card-body"><p>Connect to a server to view metrics.</p></div></div>';
        }

        try {
            const data = await this.api.getMetrics(24);
            this._lastData = data;
            return this._buildHtml(data);
        } catch (e) {
            return `<div class="card"><div class="card-body"><p>Error loading metrics: ${e.message}</p></div></div>`;
        }
    }

    _buildHtml(data) {
        const s = data.summary || {};
        const providers = data.byProvider || [];
        const projects = data.byProject || [];
        const daily = data.dailyBreakdown || [];
        const budget = data.budget;
        const rateLimits = data.rateLimits || [];

        return `
            <div class="metrics-view">
                <h2 style="margin-bottom: 16px;">Agent Performance Metrics</h2>

                <div class="stats-grid" id="metrics-summary">
                    <div class="stat-card">
                        <span class="stat-card-icon">API</span>
                        <span class="stat-card-value" data-stat="total-requests">${this._fmt(s.totalRequests)}</span>
                        <span class="stat-card-label">Requests (24h)</span>
                    </div>
                    <div class="stat-card">
                        <span class="stat-card-icon">TKN</span>
                        <span class="stat-card-value" data-stat="total-tokens">${this._fmt(s.totalTokens)}</span>
                        <span class="stat-card-label">Total Tokens</span>
                    </div>
                    <div class="stat-card">
                        <span class="stat-card-icon">AVG</span>
                        <span class="stat-card-value" data-stat="avg-tokens">${this._fmt(s.avgTokensPerRequest)}</span>
                        <span class="stat-card-label">Avg Tokens/Req</span>
                    </div>
                    <div class="stat-card">
                        <span class="stat-card-icon">USD</span>
                        <span class="stat-card-value" data-stat="total-cost">$${this._cost(s.totalCost)}</span>
                        <span class="stat-card-label">Est. Cost (24h)</span>
                    </div>
                </div>

                ${budget && budget.budgetType !== 'none' ? this._buildBudgetCard(budget) : ''}

                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px;">
                    ${this._buildProviderTable(providers)}
                    ${this._buildDailyTable(daily)}
                </div>

                ${projects.length > 0 ? this._buildProjectTable(projects) : ''}
                ${rateLimits.length > 0 ? this._buildRateLimitTable(rateLimits) : ''}
            </div>
        `;
    }

    _buildBudgetCard(budget) {
        const pct = budget.budgetLimit > 0 ? Math.min(100, (budget.currentSpend / budget.budgetLimit) * 100) : 0;
        const barColor = !budget.isWithinBudget ? 'var(--accent-error)' : budget.isWarning ? 'var(--accent-warning)' : 'var(--accent-success)';
        const label = !budget.isWithinBudget ? 'EXCEEDED' : budget.isWarning ? 'Warning' : 'OK';
        return `
            <div class="card" style="margin-bottom: 16px;">
                <div class="card-header">
                    <span class="card-title">Budget Status (${budget.budgetType})</span>
                    <span class="badge ${!budget.isWithinBudget ? 'badge-error' : budget.isWarning ? 'badge-warning' : 'badge-success'}">${label}</span>
                </div>
                <div class="card-body">
                    <div style="display: flex; justify-content: space-between; font-size: 13px; margin-bottom: 4px;">
                        <span>$${this._cost(budget.currentSpend)} spent</span>
                        <span>$${this._cost(budget.budgetLimit)} limit</span>
                    </div>
                    <div style="background: var(--bg-tertiary); border-radius: 4px; height: 8px; overflow: hidden;">
                        <div style="background: ${barColor}; height: 100%; width: ${pct}%; transition: width 0.3s;"></div>
                    </div>
                </div>
            </div>
        `;
    }

    _buildProviderTable(providers) {
        if (providers.length === 0) {
            return '<div class="card"><div class="card-header"><span class="card-title">By Provider</span></div><div class="card-body"><p style="color: var(--text-secondary); font-size: 13px;">No usage data yet.</p></div></div>';
        }

        const rows = providers.map(p => `
            <tr class="file-row">
                <td><strong>${p.provider}</strong></td>
                <td>${this._fmt(p.requests)}</td>
                <td>${this._fmt(p.promptTokens)}</td>
                <td>${this._fmt(p.completionTokens)}</td>
                <td>${this._fmt(p.totalTokens)}</td>
                <td>$${this._cost(p.costUsd)}</td>
            </tr>
        `).join('');

        return `
            <div class="card">
                <div class="card-header"><span class="card-title">By Provider (24h)</span></div>
                <div class="card-body">
                    <div class="files-table-container">
                        <table class="files-table">
                            <thead><tr>
                                <th>Provider</th><th>Requests</th><th>Prompt</th><th>Completion</th><th>Total</th><th>Cost</th>
                            </tr></thead>
                            <tbody>${rows}</tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
    }

    _buildDailyTable(daily) {
        if (daily.length === 0) {
            return '<div class="card"><div class="card-header"><span class="card-title">Daily Trend</span></div><div class="card-body"><p style="color: var(--text-secondary); font-size: 13px;">No data yet.</p></div></div>';
        }

        const maxTokens = Math.max(...daily.map(d => d.tokens), 1);
        const rows = daily.map(d => {
            const barWidth = Math.max(1, (d.tokens / maxTokens) * 100);
            return `
                <tr class="file-row">
                    <td>${d.date}</td>
                    <td>${this._fmt(d.requests)}</td>
                    <td>
                        <div style="display: flex; align-items: center; gap: 8px;">
                            <div style="background: var(--accent-primary); height: 6px; border-radius: 3px; width: ${barWidth}%; min-width: 2px;"></div>
                            <span style="font-size: 11px; white-space: nowrap;">${this._fmt(d.tokens)}</span>
                        </div>
                    </td>
                    <td>$${this._cost(d.costUsd)}</td>
                </tr>
            `;
        }).join('');

        return `
            <div class="card">
                <div class="card-header"><span class="card-title">Daily Trend (7 days)</span></div>
                <div class="card-body">
                    <div class="files-table-container">
                        <table class="files-table">
                            <thead><tr><th>Date</th><th>Requests</th><th>Tokens</th><th>Cost</th></tr></thead>
                            <tbody>${rows}</tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
    }

    _buildProjectTable(projects) {
        const rows = projects.map(p => `
            <tr class="file-row">
                <td><strong>${p.projectName}</strong></td>
                <td>${this._fmt(p.requests)}</td>
                <td>${this._fmt(p.promptTokens)}</td>
                <td>${this._fmt(p.completionTokens)}</td>
                <td>$${this._cost(p.costUsd)}</td>
            </tr>
        `).join('');

        return `
            <div class="card" style="margin-top: 16px;">
                <div class="card-header"><span class="card-title">By Project (24h)</span></div>
                <div class="card-body">
                    <div class="files-table-container">
                        <table class="files-table">
                            <thead><tr>
                                <th>Project</th><th>Requests</th><th>Prompt</th><th>Completion</th><th>Cost</th>
                            </tr></thead>
                            <tbody>${rows}</tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
    }

    _buildRateLimitTable(limits) {
        const rows = limits.map(r => {
            const rpmPct = r.rpmLimit > 0 ? Math.round((r.requestsThisMinute / r.rpmLimit) * 100) : 0;
            const rpmBadge = rpmPct >= 90 ? 'badge-error' : rpmPct >= 70 ? 'badge-warning' : 'badge-success';
            return `
                <tr class="file-row">
                    <td><strong>${r.provider}</strong></td>
                    <td>${r.rpmLimit > 0 ? `${r.requestsThisMinute}/${r.rpmLimit}` : '-'} ${r.rpmLimit > 0 ? `<span class="badge ${rpmBadge}">${rpmPct}%</span>` : ''}</td>
                    <td>${r.tpmLimit > 0 ? `${this._fmt(r.tokensThisMinute)}/${this._fmt(r.tpmLimit)}` : '-'}</td>
                    <td>${r.rpdLimit > 0 ? `${this._fmt(r.requestsToday)}/${this._fmt(r.rpdLimit)}` : '-'}</td>
                    <td>${r.tpdLimit > 0 ? `${this._fmt(r.tokensToday)}/${this._fmt(r.tpdLimit)}` : '-'}</td>
                </tr>
            `;
        }).join('');

        return `
            <div class="card" style="margin-top: 16px;">
                <div class="card-header"><span class="card-title">Rate Limits</span></div>
                <div class="card-body">
                    <div class="files-table-container">
                        <table class="files-table">
                            <thead><tr>
                                <th>Provider</th><th>RPM</th><th>TPM</th><th>RPD</th><th>TPD</th>
                            </tr></thead>
                            <tbody>${rows}</tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
    }

    _fmt(n) {
        if (n == null || n === undefined) return '0';
        return Number(n).toLocaleString();
    }

    _cost(n) {
        if (n == null || n === undefined) return '0.00';
        return Number(n).toFixed(n >= 1 ? 2 : 4);
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
            const data = await this.api.getMetrics(24);
            if (!data || !data.summary) return;

            const s = data.summary;
            this._updateStat('total-requests', this._fmt(s.totalRequests));
            this._updateStat('total-tokens', this._fmt(s.totalTokens));
            this._updateStat('avg-tokens', this._fmt(s.avgTokensPerRequest));
            this._updateStat('total-cost', `$${this._cost(s.totalCost)}`);

            this._lastData = data;
        } catch (e) {
            // Silent fail on auto-refresh
        }
    }

    _updateStat(name, value) {
        const el = document.querySelector(`[data-stat="${name}"]`);
        if (el && el.textContent !== value) {
            el.textContent = value;
            el.classList.add('stat-updated');
            setTimeout(() => el.classList.remove('stat-updated'), 1000);
        }
    }
}
