/**
 * Dashboard View Module
 */
export class DashboardView {
    constructor(app) {
        this.app = app;
        this.taskManager = app.taskManager;
        this.ui = app.ui;
    }

    async render() {
        const container = document.getElementById('viewContainer');
        container.innerHTML = `
            <!-- Dashboard Stats Overview -->
            <section class="dashboard-stats">
                <div class="stat-card">
                    <div class="stat-icon">📋</div>
                    <div class="stat-value" id="statTotal">0</div>
                    <div class="stat-label">Total Tasks</div>
                </div>
                <div class="stat-card stat-working">
                    <div class="stat-icon">🟡</div>
                    <div class="stat-value" id="statWorking">0</div>
                    <div class="stat-label">In Progress</div>
                </div>
                <div class="stat-card stat-done">
                    <div class="stat-icon">✅</div>
                    <div class="stat-value" id="statDone">0</div>
                    <div class="stat-label">Completed</div>
                </div>
                <div class="stat-card stat-pending">
                    <div class="stat-icon">⏳</div>
                    <div class="stat-value" id="statPending">0</div>
                    <div class="stat-label">Pending</div>
                </div>
            </section>

            <!-- System Stats -->
            <section class="system-stats">
                <div class="stat-item">
                    <span class="stat-item-label">🐉 Active Dragons:</span>
                    <span class="stat-item-value" id="activeDragons">0</span>
                </div>
                <div class="stat-item">
                    <span class="stat-item-label">🦎 Active Wyrms:</span>
                    <span class="stat-item-value" id="activeWyrms">0</span>
                </div>
                <div class="stat-item">
                    <span class="stat-item-label">🐲 Active Drakes:</span>
                    <span class="stat-item-value" id="activeDrakes">0</span>
                </div>
                <div class="stat-item">
                    <span class="stat-item-label">🦴 Active Kobolds:</span>
                    <span class="stat-item-value" id="activeKobolds">0</span>
                </div>
                <div class="stat-item">
                    <span class="stat-item-label">📁 Total Projects:</span>
                    <span class="stat-item-value" id="totalProjects">0</span>
                </div>
                <div class="stat-item">
                    <span class="stat-item-label">⚙️ Providers:</span>
                    <span class="stat-item-value" id="totalProviders">0</span>
                </div>
            </section>

            <!-- Workflow Overview -->
            <section id="workflowInfo" class="workflow-info hidden">
                <h2>🏰 KoboldLair Automated Workflow</h2>
                <div class="workflow-description">
                    <p>KoboldLair uses an automated AI agent hierarchy to transform your ideas into working code:</p>
                    <div class="workflow-steps">
                        <div class="workflow-step">
                            <div class="step-icon">🐉</div>
                            <h3>1. Dragon (Interactive)</h3>
                            <p>Talk to Dragon to describe your project. Dragon creates detailed specifications.</p>
                            <button class="btn btn-primary nav-link" data-route="dragon">💬 Chat with Dragon</button>
                        </div>
                        <div class="workflow-arrow">↓</div>
                        <div class="workflow-step">
                            <div class="step-icon">🐲</div>
                            <h3>2. Wyrm (Automatic)</h3>
                            <p>Wyrm automatically analyzes specifications and breaks them into organized tasks.</p>
                        </div>
                        <div class="workflow-arrow">↓</div>
                        <div class="workflow-step">
                            <div class="step-icon">🦎</div>
                            <h3>3. Drake (Automatic)</h3>
                            <p>Drake supervisors automatically assign and monitor task execution.</p>
                        </div>
                        <div class="workflow-arrow">↓</div>
                        <div class="workflow-step">
                            <div class="step-icon">⚙️</div>
                            <h3>4. Kobold (Automatic)</h3>
                            <p>Kobold workers automatically execute individual tasks and generate code.</p>
                        </div>
                    </div>
                    <div class="workflow-note">
                        <strong>Note:</strong> Only Dragon requires user interaction. Everything else happens automatically in the background.
                    </div>
                </div>
            </section>

            <!-- Dashboard Grid -->
            <div class="dashboard-grid">
                <!-- Task Status Panel -->
                <section class="tasks-section dashboard-panel">
                    <div class="section-header">
                        <h2>📊 Task Status</h2>
                        <div class="header-actions">
                            <button id="refreshBtn" class="btn btn-secondary">🔄 Refresh</button>
                            <button id="downloadMarkdownBtn" class="btn btn-secondary">📥 Report</button>
                        </div>
                    </div>
                    
                    <div class="filter-bar">
                        <button class="filter-btn active" data-filter="all">All</button>
                        <button class="filter-btn" data-filter="unassigned">⚪ Unassigned</button>
                        <button class="filter-btn" data-filter="notinitialized">🔵 Not Initialized</button>
                        <button class="filter-btn" data-filter="working">🟡 Working</button>
                        <button class="filter-btn" data-filter="done">🟢 Done</button>
                    </div>

                    <div id="tasksContainer" class="tasks-container">
                        <p class="empty-message">No tasks yet. Submit a task to get started!</p>
                    </div>
                </section>

                <!-- Agent Logs Panel -->
                <section class="logs-section dashboard-panel">
                    <div class="section-header">
                        <h2>📝 Agent Logs</h2>
                        <button id="clearLogsBtn" class="btn btn-secondary">🗑️ Clear</button>
                    </div>
                    <div id="logsContainer" class="logs-container"></div>
                </section>
            </div>
        `;

        // Re-initialize UI elements
        this.ui.initElements();
        this.ui.bindEvents();
        
        // Update stats and tasks
        this.ui.updateStats();
        this.ui.renderTasks();
        
        // Setup info toggle
        this.setupInfoToggle();
        
        // Setup router navigation for workflow button
        this.app.router.setupNavLinks();
        
        // Load system stats
        await this.loadSystemStats();
        
        // Refresh stats every 10 seconds
        this.statsInterval = setInterval(() => this.loadSystemStats(), 10000);
    }

    async loadSystemStats() {
        try {
            const response = await fetch('/api/stats');
            const stats = await response.json();
            
            // Update system stats
            this.updateElement('activeDragons', stats.activeDragons || 0);
            this.updateElement('activeWyrms', stats.activeWyrms || 0);
            this.updateElement('activeDrakes', stats.activeDrakes || 0);
            this.updateElement('activeKobolds', stats.activeKobolds || 0);
            this.updateElement('totalProjects', stats.totalProjects || 0);
            this.updateElement('totalProviders', stats.totalProviders || 0);
        } catch (error) {
            console.error('Failed to load system stats:', error);
        }
    }

    updateElement(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = value;
        }
    }

    setupInfoToggle() {
        const infoToggle = document.getElementById('infoToggle');
        const workflowInfo = document.getElementById('workflowInfo');
        
        if (infoToggle && workflowInfo) {
            infoToggle.addEventListener('click', function() {
                workflowInfo.classList.toggle('hidden');
                infoToggle.classList.toggle('active');
                
                const isHidden = workflowInfo.classList.contains('hidden');
                localStorage.setItem('workflowInfoHidden', isHidden);
            });
            
            const savedState = localStorage.getItem('workflowInfoHidden');
            if (savedState === 'false') {
                workflowInfo.classList.remove('hidden');
                infoToggle.classList.add('active');
            }
        }
    }

    cleanup() {
        if (this.statsInterval) {
            clearInterval(this.statsInterval);
        }
    }
}
