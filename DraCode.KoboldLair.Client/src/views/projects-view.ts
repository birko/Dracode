/**
 * Projects View for KoboldLair
 * Lists all projects with filtering, search, and actions using b-data-table
 */

import { BaseComponent, define } from 'birko-web-core';

interface Project {
  id: string;
  name: string;
  status: 'Pending' | 'Analyzing' | 'Analyzed' | 'InProgress' | 'Completed' | 'Failed' | 'Cancelled';
  progress: number;
  createdAt: string;
  updatedAt: string;
  agentCount: number;
  taskCount: number;
  description?: string;
}

/**
 * Projects View Component
 * Displays all projects in a data table with actions
 */
export class ProjectsView extends BaseComponent {
  private _projects: Project[] = [];
  private _filter: 'all' | 'active' | 'completed' | 'failed' = 'all';
  private _searchQuery = '';

  static get styles() {
    return `
      :host {
        display: block;
        padding: 20px;
        max-width: 1400px;
        margin: 0 auto;
      }

      .projects-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
      }

      .projects-header h1 {
        font-size: 28px;
        font-weight: 600;
        margin: 0;
        color: var(--b-text, #1f2937);
      }

      .filter-bar {
        display: flex;
        gap: 12px;
        margin-bottom: 20px;
        flex-wrap: wrap;
      }

      .filter-group {
        display: flex;
        gap: 8px;
        align-items: center;
      }

      .stats-summary {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
        gap: 16px;
        margin-bottom: 24px;
      }

      .stat-card {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .stat-label {
        font-size: 13px;
        color: var(--b-text-secondary, #6b7280);
      }

      .stat-value {
        font-size: 28px;
        font-weight: 700;
        color: var(--b-text, #1f2937);
      }

      .projects-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
        gap: 16px;
      }

      .project-card {
        cursor: pointer;
        transition: all 0.2s ease;
        position: relative;
      }

      .project-card:hover {
        border-color: var(--b-color-primary, #3b82f6);
        box-shadow: var(--b-shadow-md, 0 4px 12px rgba(0, 0, 0, 0.1));
        transform: translateY(-2px);
      }

      .project-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        margin-bottom: 12px;
      }

      .project-name {
        font-size: 18px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
        margin-bottom: 4px;
      }

      .project-description {
        font-size: 14px;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 16px;
        line-height: 1.4;
      }

      .project-meta {
        display: flex;
        justify-content: space-between;
        align-items: center;
        font-size: 13px;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 12px;
      }

      .project-actions {
        display: flex;
        gap: 8px;
        margin-top: 16px;
      }

      .progress-bar {
        height: 6px;
        background: var(--b-bg-tertiary, #f3f4f6);
        border-radius: var(--b-radius-full, 9999px);
        overflow: hidden;
        margin-bottom: 12px;
      }

      .progress-fill {
        height: 100%;
        background: var(--b-color-primary, #3b82f6);
        transition: width 0.3s ease;
      }

      .empty-state {
        text-align: center;
        padding: 80px 20px;
        color: var(--b-text-secondary, #6b7280);
      }

      .empty-state-icon {
        font-size: 64px;
        margin-bottom: 20px;
      }
    `;
  }

  render() {
    const filteredProjects = this.getFilteredProjects();
    const stats = this.getStats();

    return `
      <div class="projects-header">
        <h1>📁 Projects</h1>
        <b-button variant="primary" id="newProject">+ New Project</b-button>
      </div>

      <div class="stats-summary">
        <b-card padding="xl" class="stat-card">
          <div class="stat-label">Total Projects</div>
          <div class="stat-value">${stats.total}</div>
        </b-card>
        <b-card padding="xl" class="stat-card">
          <div class="stat-label">In Progress</div>
          <div class="stat-value">${stats.inProgress}</div>
        </b-card>
        <b-card padding="xl" class="stat-card">
          <div class="stat-label">Completed</div>
          <div class="stat-value">${stats.completed}</div>
        </b-card>
        <b-card padding="xl" class="stat-card">
          <div class="stat-label">Failed</div>
          <div class="stat-value">${stats.failed}</div>
        </b-card>
      </div>

      <div class="filter-bar">
        <div class="filter-group">
          <b-search-input
            id="search-input"
            placeholder="Search projects..."
            debounce="300">
          </b-search-input>
        </div>

        <div class="filter-group">
          <label style="font-size: 14px; color: var(--b-text-secondary);">Filter:</label>
          <b-button variant="${this._filter === 'all' ? 'primary' : 'ghost'}" size="sm" data-filter="all">All</b-button>
          <b-button variant="${this._filter === 'active' ? 'primary' : 'ghost'}" size="sm" data-filter="active">Active</b-button>
          <b-button variant="${this._filter === 'completed' ? 'primary' : 'ghost'}" size="sm" data-filter="completed">Completed</b-button>
          <b-button variant="${this._filter === 'failed' ? 'primary' : 'ghost'}" size="sm" data-filter="failed">Failed</b-button>
        </div>
      </div>

      ${filteredProjects.length === 0 ? this.renderEmptyState() : this.renderProjects(filteredProjects)}
    `;
  }

  private renderProjects(projects: Project[]): string {
    return `
      <div class="projects-grid">
        ${projects.map(project => this.renderProjectCard(project)).join('')}
      </div>
    `;
  }

  private renderProjectCard(project: Project): string {
    const statusVariant = this.getStatusVariant(project.status);
    const statusColor = this.getStatusColor(project.status);

    return `
      <b-card class="project-card" data-project-id="${project.id}">
        <div class="project-header">
          <div>
            <div class="project-name">${this.escapeHtml(project.name)}</div>
            ${project.description ? `<div class="project-description">${this.escapeHtml(project.description)}</div>` : ''}
          </div>
          <b-badge variant="${statusVariant}">${project.status}</b-badge>
        </div>

        <div class="progress-bar">
          <div class="progress-fill" style="width: ${project.progress}%; background: ${statusColor}"></div>
        </div>

        <div class="project-meta">
          <span>📅 Created: ${project.createdAt}</span>
          <span>🤖 ${project.agentCount} agents</span>
          <span>📋 ${project.taskCount} tasks</span>
        </div>

        <div class="project-actions">
          <b-button variant="ghost" size="sm" class="view-btn">View Details</b-button>
          <b-button variant="ghost" size="sm" class="pause-btn" ${project.status === 'InProgress' ? '' : 'hidden'}>
            ${project.status === 'InProgress' ? '⏸ Pause' : '▶ Resume'}
          </b-button>
        </div>
      </div>
    `;
  }

  private renderEmptyState(): string {
    return `
      <b-empty
        icon="📁"
        title="No projects yet"
        message="Create your first project to get started with KoboldLair. Projects will be analyzed by Wyvern and executed by Drake.">
      </b-empty>
    `;
  }

  private getStatusVariant(status: string): string {
    const variantMap: Record<string, string> = {
      'Pending': 'secondary',
      'Analyzing': 'info',
      'Analyzed': 'success',
      'InProgress': 'info',
      'Completed': 'success',
      'Failed': 'danger',
      'Cancelled': 'warning'
    };
    return variantMap[status] || 'secondary';
  }

  private getStatusColor(status: string): string {
    const colorMap: Record<string, string> = {
      'Pending': '#9ca3af',
      'Analyzing': '#06b6d4',
      'Analyzed': '#10b981',
      'InProgress': '#3b82f6',
      'Completed': '#10b981',
      'Failed': '#ef4444',
      'Cancelled': '#f59e0b'
    };
    return colorMap[status] || '#9ca3af';
  }

  private getStats() {
    return {
      total: this._projects.length,
      inProgress: this._projects.filter(p => p.status === 'InProgress').length,
      completed: this._projects.filter(p => p.status === 'Completed').length,
      failed: this._projects.filter(p => p.status === 'Failed').length
    };
  }

  private getFilteredProjects(): Project[] {
    return this._projects.filter(project => {
      // Apply status filter
      if (this._filter === 'active' && !['InProgress', 'Analyzing'].includes(project.status)) {
        return false;
      }
      if (this._filter === 'completed' && project.status !== 'Completed') {
        return false;
      }
      if (this._filter === 'failed' && project.status !== 'Failed') {
        return false;
      }

      // Apply search filter
      if (this._searchQuery) {
        const query = this._searchQuery.toLowerCase();
        return project.name.toLowerCase().includes(query) ||
               (project.description?.toLowerCase().includes(query) ?? false);
      }

      return true;
    });
  }

  protected onMount() {
    // Load projects (simulated for now)
    this.loadProjects();

    // Set up event listeners
    this.$('#newProject')?.addEventListener('click', () => this.handleNewProject());

    this.$('#search-input')?.addEventListener('search', (e: CustomEvent) => {
      this._searchQuery = e.detail.value;
      this.update();
    });

    // Filter buttons
    this.$$('b-button[data-filter]').forEach(btn => {
      btn.addEventListener('click', () => {
        this._filter = btn.getAttribute('data-filter') as any;
        this.update();
      });
    });

    // Project card clicks
    this.$$('.project-card').forEach(card => {
      card.addEventListener('click', (e) => {
        if ((e.target as HTMLElement).closest('.project-actions')) {
          return; // Don't trigger if action button clicked
        }
        const projectId = card.getAttribute('data-project-id');
        if (projectId) {
          this.handleProjectClick(projectId);
        }
      });

      const viewBtn = card.querySelector('.view-btn');
      viewBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        const projectId = card.getAttribute('data-project-id');
        this.handleViewProject(projectId!);
      });

      const pauseBtn = card.querySelector('.pause-btn');
      pauseBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        const projectId = card.getAttribute('data-project-id');
        this.handlePauseProject(projectId!);
      });
    });
  }

  protected onUpdated() {
    this.onMount();
  }

  private async loadProjects() {
    // TODO: Load from actual API
    // For now, use mock data
    this._projects = [
      {
        id: '1',
        name: 'E-Commerce Platform',
        description: 'Full-stack e-commerce solution with payment integration',
        status: 'InProgress',
        progress: 65,
        createdAt: '2 hours ago',
        updatedAt: '5 mins ago',
        agentCount: 3,
        taskCount: 12
      },
      {
        id: '2',
        name: 'Task Management System',
        description: 'Collaborative task tracking with real-time updates',
        status: 'Completed',
        progress: 100,
        createdAt: '1 day ago',
        updatedAt: '12 hours ago',
        agentCount: 2,
        taskCount: 8
      },
      {
        id: '3',
        name: 'API Gateway',
        description: 'RESTful API gateway with authentication and rate limiting',
        status: 'Analyzing',
        progress: 15,
        createdAt: '2 days ago',
        updatedAt: '10 mins ago',
        agentCount: 1,
        taskCount: 15
      },
      {
        id: '4',
        name: 'Blog Engine',
        description: 'Markdown-based blog with comments and tags',
        status: 'Pending',
        progress: 0,
        createdAt: '3 days ago',
        updatedAt: '3 days ago',
        agentCount: 0,
        taskCount: 6
      },
      {
        id: '5',
        name: 'Chat Application',
        description: 'Real-time chat with WebSocket and file sharing',
        status: 'Failed',
        progress: 35,
        createdAt: '4 days ago',
        updatedAt: '1 day ago',
        agentCount: 2,
        taskCount: 10
      }
    ];

    this.update();
  }

  private handleNewProject() {
    console.log('New Project clicked');
    this.emit('navigate', { route: '/dragon' });
  }

  private handleProjectClick(projectId: string) {
    console.log('Project clicked:', projectId);
    // TODO: Navigate to project details
  }

  private handleViewProject(projectId: string) {
    console.log('View project:', projectId);
    this.emit('project-selected', { projectId });
  }

  private handlePauseProject(projectId: string) {
    const project = this._projects.find(p => p.id === projectId);
    if (project) {
      console.log('Pause/Resume project:', projectId, project.status);
      // TODO: Call API to pause/resume project
      this.emit('project-toggle', { projectId, currentStatus: project.status });
    }
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  /**
   * Public methods
   */
  public refresh(): void {
    this.loadProjects();
  }
}

// Register the custom element
define('kobold-projects-view', ProjectsView);
