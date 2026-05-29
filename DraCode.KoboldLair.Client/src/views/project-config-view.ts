/**
 * Project Config View for KoboldLair
 * Manage project-specific agent configurations and limits
 */

import { BaseComponent, define } from 'birko-web-core';
import toastService from '../services/toast-service.js';

interface AgentConfig {
  enabled?: boolean;
  provider?: string;
  model?: string;
  maxParallel?: number;
  timeout?: number;
}

interface ProjectConfig {
  project?: {
    id: string;
    name: string;
  };
  agents?: {
    wyrm?: AgentConfig;
    wyvern?: AgentConfig;
    drake?: AgentConfig;
    koboldPlanner?: AgentConfig;
    kobold?: AgentConfig;
  };
}

interface Provider {
  name: string;
  displayName: string;
  isEnabled: boolean;
}

/**
 * Project Config View Component
 * Displays and manages project configurations
 */
export class ProjectConfigView extends BaseComponent {
  private configs: ProjectConfig[] = [];
  private defaults: any = {};
  private providers: Provider[] = [];
  private selectedProjectId: string | null = null;
  private loading = true;
  private error: string | null = null;

  static get styles() {
    return `
      :host {
        display: block;
        padding: 20px;
        max-width: 1200px;
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

      .defaults-card {
        margin-bottom: 20px;
      }

      .defaults-grid {
        display: flex;
        gap: 12px;
        flex-wrap: wrap;
      }

      .config-list {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .config-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 16px;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-md, 8px);
        transition: border-color 0.2s ease;
      }

      .config-item:hover {
        border-color: var(--b-color-primary, #3b82f6);
      }

      .config-item-main {
        display: flex;
        align-items: center;
        gap: 12px;
        flex: 1;
      }

      .config-item-icon {
        font-size: 24px;
      }

      .config-item-info {
        flex: 1;
      }

      .config-item-name {
        font-size: 16px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
        margin-bottom: 4px;
      }

      .config-item-limits {
        font-size: 13px;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 6px;
      }

      .config-item-agents {
        display: flex;
        gap: 6px;
        flex-wrap: wrap;
      }

      .config-item-actions {
        display: flex;
        gap: 8px;
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

      .error-state {
        text-align: center;
        padding: 40px 20px;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-color-danger, #ef4444);
        border-radius: var(--b-radius-lg, 12px);
      }

      .error-title {
        font-size: 18px;
        font-weight: 600;
        color: var(--b-color-danger, #ef4444);
        margin-bottom: 8px;
      }

      .error-message {
        font-size: 14px;
        color: var(--b-text-secondary, #6b7280);
      }

      .loading-state {
        text-align: center;
        padding: 60px 20px;
      }
    `;
  }

  render() {
    if (this.loading) {
      return `
        <div class="view-header">
          <h1>⚙️ Project Configurations</h1>
        </div>
        <div style="display: flex; justify-content: center; padding: 60px 20px;">
          <b-spinner size="large" message="Loading configurations..."></b-spinner>
        </div>
      `;
    }

    if (this.error) {
      return `
        <div class="view-header">
          <h1>⚙️ Project Configurations</h1>
        </div>
        <div class="error-state">
          <div class="error-title">Failed to load configurations</div>
          <div class="error-message">${this.escapeHtml(this.error)}</div>
          <div style="margin-top: 16px;">
            <b-button variant="primary" id="retryBtn">Retry</b-button>
          </div>
        </div>
      `;
    }

    return `
      <div class="view-header">
        <h1>⚙️ Project Configurations</h1>
        <p>Manage agent configurations and parallel limits for each project</p>
      </div>

      <b-card header="Default Limits" class="defaults-card">
        <div class="defaults-grid">
          <b-badge variant="info">Kobolds: ${this.defaults.maxParallelKobolds || 1}</b-badge>
          <b-badge variant="info">Drakes: ${this.defaults.maxParallelDrakes || 1}</b-badge>
          <b-badge variant="info">Wyrms: ${this.defaults.maxParallelWyrms || 1}</b-badge>
          <b-badge variant="info">Wyverns: ${this.defaults.maxParallelWyverns || 1}</b-badge>
        </div>
      </b-card>

      ${this.configs.length > 0 ? `
        <div class="config-list">
          ${this.configs.map(config => this.renderConfigItem(config)).join('')}
        </div>
      ` : `
        <div class="empty-state">
          <div class="empty-icon">📁</div>
          <h3>No Project Configurations</h3>
          <p>Create a project via Dragon to add configurations</p>
        </div>
      `}
    `;
  }

  private renderConfigItem(config: ProjectConfig): string {
    const projectId = config.project?.id || '';
    const projectName = config.project?.name || projectId;
    const agents = config.agents || {};

    const koboldLimit = agents.kobold?.maxParallel || 1;
    const drakeLimit = agents.drake?.maxParallel || 1;
    const wyrmLimit = agents.wyrm?.maxParallel || 1;
    const wyvernLimit = agents.wyvern?.maxParallel || 1;

    return `
      <div class="config-item" data-project-id="${projectId}">
        <div class="config-item-main">
          <span class="config-item-icon">📁</span>
          <div class="config-item-info">
            <div class="config-item-name">${this.escapeHtml(projectName)}</div>
            <div class="config-item-limits">
              Limits: ${koboldLimit} Kobolds, ${drakeLimit} Drakes, ${wyrmLimit} Wyrms, ${wyvernLimit} Wyverns
            </div>
            <div class="config-item-agents">
              <b-badge variant="${agents.wyrm?.enabled ? 'success' : 'secondary'}" title="Wyrm: ${agents.wyrm?.provider || 'default'}">Wyrm</b-badge>
              <b-badge variant="${agents.wyvern?.enabled ? 'success' : 'secondary'}" title="Wyvern: ${agents.wyvern?.provider || 'default'}">Wyvern</b-badge>
              <b-badge variant="${agents.drake?.enabled ? 'success' : 'secondary'}" title="Drake: ${agents.drake?.provider || 'default'}">Drake</b-badge>
              <b-badge variant="${agents.koboldPlanner?.enabled ? 'success' : 'secondary'}" title="Planner: ${agents.koboldPlanner?.provider || 'default'}">Planner</b-badge>
              <b-badge variant="${agents.kobold?.enabled ? 'success' : 'secondary'}" title="Kobold: ${agents.kobold?.provider || 'default'}">Kobold</b-badge>
            </div>
          </div>
        </div>
        <div class="config-item-actions">
          <b-button variant="primary" size="sm" data-action="edit" data-project-id="${projectId}">Edit</b-button>
          <b-button variant="secondary" size="sm" data-action="delete" data-project-id="${projectId}">Delete</b-button>
        </div>
      </div>
    `;
  }

  protected onMount() {
    this.loadConfigs();

    this.$$('b-button[data-action="edit"]').forEach(btn => {
      btn.addEventListener('click', () => {
        const projectId = btn.getAttribute('data-project-id');
        this.openEditModal(projectId!);
      });
    });

    this.$$('b-button[data-action="delete"]').forEach(btn => {
      btn.addEventListener('click', () => {
        const projectId = btn.getAttribute('data-project-id');
        this.deleteConfig(projectId!);
      });
    });

    this.$('#retryBtn')?.addEventListener('click', () => {
      this.loadConfigs();
    });
  }

  protected onUpdated() {
    this.onMount();
  }

  private async loadConfigs() {
    this.loading = true;
    this.error = null;
    this.update();

    try {
      // TODO: Call actual API
      // const [configData, providerData] = await Promise.all([
      //   this.api.getAllProjectConfigs(),
      //   this.api.getProviders()
      // ]);

      // Mock data for now
      await new Promise(resolve => setTimeout(resolve, 1000));

      this.configs = [];
      this.defaults = {
        maxParallelKobolds: 4,
        maxParallelDrakes: 1,
        maxParallelWyrms: 1,
        maxParallelWyverns: 1
      };
      this.providers = [];

      this.loading = false;
      this.update();
    } catch (err: any) {
      this.error = err.message || 'Failed to load configurations';
      this.loading = false;
      this.update();
    }
  }

  private openEditModal(projectId: string) {
    this.selectedProjectId = projectId;
    const config = this.configs.find(c => c.project?.id === projectId);
    if (!config) return;

    // TODO: Open edit modal using b-modal
    console.log('Open edit modal for:', projectId);
    this.emit('edit-config', { projectId, config });
  }

  private async deleteConfig(projectId: string) {
    if (!confirm('Delete this project configuration?')) {
      return;
    }

    try {
      // TODO: Call actual API
      // await this.api.deleteProjectConfig(projectId);

      this.configs = this.configs.filter(c => c.project?.id !== projectId);
      this.update();
      this.showMessage('Configuration deleted successfully', 'success');
    } catch (err: any) {
      this.showMessage(`Failed to delete: ${err.message}`, 'error');
    }
  }

  private showMessage(message: string, type: 'success' | 'error' | 'info') {
    // Use toast notification service
    toastService.show({ message, variant: type as any });
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
    this.loadConfigs();
  }
}

// Register the custom element
define('project-config-view', ProjectConfigView);
