/**
 * Hierarchy View Module - Process Hierarchy Visualization
 */
export class HierarchyView {
    constructor(app) {
        this.app = app;
        this.autoRefresh = true;
        this.refreshInterval = 5000;
        this.refreshTimer = null;
    }

    async render() {
        const container = document.getElementById('viewContainer');
        container.innerHTML = `
            <section class="hierarchy-view">
                <div class="section-header">
                    <h2>🏰 Process Hierarchy</h2>
                    <div class="header-actions">
                        <button id="autoRefreshBtn" class="btn btn-secondary active">
                            🔄 Auto-Refresh
                        </button>
                        <button id="refreshBtn" class="btn btn-secondary">
                            ↻ Refresh Now
                        </button>
                    </div>
                </div>

                <!-- Statistics -->
                <div id="statsGrid" class="hierarchy-stats">
                    <div class="stat-card">
                        <div class="stat-icon">🐉</div>
                        <div class="stat-value" id="dragonCount">-</div>
                        <div class="stat-label">Dragon Sessions</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon">📁</div>
                        <div class="stat-value" id="projectCount">-</div>
                        <div class="stat-label">Projects</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon">🐲</div>
                        <div class="stat-value" id="wyrmCount">-</div>
                        <div class="stat-label">Wyrms</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon">🦎</div>
                        <div class="stat-value" id="drakeCount">-</div>
                        <div class="stat-label">Drakes</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon">⚙️</div>
                        <div class="stat-value" id="koboldCount">-</div>
                        <div class="stat-label">Kobolds Active</div>
                    </div>
                </div>

                <!-- Hierarchy Tree -->
                <div class="hierarchy-container">
                    <div id="hierarchyTree" class="hierarchy-tree">
                        <p class="loading-message">Loading hierarchy...</p>
                    </div>
                </div>
            </section>
        `;

        this.setupEventListeners();
        this.startAutoRefresh();
        await this.loadData();
    }

    setupEventListeners() {
        const refreshBtn = document.getElementById('refreshBtn');
        const autoRefreshBtn = document.getElementById('autoRefreshBtn');

        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => this.loadData());
        }

        if (autoRefreshBtn) {
            autoRefreshBtn.addEventListener('click', (e) => {
                this.autoRefresh = !this.autoRefresh;
                e.target.classList.toggle('active');

                if (this.autoRefresh) {
                    this.startAutoRefresh();
                } else {
                    this.stopAutoRefresh();
                }
            });
        }
    }

    startAutoRefresh() {
        if (this.refreshTimer) {
            clearInterval(this.refreshTimer);
        }

        this.refreshTimer = setInterval(() => {
            if (this.autoRefresh) {
                this.loadData();
            }
        }, this.refreshInterval);
    }

    stopAutoRefresh() {
        if (this.refreshTimer) {
            clearInterval(this.refreshTimer);
            this.refreshTimer = null;
        }
    }

    async loadData() {
        try {
            const apiUrl = CONFIG.apiUrl + '/api/hierarchy';
            const response = await fetch(apiUrl);

            if (!response.ok) {
                throw new Error('Failed to fetch hierarchy data');
            }

            const data = await response.json();
            this.renderHierarchy(data);
        } catch (error) {
            console.error('Error loading hierarchy:', error);
            this.renderPlaceholder();
        }
    }

    renderHierarchy(data) {
        // Update statistics
        if (data.statistics) {
            this.updateStats(data.statistics);
        }

        // Render hierarchy tree
        const treeContainer = document.getElementById('hierarchyTree');
        if (treeContainer && data.hierarchy) {
            treeContainer.innerHTML = this.buildHierarchyHTML(data.hierarchy);
        }
    }

    updateStats(stats) {
        const updates = {
            'dragonCount': stats.dragonSessions || 0,
            'projectCount': stats.projects || 0,
            'wyrmCount': stats.wyrms || 0,
            'drakeCount': stats.drakes || 0,
            'koboldCount': stats.koboldsWorking || 0
        };

        Object.entries(updates).forEach(([id, value]) => {
            const elem = document.getElementById(id);
            if (elem) elem.textContent = value;
        });
    }

    buildHierarchyHTML(hierarchy) {
        // Simple hierarchy representation
        return `
            <div class="hierarchy-node root">
                <span class="node-icon">🐉</span>
                <span class="node-title">Dragon</span>
                <span class="node-status active">Active</span>
            </div>
            <div class="placeholder-info">
                <p>Full hierarchy visualization coming soon</p>
            </div>
        `;
    }

    renderPlaceholder() {
        const treeContainer = document.getElementById('hierarchyTree');
        if (treeContainer) {
            treeContainer.innerHTML = `
                <p class="placeholder-info">No hierarchy data available. Start by creating a project with Dragon.</p>
            `;
        }
    }

    cleanup() {
        this.stopAutoRefresh();
    }
}

