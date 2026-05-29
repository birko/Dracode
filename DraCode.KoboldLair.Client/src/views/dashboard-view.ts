/**
 * Dashboard View for KoboldLair
 * First migrated view component using Birko.Web components
 */

import { BaseComponent, define } from 'birko-web-core';
import toastService from '../services/toast-service.js';

interface DashboardStats {
  totalProjects: number;
  activeAgents: number;
  runningTasks: number;
  completedToday: number;
}

interface RecentProject {
  id: string;
  name: string;
  status: string;
  progress: number;
  createdAt: string;
}

/**
 * Dashboard View Component
 * Displays project statistics, recent activity, and quick actions
 */
export class DashboardView extends BaseComponent {
  private _stats: DashboardStats = {
    totalProjects: 0,
    activeAgents: 0,
    runningTasks: 0,
    completedToday: 0
  };

  private _recentProjects: RecentProject[] = [];
  private _loading = true;

  static get styles() {
    return `
      :host {
        display: block;
        padding: 20px;
        max-width: 1400px;
        margin: 0 auto;
      }

      .dashboard-header {
        margin-bottom: 32px;
      }

      .dashboard-header h1 {
        font-size: 32px;
        font-weight: 600;
        margin: 0 0 8px 0;
        color: var(--b-text, #1f2937);
      }

      .dashboard-header p {
        font-size: 14px;
        color: var(--b-text-secondary, #6b7280);
        margin: 0;
      }

      .stats-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
        gap: 20px;
        margin-bottom: 32px;
      }

      /* b-card hover effect */
      .stat-card:hover {
        box-shadow: var(--b-shadow-md, 0 4px 6px -1px rgba(0, 0, 0, 0.1));
      }

      .stat-label {
        font-size: 14px;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 8px;
      }

      .stat-value {
        font-size: 36px;
        font-weight: 700;
        color: var(--b-text, #1f2937);
        margin-bottom: 4px;
      }

      .stat-change {
        font-size: 12px;
        color: var(--b-color-success, #10b981);
      }

      .section-title {
        font-size: 20px;
        font-weight: 600;
        margin-bottom: 16px;
        color: var(--b-text, #1f2937);
      }

      .projects-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
        gap: 16px;
      }

      /* b-card hover effect */
      .project-card {
        cursor: pointer;
        transition: all 0.2s ease;
      }

      .project-card:hover {
        border-color: var(--b-color-primary, #3b82f6);
        box-shadow: var(--b-shadow-md, 0 4px 6px -1px rgba(0, 0, 0, 0.1));
      }

      .project-name {
        font-size: 16px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
        margin-bottom: 8px;
      }

      .project-meta {
        display: flex;
        justify-content: space-between;
        align-items: center;
        font-size: 12px;
        color: var(--b-text-secondary, #6b7280);
      }

      .progress-bar {
        height: 4px;
        background: var(--b-bg-tertiary, #f3f4f6);
        border-radius: var(--b-radius-full, 9999px);
        overflow: hidden;
        margin-top: 12px;
      }

      .progress-fill {
        height: 100%;
        background: var(--b-color-primary, #3b82f6);
        transition: width 0.3s ease;
      }

      .quick-actions {
        display: flex;
        gap: 12px;
        margin-bottom: 32px;
      }

      .empty-state {
        text-align: center;
        padding: 60px 20px;
        color: var(--b-text-secondary, #6b7280);
      }

      .empty-state-icon {
        font-size: 48px;
        margin-bottom: 16px;
      }
    `;
  }

  render() {
    return `
      <div class="dashboard-header">
        <h1>🏰 KoboldLair Dashboard</h1>
        <p>Multi-agent system overview and activity</p>
      </div>

      <!-- Statistics -->
      <div class="stats-grid">
        <b-card padding="xl" class="stat-card">
          <div class="stat-label">Total Projects</div>
          <div class="stat-value">${this._stats.totalProjects}</div>
          <div class="stat-change">+${this._stats.completedToday} completed today</div>
        </b-card>

        <b-card padding="xl" class="stat-card">
          <div class="stat-label">Active Agents</div>
          <div class="stat-value">${this._stats.activeAgents}</div>
          <div class="stat-change">Currently running</div>
        </b-card>

        <b-card padding="xl" class="stat-card">
          <div class="stat-label">Running Tasks</div>
          <div class="stat-value">${this._stats.runningTasks}</div>
          <div class="stat-change">In progress</div>
        </b-card>

        <b-card padding="xl" class="stat-card">
          <div class="stat-label">Completed Today</div>
          <div class="stat-value">${this._stats.completedToday}</div>
          <div class="stat-change">Tasks finished</div>
        </b-card>
      </div>

      <!-- Quick Actions -->
      <div class="quick-actions">
        <b-button variant="primary" id="newProject">+ New Project</b-button>
        <b-button variant="secondary" id="viewAll">View All Projects</b-button>
      </div>

      <!-- Recent Projects -->
      <h2 class="section-title">Recent Projects</h2>
      ${this.renderProjects()}

      <!-- Empty State -->
      ${this._recentProjects.length === 0 ? this.renderEmptyState() : ''}

      <!-- Loading Skeleton -->
      ${this._loading ? this.renderSkeleton() : ''}
    `;
  }

  private renderProjects(): string {
    if (this._recentProjects.length === 0) {
      return '';
    }

    return `
      <div class="projects-grid">
        ${this._recentProjects.map(project => this.renderProjectCard(project)).join('')}
      </div>
    `;
  }

  private renderProjectCard(project: RecentProject): string {
    const statusVariant = this.getStatusVariant(project.status);

    return `
      <b-card class="project-card" data-project-id="${project.id}">
        <div class="project-name">${this.escapeHtml(project.name)}</div>
        <div class="project-meta">
          <span>${project.createdAt}</span>
          <b-badge variant="${statusVariant}">${project.status}</b-badge>
        </div>
        <div class="progress-bar">
          <div class="progress-fill" style="width: ${project.progress}%"></div>
        </div>
      </b-card>
    `;
  }

  private renderEmptyState(): string {
    return `
      <b-empty
        icon="📁"
        title="No projects yet"
        message="Create your first project to get started with KoboldLair"
        actionLabel="+ New Project"
        actionId="newProjectEmpty">
      </b-empty>
    `;
  }

  private renderSkeleton(): string {
    return `
      <div style="margin-top: 32px;">
        <h2 class="section-title">Recent Projects</h2>
        <div class="projects-grid">
          <b-skeleton type="card" style="height: 120px;"></b-skeleton>
          <b-skeleton type="card" style="height: 120px;"></b-skeleton>
          <b-skeleton type="card" style="height: 120px;"></b-skeleton>
        </div>
      </div>
    `;
  }

  private getStatusVariant(status: string): string {
    const variantMap: Record<string, string> = {
      'Analyzed': 'success',
      'InProgress': 'info',
      'Pending': 'warning',
      'Failed': 'danger',
      'Completed': 'success'
    };
    return variantMap[status] || 'secondary';
  }

  protected onMount() {
    // Load dashboard data
    this.loadDashboardData();

    // Set up event listeners
    this.$('#newProject')?.addEventListener('click', () => {
      this.handleNewProject();
    });

    this.$('#viewAll')?.addEventListener('click', () => {
      this.handleViewAll();
    });

    // Listen for project card clicks
    this.$$('.project-card').forEach(card => {
      card.addEventListener('click', () => {
        const projectId = card.getAttribute('data-project-id');
        if (projectId) {
          this.handleProjectClick(projectId);
        }
      });
    });
  }

  protected onUpdated() {
    // Re-attach event listeners after update
    this.onMount();
  }

  private async loadDashboardData() {
    this._loading = true;
    this.update();

    try {
      // TODO: Replace with actual API call
      // For now, load from WebSocket or use mock data

      // Mock data for demonstration
      this._stats = {
        totalProjects: 12,
        activeAgents: 5,
        runningTasks: 8,
        completedToday: 15
      };

      this._recentProjects = [
        {
          id: '1',
          name: 'E-Commerce Platform',
          status: 'InProgress',
          progress: 65,
          createdAt: '2 hours ago'
        },
        {
          id: '2',
          name: 'Task Management System',
          status: 'Analyzed',
          progress: 100,
          createdAt: '1 day ago'
        },
        {
          id: '3',
          name: 'API Gateway',
          status: 'Pending',
          progress: 0,
          createdAt: '2 days ago'
        }
      ];

      this._loading = false;
      this.update();
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
      this._loading = false;
      this.update();
    }
  }

  private handleNewProject() {
    console.log('New Project clicked');
    // TODO: Navigate to project creation or show modal
    this.emit('navigate', { route: '/projects/create' });
  }

  private handleViewAll() {
    console.log('View All clicked');
    this.emit('navigate', { route: '/projects' });
  }

  private handleProjectClick(projectId: string) {
    console.log('Project clicked:', projectId);
    this.emit('project-selected', { projectId });
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  /**
   * Public method to refresh dashboard data
   */
  public refresh(): void {
    this.loadDashboardData();
  }
}

// Register the custom element
define('kobold-dashboard-view', DashboardView);
