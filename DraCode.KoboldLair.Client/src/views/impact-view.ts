/**
 * Impact View for KoboldLair
 * Displays feature-to-code impact analysis and implementation tracking
 */

import { BaseComponent, define } from 'birko-web-core';

interface FeatureImplementation {
  featureId: string;
  title: string;
  status: 'NotStarted' | 'InProgress' | 'Completed' | 'Failed' | 'Blocked';
  progressPercentage: number;
  filesCreated?: Record<string, any>;
  filesModified?: Record<string, any>;
  taskCount?: number;
}

interface FileImpact {
  path: string;
  features?: string[];
  linesAdded?: number;
  linesModified?: number;
  linesDeleted?: number;
}

interface AreaSummary {
  area: string;
  taskCount: number;
  completedTasks: number;
  progress: number;
}

interface ImpactSummary {
  projectId: string;
  projectName: string;
  overallProgress: number;
  completedTasks: number;
  totalTasks: number;
  featureImplementations: Record<string, FeatureImplementation>;
  fileImpacts: Record<string, FileImpact>;
  areaSummaries: AreaSummary[];
  specificationVersion: number;
  specificationContentHash: string;
}

/**
 * Impact View Component
 * Shows feature-to-code impact analysis for projects
 */
export class ImpactView extends BaseComponent {
  private projects: any[] = [];
  private selectedProjectId: string | null = null;
  private impactData: ImpactSummary | null = null;
  private loading = false;
  private error: string | null = null;

  static get styles() {
    return `
      :host {
        display: block;
        padding: 20px;
        max-width: 1400px;
        margin: 0 auto;
      }

      .view-header {
        margin-bottom: 24px;
      }

      .view-header h1 {
        font-size: 28px;
        font-weight: 600;
        margin: 0 0 8px 0;
        color: var(--b-text, #1f2937);
      }

      .controls-bar {
        display: flex;
        gap: 12px;
        align-items: center;
        margin-bottom: 24px;
        padding: 16px;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-md, 8px);
      }

      .project-select {
        flex: 1;
        max-width: 400px;
      }

      .select-input {
        width: 100%;
        padding: 8px 12px;
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-md, 8px);
        font-size: 14px;
        font-family: inherit;
      }

      .overview-stats {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
        gap: 16px;
        margin-bottom: 24px;
      }

      .stat-card {
        display: flex;
        align-items: center;
        gap: 16px;
      }

      .stat-icon {
        font-size: 32px;
      }

      .stat-content {
        flex: 1;
      }

      .stat-label {
        font-size: 13px;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 4px;
      }

      .stat-value {
        font-size: 24px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
      }

      .stat-detail {
        font-size: 12px;
        color: var(--b-text-muted, #9ca3af);
      }

      .section {
        margin-bottom: 32px;
      }

      .section-title {
        font-size: 20px;
        font-weight: 600;
        margin-bottom: 16px;
        color: var(--b-text, #1f2937);
      }

      .features-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
        gap: 16px;
      }

      .feature-header {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 12px;
      }

      .feature-status {
        font-size: 20px;
      }

      .feature-title {
        flex: 1;
        font-weight: 600;
        color: var(--b-text, #1f2937);
      }

      .feature-progress {
        margin-bottom: 12px;
      }

      .progress-bar {
        height: 8px;
        background: var(--b-bg-secondary, #f9fafb);
        border-radius: 4px;
        overflow: hidden;
      }

      .progress-fill {
        height: 100%;
        background: var(--b-color-primary, #3b82f6);
        border-radius: 4px;
        transition: width 0.3s ease;
      }

      .feature-meta {
        display: flex;
        gap: 16px;
        font-size: 12px;
        color: var(--b-text-secondary, #6b7280);
      }

      .empty-state {
        text-align: center;
        padding: 60px 20px;
        color: var(--b-text-secondary, #6b7280);
      }

      .empty-icon {
        font-size: 64px;
        margin-bottom: 16px;
      }

      .loading-state {
        text-align: center;
        padding: 60px 20px;
      }

      .error-state {
        text-align: center;
        padding: 40px 20px;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-color-danger, #ef4444);
        border-radius: var(--b-radius-lg, 12px);
      }
    `;
  }

  render() {
    return `
      <div class="view-header">
        <h1>🎯 Impact Analysis</h1>
        <p>Track feature implementation and code impact across projects</p>
      </div>

      <div class="controls-bar">
        <div class="project-select">
          <select id="projectSelect" class="select-input">
            <option value="">Select a project...</option>
            ${this.projects.map(p => `
              <option value="${p.id}" ${this.selectedProjectId === p.id ? 'selected' : ''}>
                ${this.escapeHtml(p.name)}
              </option>
            `).join('')}
          </select>
        </div>
        <b-button variant="secondary" id="refreshBtn">
          🔄 Refresh
        </b-button>
      </div>

      <div id="impactContent">
        ${this.renderContent()}
      </div>
    `;
  }

  private renderContent(): string {
    if (this.loading) {
      return `
        <div style="display: flex; justify-content: center; padding: 60px 20px;">
          <b-spinner size="large" message="Loading impact data..."></b-spinner>
        </div>
      `;
    }

    if (this.error) {
      return `
        <div class="error-state">
          <div class="error-icon">⚠️</div>
          <div class="error-title">Failed to load impact data</div>
          <div class="error-message">${this.escapeHtml(this.error)}</div>
        </div>
      `;
    }

    if (!this.impactData) {
      return `
        <div class="empty-state">
          <div class="empty-icon">🎯</div>
          <h3>Select a project</h3>
          <p>Choose a project to view feature-to-code impact analysis</p>
        </div>
      `;
    }

    return this.renderImpactSummary(this.impactData);
  }

  private renderImpactSummary(summary: ImpactSummary): string {
    const progressPercent = summary.overallProgress.toFixed(0);
    const features = Object.values(summary.featureImplementations || {});
    const files = Object.keys(summary.fileImpacts || {});

    return `
      <div class="overview-stats">
        <b-card padding="xl" class="stat-card">
          <div class="stat-icon">📊</div>
          <div class="stat-content">
            <div class="stat-label">Progress</div>
            <div class="stat-value">${progressPercent}%</div>
            <div class="stat-detail">${summary.completedTasks}/${summary.totalTasks} tasks</div>
          </div>
        </b-card>
        <b-card padding="xl" class="stat-card">
          <div class="stat-icon">🎯</div>
          <div class="stat-content">
            <div class="stat-label">Features</div>
            <div class="stat-value">${features.length}</div>
            <div class="stat-detail">In specification</div>
          </div>
        </b-card>
        <b-card padding="xl" class="stat-card">
          <div class="stat-icon">📁</div>
          <div class="stat-content">
            <div class="stat-label">Files</div>
            <div class="stat-value">${files.length}</div>
            <div class="stat-detail">Tracked</div>
          </div>
        </b-card>
        <b-card padding="xl" class="stat-card">
          <div class="stat-icon">📋</div>
          <div class="stat-content">
            <div class="stat-label">Spec Version</div>
            <div class="stat-value">v${summary.specificationVersion}</div>
            <div class="stat-detail">${summary.specificationContentHash.substring(0, 8)}...</div>
          </div>
        </b-card>
      </div>

      ${this.renderFeaturesSection(features)}
      ${this.renderFilesSection(files)}
      ${this.renderAreasSection(summary.areaSummaries)}
    `;
  }

  private renderFeaturesSection(features: FeatureImplementation[]): string {
    if (features.length === 0) return '';

    const statusIcons: Record<string, string> = {
      'NotStarted': '⏳',
      'InProgress': '🔨',
      'Completed': '✅',
      'Failed': '❌',
      'Blocked': '🚫'
    };

    return `
      <div class="section">
        <h2 class="section-title">🎯 Features</h2>
        <div class="features-grid">
          ${features.map(feature => {
            const icon = statusIcons[feature.status] || '❓';
            const progress = feature.progressPercentage.toFixed(0);
            const filesCreated = Object.keys(feature.filesCreated || {}).length;
            const filesModified = Object.keys(feature.filesModified || {}).length;

            return `
              <b-card class="feature-card">
                <div class="feature-header">
                  <span class="feature-status">${icon}</span>
                  <div class="feature-title">${this.escapeHtml(feature.title)}</div>
                </div>
                <div class="feature-progress">
                  <div class="progress-bar">
                    <div class="progress-fill" style="width: ${progress}%"></div>
                  </div>
                  <div style="font-size: 12px; color: var(--b-text-secondary); margin-top: 4px;">
                    ${progress}% complete
                  </div>
                </div>
                <div class="feature-meta">
                  <span>📝 ${filesCreated} created</span>
                  <span>✏️ ${filesModified} modified</span>
                </div>
              </div>
            `;
          }).join('')}
        </div>
      </div>
    `;
  }

  private renderFilesSection(files: string[]): string {
    if (files.length === 0) return '';

    return `
      <div class="section">
        <h2 class="section-title">📁 Files Impacted</h2>
        <div style="display: grid; grid-template-columns: repeat(auto-fill, minmax(400px, 1fr)); gap: 8px;">
          ${files.map(file => `
            <div style="padding: 8px 12px; background: var(--b-bg-elevated, #ffffff); border: 1px solid var(--b-border, #e5e7eb); border-radius: 6px; font-family: monospace; font-size: 13px;">
              ${this.escapeHtml(file)}
            </div>
          `).join('')}
        </div>
      </div>
    `;
  }

  private renderAreasSection(areas: AreaSummary[]): string {
    if (!areas || areas.length === 0) return '';

    return `
      <div class="section">
        <h2 class="section-title">📊 Area Progress</h2>
        <div style="display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px;">
          ${areas.map(area => `
            <div style="padding: 16px; background: var(--b-bg-elevated, #ffffff); border: 1px solid var(--b-border, #e5e7eb); border-radius: var(--b-radius-md, 8px);">
              <div style="font-weight: 600; margin-bottom: 8px;">${this.escapeHtml(area.area)}</div>
              <div style="font-size: 13px; color: var(--b-text-secondary); margin-bottom: 8px;">
                ${area.completedTasks}/${area.taskCount} tasks
              </div>
              <div class="progress-bar">
                <div class="progress-fill" style="width: ${area.progress.toFixed(0)}%"></div>
              </div>
            </div>
          `).join('')}
        </div>
      </div>
    `;
  }

  protected onMount() {
    this.loadProjects();

    this.$('#projectSelect')?.addEventListener('change', (e) => {
      const select = e.target as HTMLSelectElement;
      this.selectedProjectId = select.value || null;
      if (this.selectedProjectId) {
        this.loadImpactData(this.selectedProjectId);
      } else {
        this.impactData = null;
        this.update();
      }
    });

    this.$('#refreshBtn')?.addEventListener('click', () => {
      if (this.selectedProjectId) {
        this.loadImpactData(this.selectedProjectId);
      }
    });
  }

  protected onUpdated() {
    this.onMount();
  }

  private async loadProjects() {
    try {
      // TODO: Call actual API
      // this.projects = await this.api.getProjects();

      // Mock data for now
      this.projects = [
        { id: '1', name: 'Todo App' },
        { id: '2', name: 'E-commerce Site' }
      ];

      this.update();
    } catch (err: any) {
      this.error = err.message;
      this.update();
    }
  }

  private async loadImpactData(projectId: string) {
    this.loading = true;
    this.error = null;
    this.update();

    try {
      // TODO: Call actual API
      // const summary = await this.api.getImplementationSummary(projectId);

      // Mock data for now
      await new Promise(resolve => setTimeout(resolve, 1000));

      this.impactData = {
        projectId,
        projectName: 'Mock Project',
        overallProgress: 65,
        completedTasks: 13,
        totalTasks: 20,
        featureImplementations: {
          'feature-1': {
            featureId: 'feature-1',
            title: 'User Authentication',
            status: 'Completed',
            progressPercentage: 100,
            filesCreated: { 'auth.ts': {} },
            filesModified: { 'user.ts': {} }
          },
          'feature-2': {
            featureId: 'feature-2',
            title: 'Data Persistence',
            status: 'InProgress',
            progressPercentage: 45,
            filesCreated: {},
            filesModified: { 'database.ts': {} }
          }
        },
        fileImpacts: {
          'auth.ts': { path: 'auth.ts' },
          'user.ts': { path: 'user.ts' },
          'database.ts': { path: 'database.ts' }
        },
        areaSummaries: [
          { area: 'Backend', taskCount: 10, completedTasks: 8, progress: 80 },
          { area: 'Frontend', taskCount: 10, completedTasks: 5, progress: 50 }
        ],
        specificationVersion: 2,
        specificationContentHash: 'abc123def456'
      };

      this.loading = false;
      this.update();
    } catch (err: any) {
      this.error = err.message;
      this.loading = false;
      this.update();
    }
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  /**
   * Public method to refresh the view
   */
  public refresh(): void {
    if (this.selectedProjectId) {
      this.loadImpactData(this.selectedProjectId);
    } else {
      this.loadProjects();
    }
  }
}

// Register the custom element
define('impact-view', ImpactView);
