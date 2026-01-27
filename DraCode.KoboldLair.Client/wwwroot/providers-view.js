import { ApiClient } from './api.js';

export class ProvidersView {
    constructor(api) {
        this.api = api;
    }

    async render() {
        try {
            const data = await this.api.getProviders();
            const providers = data.providers;
            const agentProviders = data.agentProviders;

            return `
                <div class="card">
                    <div class="card-header">
                        <h2 class="card-title">AI Providers</h2>
                    </div>
                    <div class="card-body">
                        ${providers.length > 0 ? `
                            <div class="list">
                                ${providers.map(p => this.renderProvider(p)).join('')}
                            </div>
                        ` : `
                            <div class="empty-state">
                                <div class="empty-state-icon">🔧</div>
                                <div>No providers configured</div>
                            </div>
                        `}
                    </div>
                </div>

                <div class="card">
                    <div class="card-header">
                        <h2 class="card-title">Agent Assignments</h2>
                    </div>
                    <div class="card-body">
                        ${this.renderAgentProviders(agentProviders)}
                    </div>
                </div>
            `;
        } catch (error) {
            return `<div class="empty-state">
                <div class="empty-state-icon">⚠️</div>
                <div>Failed to load providers</div>
            </div>`;
        }
    }

    renderProvider(provider) {
        return `
            <div class="list-item">
                <div class="list-item-main">
                    <span class="list-item-icon">${provider.isConfigured ? '✅' : '⚙️'}</span>
                    <div class="list-item-content">
                        <div class="list-item-title">${provider.displayName}</div>
                        <div class="list-item-subtitle">
                            Type: ${provider.type} • Model: ${provider.defaultModel}
                        </div>
                        ${provider.description ? `
                            <div class="list-item-subtitle">${provider.description}</div>
                        ` : ''}
                        <div class="list-item-subtitle">
                            Compatible: ${provider.compatibleAgents.join(', ')}
                        </div>
                    </div>
                </div>
                <div class="list-item-actions">
                    ${provider.requiresApiKey ? `
                        <span class="badge badge-warning">API Key Required</span>
                    ` : ''}
                    ${provider.isEnabled ? `
                        <span class="badge badge-success">Enabled</span>
                    ` : `
                        <span class="badge badge-error">Disabled</span>
                    `}
                    ${provider.isConfigured ? `
                        <span class="badge badge-success">Configured</span>
                    ` : `
                        <span class="badge badge-warning">Not Configured</span>
                    `}
                </div>
            </div>
        `;
    }

    renderAgentProviders(agentProviders) {
        if (!agentProviders) {
            return '<div class="empty-state"><div>No agent assignments</div></div>';
        }

        return `
            <div class="list">
                ${Object.entries(agentProviders).map(([agent, provider]) => `
                    <div class="list-item">
                        <div class="list-item-main">
                            <span class="list-item-icon">${this.getAgentIcon(agent)}</span>
                            <div class="list-item-content">
                                <div class="list-item-title">${agent}</div>
                                <div class="list-item-subtitle">Provider: ${provider}</div>
                            </div>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
    }

    getAgentIcon(agent) {
        const icons = {
            'Dragon': '🐉',
            'Wyrm': '🐲',
            'Drake': '🔥',
            'Kobold': '⚒️'
        };
        return icons[agent] || '🤖';
    }
}
