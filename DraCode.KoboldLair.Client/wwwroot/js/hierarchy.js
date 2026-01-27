/**
 * Hierarchy Visualization Controller
 * Displays real-time KoboldTown process hierarchy
 */

class HierarchyVisualization {
    constructor() {
        this.autoRefresh = true;
        this.refreshInterval = 5000; // 5 seconds
        this.refreshTimer = null;
        
        this.init();
    }

    init() {
        this.setupEventListeners();
        this.loadData();
        this.startAutoRefresh();
    }

    setupEventListeners() {
        document.getElementById('refreshBtn')?.addEventListener('click', () => {
            this.loadData();
        });

        document.getElementById('autoRefreshBtn')?.addEventListener('click', (e) => {
            this.autoRefresh = !this.autoRefresh;
            e.target.classList.toggle('active');
            
            if (this.autoRefresh) {
                this.startAutoRefresh();
            } else {
                this.stopAutoRefresh();
            }
        });
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
            // Fetch hierarchy data from API
            const apiUrl = CONFIG.serverUrl.replace(/^ws/, 'http') + '/api/hierarchy';
            const response = await fetch(apiUrl);
            
            if (!response.ok) {
                // If API not found, generate mock data
                if (response.status === 404) {
                    this.renderMockData();
                    return;
                }
                throw new Error('Failed to fetch hierarchy data');
            }
            
            const data = await response.json();
            this.render(data);
        } catch (error) {
            console.error('Error loading hierarchy:', error);
            this.renderMockData();
        }
    }

    renderMockData() {
        const mockData = {
            statistics: {
                dragonSessions: 1,
                projects: 3,
                wyrms: 2,
                drakes: 4,
                koboldsWorking: 8
            },
            projects: [
                {
                    id: '1',
                    name: 'E-Commerce Platform',
                    status: 'Analyzed',
                    wyrmId: 'wyrm-1',
                    createdAt: new Date(Date.now() - 3600000).toISOString(),
                    analyzedAt: new Date(Date.now() - 1800000).toISOString(),
                    outputPath: './workspace/e-commerce-platform',
                    taskFiles: {
                        'backend': 'backend-tasks.md',
                        'frontend': 'frontend-tasks.md'
                    }
                },
                {
                    id: '2',
                    name: 'Mobile App Redesign',
                    status: 'WyrmAssigned',
                    wyrmId: 'wyrm-2',
                    createdAt: new Date(Date.now() - 7200000).toISOString(),
                    outputPath: './workspace/mobile-app-redesign'
                },
                {
                    id: '3',
                    name: 'API Gateway Service',
                    status: 'New',
                    createdAt: new Date(Date.now() - 300000).toISOString(),
                    outputPath: './workspace/api-gateway-service'
                }
            ],
            hierarchy: {
                dragon: {
                    name: 'Dragon Requirements Agent',
                    icon: '🐉',
                    status: 'active',
                    activeSessions: 1
                },
                projects: [
                    {
                        name: 'E-Commerce Platform',
                        icon: '📁',
                        status: 'analyzed',
                        wyrm: {
                            name: 'Wyrm (e-commerce-platform)',
                            icon: '🐲',
                            status: 'active',
                            analyzed: true,
                            totalTasks: 12
                        }
                    },
                    {
                        name: 'Mobile App Redesign',
                        icon: '📁',
                        status: 'assigned',
                        wyrm: {
                            name: 'Wyrm (mobile-app-redesign)',
                            icon: '🐲',
                            status: 'working',
                            analyzed: false
                        }
                    }
                ]
            }
        };

        this.render(mockData);
    }

    render(data) {
        this.updateStatistics(data.statistics);
        this.renderHierarchy(data.hierarchy);
        this.renderProjects(data.projects);
    }

    updateStatistics(stats) {
        document.getElementById('dragonCount').textContent = stats.dragonSessions || 0;
        document.getElementById('projectCount').textContent = stats.projects || 0;
        document.getElementById('wyrmCount').textContent = stats.wyrms || 0;
        document.getElementById('drakeCount').textContent = stats.drakes || 0;
        document.getElementById('koboldCount').textContent = stats.koboldsWorking || 0;

        // Animate stat changes
        document.querySelectorAll('.stat-value').forEach(el => {
            el.style.animation = 'none';
            setTimeout(() => {
                el.style.animation = 'pulse 0.3s ease-in-out';
            }, 10);
        });
    }

    renderHierarchy(hierarchy) {
        const canvas = document.getElementById('hierarchyCanvas');
        
        const html = `
            <div class="hierarchy-container">
                <!-- Dragon Level -->
                <div class="hierarchy-level">
                    <div class="level-title">🐉 Requirements Gathering</div>
                    <div class="nodes-container">
                        ${this.createNode('dragon', hierarchy.dragon)}
                    </div>
                </div>

                <!-- Projects & Wyrms Level -->
                <div class="hierarchy-level">
                    <div class="level-title">📁 Projects & 🐲 Wyrms</div>
                    <div class="nodes-container">
                        ${hierarchy.projects?.map(proj => `
                            <div class="project-wyrm-group">
                                ${this.createNode('project', proj)}
                                ${proj.wyrm ? this.createNode('wyrm', proj.wyrm) : ''}
                            </div>
                        `).join('') || '<p class="empty-message">No projects yet</p>'}
                    </div>
                </div>

                <!-- Note: Drake and Kobold hierarchy shown in task execution view -->
                <div class="hierarchy-level">
                    <div class="level-title">🦎 Task Execution</div>
                    <div class="nodes-container">
                        <div class="hierarchy-node drake">
                            <div class="node-icon">🦎</div>
                            <div class="node-title">Drakes</div>
                            <div class="node-meta">Supervise task execution</div>
                            <div class="node-status working">Active</div>
                        </div>
                        <div class="hierarchy-node kobold">
                            <div class="node-icon">⚙️</div>
                            <div class="node-title">Kobolds</div>
                            <div class="node-meta">Execute individual tasks</div>
                            <div class="node-status working">Working</div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        canvas.innerHTML = html;

        // Add click handlers for interactive nodes
        canvas.querySelectorAll('.hierarchy-node').forEach(node => {
            node.addEventListener('click', () => {
                this.showNodeDetails(node);
            });
        });
    }

    createNode(type, data) {
        if (!data) return '';

        const statusClass = data.status || 'idle';
        const icon = data.icon || this.getDefaultIcon(type);
        
        return `
            <div class="hierarchy-node ${type} ${data.status === 'working' ? 'pulse-animation' : ''}" data-type="${type}" data-id="${data.id || ''}">
                <div class="node-icon">${icon}</div>
                <div class="node-title">${data.name}</div>
                ${data.activeSessions ? `<div class="node-meta">${data.activeSessions} active</div>` : ''}
                ${data.totalTasks ? `<div class="node-meta">${data.totalTasks} tasks</div>` : ''}
                ${data.analyzed !== undefined ? `<div class="node-meta">${data.analyzed ? '✅ Analyzed' : '⏳ Analyzing'}</div>` : ''}
                <div class="node-status ${statusClass}">${this.getStatusLabel(statusClass)}</div>
            </div>
        `;
    }

    getDefaultIcon(type) {
        const icons = {
            dragon: '🐉',
            project: '📁',
            wyrm: '🐲',
            drake: '🦎',
            kobold: '⚙️'
        };
        return icons[type] || '📦';
    }

    getStatusLabel(status) {
        const labels = {
            active: 'Active',
            idle: 'Idle',
            working: 'Working',
            done: 'Done',
            analyzed: 'Analyzed',
            assigned: 'Assigned'
        };
        return labels[status] || status;
    }

    showNodeDetails(node) {
        const type = node.dataset.type;
        const title = node.querySelector('.node-title').textContent;
        
        // Add visual feedback
        node.style.transform = 'scale(1.1)';
        setTimeout(() => {
            node.style.transform = '';
        }, 200);

        console.log(`Clicked on ${type}: ${title}`);
        // TODO: Show detailed modal/panel with node information
    }

    renderProjects(projects) {
        const container = document.getElementById('projectsList');
        
        if (!projects || projects.length === 0) {
            container.innerHTML = '<p class="empty-message">No projects yet</p>';
            return;
        }

        const html = projects.map(project => `
            <div class="project-card">
                <div class="project-header">
                    <div class="project-title">📁 ${project.name}</div>
                    <div class="project-status-badge ${project.status.toLowerCase().replace(/\s+/g, '')}">${project.status}</div>
                </div>
                <div class="project-info">
                    <div class="project-info-row">
                        <span class="project-info-label">Created:</span>
                        <span>${this.formatDate(project.createdAt)}</span>
                    </div>
                    ${project.wyrmId ? `
                        <div class="project-info-row">
                            <span class="project-info-label">Wyrm:</span>
                            <span class="project-wyrm">🐲 ${project.wyrmId}</span>
                        </div>
                    ` : ''}
                    ${project.analyzedAt ? `
                        <div class="project-info-row">
                            <span class="project-info-label">Analyzed:</span>
                            <span>${this.formatDate(project.analyzedAt)}</span>
                        </div>
                    ` : ''}
                    <div class="project-info-row">
                        <span class="project-info-label">Output:</span>
                        <span>${project.outputPath}</span>
                    </div>
                    ${project.taskFiles && Object.keys(project.taskFiles).length > 0 ? `
                        <div class="project-info-row">
                            <span class="project-info-label">Task Files:</span>
                            <span>${Object.keys(project.taskFiles).length} area(s)</span>
                        </div>
                    ` : ''}
                </div>
            </div>
        `).join('');

        container.innerHTML = html;
    }

    formatDate(dateString) {
        if (!dateString) return 'N/A';
        
        const date = new Date(dateString);
        const now = new Date();
        const diff = now - date;
        
        const minutes = Math.floor(diff / 60000);
        const hours = Math.floor(diff / 3600000);
        const days = Math.floor(diff / 86400000);
        
        if (minutes < 1) return 'Just now';
        if (minutes < 60) return `${minutes}m ago`;
        if (hours < 24) return `${hours}h ago`;
        if (days < 7) return `${days}d ago`;
        
        return date.toLocaleDateString();
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.hierarchyViz = new HierarchyVisualization();
});
