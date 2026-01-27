/**
 * Providers View Module - AI Provider Configuration
 */
export class ProvidersView {
    constructor(app) {
        this.app = app;
        this.providers = [];
        this.agentProviders = {};
    }

    async render() {
        const container = document.getElementById('viewContainer');
        container.innerHTML = `
            <section class="providers-view">
                <div class="section-header">
                    <h2>🔧 AI Provider Configuration</h2>
                    <button id="refreshProvidersBtn" class="btn btn-secondary">🔄 Refresh</button>
                </div>

                <div class="info-banner" style="margin-bottom: 1.5rem;">
                    <span class="info-icon">ℹ️</span>
                    <div class="info-content">
                        <strong>Provider Configuration</strong>
                        <p>Configure which AI providers are used by different agent types (Dragon, Wyrm, Drake, Kobold).</p>
                    </div>
                </div>

                <!-- Agent Provider Assignments -->
                <div class="agent-providers-section">
                    <h3>Agent Provider Assignments</h3>
                    <div id="agentProvidersContainer" class="agent-providers-grid">
                        <p class="loading-message">Loading agent configurations...</p>
                    </div>
                </div>

                <!-- Available Providers -->
                <div class="providers-section">
                    <h3>Available Providers</h3>
                    <div id="providersContainer" class="providers-container">
                        <p class="loading-message">Loading providers...</p>
                    </div>
                </div>
            </section>
        `;

        this.setupEventListeners();
        await this.loadProviders();
    }

    setupEventListeners() {
        const refreshBtn = document.getElementById('refreshProvidersBtn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => this.loadProviders());
        }
    }

    async loadProviders() {
        try {
            const response = await fetch('/api/providers');
            const data = await response.json();
            this.providers = data.providers || [];
            this.agentProviders = data.agentProviders || {};
            this.renderAgentProviders();
            this.renderProviders();
        } catch (error) {
            console.error('Failed to load providers:', error);
            this.renderError();
        }
    }

    renderAgentProviders() {
        const container = document.getElementById('agentProvidersContainer');
        if (!container) return;

        const agentTypes = ['dragon', 'wyrm', 'drake', 'kobold'];
        const agentIcons = {
            'dragon': '🐉',
            'wyrm': '🐲',
            'drake': '🦎',
            'kobold': '⚙️'
        };

        container.innerHTML = agentTypes.map(agentType => {
            const currentProvider = this.agentProviders[agentType] || 'Not configured';
            return `
                <div class="agent-provider-card">
                    <div class="agent-provider-header">
                        <span class="agent-icon">${agentIcons[agentType]}</span>
                        <h4>${agentType.charAt(0).toUpperCase() + agentType.slice(1)}</h4>
                    </div>
                    <div class="form-group">
                        <label>Current Provider:</label>
                        <select class="form-select provider-select" data-agent="${agentType}">
                            ${this.providers
                                .filter(p => p.compatibleAgents.includes(agentType) || p.compatibleAgents.includes('all'))
                                .map(p => `
                                    <option value="${p.name}" ${currentProvider === p.name ? 'selected' : ''}>
                                        ${this.escapeHtml(p.displayName)} (${this.escapeHtml(p.defaultModel)})
                                    </option>
                                `).join('')}
                        </select>
                    </div>
                    <button class="btn btn-primary update-provider-btn" data-agent="${agentType}">
                        Update Provider
                    </button>
                </div>
            `;
        }).join('');

        // Add event listeners for update buttons
        container.querySelectorAll('.update-provider-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const agentType = e.target.dataset.agent;
                this.updateAgentProvider(agentType);
            });
        });
    }

    async updateAgentProvider(agentType) {
        const select = document.querySelector(`select[data-agent="${agentType}"]`);
        if (!select) return;

        const providerName = select.value;
        const button = document.querySelector(`button[data-agent="${agentType}"]`);
        
        // Disable button during validation
        if (button) {
            button.disabled = true;
            button.textContent = 'Validating...';
        }
        
        try {
            // Validate provider first
            const validateResponse = await fetch(`/api/providers/validate/${encodeURIComponent(providerName)}`);
            const validation = await validateResponse.json();
            
            if (!validation.isValid) {
                this.app.ui.addLog('error', `Provider validation failed: ${validation.message || 'Invalid configuration'}`);
                if (button) {
                    button.disabled = false;
                    button.textContent = 'Update Provider';
                }
                return;
            }
            
            // Update button text
            if (button) {
                button.textContent = 'Updating...';
            }
            
            // Provider is valid, proceed with update
            const response = await fetch('/api/providers/configure', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    agentType,
                    providerName,
                    modelOverride: null
                })
            });

            const result = await response.json();
            
            if (result.success) {
                this.app.ui.addLog('success', `✓ ${agentType} provider updated to ${providerName}`);
                await this.loadProviders();
            } else {
                this.app.ui.addLog('error', `Failed to update provider: ${result.message}`);
            }
        } catch (error) {
            console.error('Failed to update provider:', error);
            this.app.ui.addLog('error', 'Failed to update provider');
        } finally {
            // Re-enable button
            if (button) {
                button.disabled = false;
                button.textContent = 'Update Provider';
            }
        }
    }

    renderProviders() {
        const container = document.getElementById('providersContainer');
        if (!container) return;

        if (this.providers.length === 0) {
            container.innerHTML = `
                <p class="placeholder-info">No providers configured. Configure providers in the server settings.</p>
            `;
            return;
        }

        container.innerHTML = this.providers.map(provider => `
            <div class="provider-card ${provider.isEnabled ? 'enabled' : 'disabled'}">
                <div class="provider-header">
                    <h3>${this.escapeHtml(provider.displayName)}</h3>
                    <span class="provider-status ${provider.isEnabled ? 'status-enabled' : 'status-disabled'}">
                        ${provider.isEnabled ? '✓ Enabled' : '○ Disabled'}
                    </span>
                </div>
                <div class="provider-details">
                    <div class="provider-info">
                        <strong>Provider:</strong> ${this.escapeHtml(provider.name)}
                    </div>
                    <div class="provider-info">
                        <strong>Type:</strong> ${this.escapeHtml(provider.type)}
                    </div>
                    <div class="provider-info">
                        <strong>Default Model:</strong> ${this.escapeHtml(provider.defaultModel)}
                    </div>
                    <div class="provider-info">
                        <strong>Compatible Agents:</strong> ${provider.compatibleAgents.join(', ')}
                    </div>
                    <div class="provider-info">
                        <strong>Configured:</strong> ${provider.isConfigured ? '✓ Yes' : '✗ No'}
                    </div>
                    ${provider.description ? `
                        <div class="provider-info">
                            <strong>Description:</strong> ${this.escapeHtml(provider.description)}
                        </div>
                    ` : ''}
                </div>
            </div>
        `).join('');
    }

    renderError() {
        const container = document.getElementById('providersContainer');
        if (container) {
            container.innerHTML = `
                <p class="error-message">Failed to load providers. Please check your connection.</p>
            `;
        }
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    cleanup() {
        // Cleanup if needed
    }
}

