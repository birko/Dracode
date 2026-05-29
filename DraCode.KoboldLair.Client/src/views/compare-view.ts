/**
 * Compare View for KoboldLair
 * Side-by-side comparison of task plans and execution results
 */

import { BaseComponent, define } from 'birko-web-core';

interface TaskComparison {
  taskId: string;
  taskName: string;
  plans: PlanComparison[];
}

interface PlanComparison {
  planId: string;
  planName: string;
  agentType: string;
  status: string;
  duration: string;
  steps: StepComparison[];
  success: boolean;
  result?: string;
}

interface StepComparison {
  stepId: string;
  description: string;
  plans: {
    planA: string;
    planB?: string;
  };
  outcome: 'same' | 'different' | 'a_only' | 'b_only';
}

/**
 * Compare View Component
 * Compare task executions side by side
 */
export class CompareView extends BaseComponent {
  private _comparisons: TaskComparison[] = [];
  private _selectedTask: TaskComparison | null = null;
  private _viewMode: 'side-by-side' | 'unified' = 'side-by-side';

  static get styles() {
    return `
      :host {
        display: block;
        padding: 20px;
        max-width: 1600px;
        margin: 0 auto;
      }

      .compare-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
      }

      .compare-header h1 {
        font-size: 28px;
        font-weight: 600;
        margin: 0;
        color: var(--b-text, #1f2937);
      }

      .view-controls {
        display: flex;
        gap: 12px;
        align-items: center;
      }

      .task-selector {
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-md, 8px);
        padding: 12px 16px;
        margin-bottom: 20px;
      }

      .comparison-container {
        display: grid;
        gap: 20px;
      }

      .comparison-container.side-by-side {
        grid-template-columns: 1fr 1fr;
      }

      .comparison-container.unified {
        grid-template-columns: 1fr;
      }

      .comparison-panel {
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-lg, 12px);
        padding: 20px;
      }

      .panel-header {
        margin-bottom: 16px;
        padding-bottom: 12px;
        border-bottom: 1px solid var(--b-border, #e5e7eb);
      }

      .panel-title {
        font-size: 16px;
        font-weight: 600;
        margin-bottom: 4px;
        color: var(--b-text, #1f2937);
      }

      .panel-meta {
        font-size: 13px;
        color: var(--b-text-secondary, #6b7280);
      }

      .steps-list {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .step-item {
        padding: 12px;
        background: var(--b-bg-secondary, #f9fafb);
        border-radius: var(--b-radius-md, 8px);
        border-left: 3px solid var(--b-border, #e5e7eb);
      }

      .step-item.same {
        border-left-color: var(--b-color-success, #10b981);
      }

      .step-item.different {
        border-left-color: var(--b-color-warning, #f59e0b);
      }

      .step-item.a_only,
      .step-item.b_only {
        border-left-color: var(--b-color-info, #06b6d4);
      }

      .step-description {
        font-size: 14px;
        color: var(--b-text, #1f2937);
        margin-bottom: 8px;
      }

      .step-outcome {
        font-size: 12px;
        font-weight: 600;
        padding: 4px 8px;
        border-radius: var(--b-radius-sm, 4px);
        display: inline-block;
      }

      .step-outcome.same {
        background: var(--b-color-success, #10b981);
        color: white;
      }

      .step-outcome.different {
        background: var(--b-color-warning, #f59e0b);
        color: white;
      }

      .step-outcome.a_only {
        background: var(--b-color-info, #06b6d4);
        color: white;
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
    `;
  }

  render() {
    return `
      <div class="compare-header">
        <h1>⚖️ Compare Executions</h1>
        <div class="view-controls">
          <label style="font-size: 14px; color: var(--b-text-secondary);">View:</label>
          <b-button variant="${this._viewMode === 'side-by-side' ? 'primary' : 'ghost'}" size="sm" data-view="side-by-side">Side by Side</b-button>
          <b-button variant="${this._viewMode === 'unified' ? 'primary' : 'ghost'}" size="sm" data-view="unified">Unified</b-button>
        </div>
      </div>

      ${!this._selectedTask ? this.renderTaskSelector() : ''}

      ${this._selectedTask ? `
        <div class="comparison-container ${this._viewMode}">
          ${this.renderComparison(this._selectedTask)}
        </div>
      ` : `
        <div class="empty-state">
          <div class="empty-icon">⚖️</div>
          <h3>No Comparison Selected</h3>
          <p>Select a task to compare different execution plans</p>
        </div>
      `}
    `;
  }

  private renderTaskSelector(): string {
    return `
      <div class="task-selector">
        <h3 style="margin: 0 0 12px 0; font-size: 16px; color: var(--b-text);">Available Comparisons</h3>
        <div style="display: flex; flex-direction: column; gap: 8px;">
          ${this._comparisons.map(comp => `
            <b-button
              variant="ghost"
              size="sm"
              data-task-id="${comp.taskId}"
              style="justify-content: flex-start;">
              ${comp.taskName}
            </b-button>
          `).join('')}
        </div>
      </div>
    `;
  }

  private renderComparison(task: TaskComparison): string {
    if (this._viewMode === 'side-by-side' && task.plans.length >= 2) {
      return `
        ${this.renderPanel(task.plans[0], 'Plan A')}
        ${this.renderPanel(task.plans[1], 'Plan B')}
      `;
    } else {
      return this.renderUnifiedComparison(task);
    }
  }

  private renderPanel(plan: PlanComparison, label: string): string {
    return `
      <div class="comparison-panel">
        <div class="panel-header">
          <div class="panel-title">${label}: ${this.escapeHtml(plan.planName)}</div>
          <div class="panel-meta">
            <span>Agent: ${plan.agentType}</span>
            <span>•</span>
            <span>Status: <b-badge variant="${this.getStatusVariant(plan.status)}">${plan.status}</b-badge></span>
          </div>
        </div>

        ${plan.result ? `
          <div style="margin-top: 12px; padding: 12px; background: var(--b-bg-secondary, #f9fafb); border-radius: 8px;">
            <div style="font-size: 13px; font-weight: 600; margin-bottom: 4px;">Result:</div>
            <div style="font-size: 14px; color: var(--b-text, #1f2937);">${this.escapeHtml(plan.result)}</div>
          </div>
        ` : ''}

        <div class="steps-list">
          ${plan.steps.map((step, index) => `
            <div class="step-item ${step.outcome}">
              <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 4px;">
                <span style="font-size: 12px; color: var(--b-text-muted);">${index + 1}.</span>
                <span class="step-outcome ${step.outcome}">${step.outcome.toUpperCase()}</span>
              </div>
              <div class="step-description">${this.escapeHtml(step.description)}</div>
            </div>
          `).join('')}
        </div>
      </div>
    `;
  }

  private renderUnifiedComparison(task: TaskComparison): string {
    return `
      <div class="comparison-panel" style="grid-column: 1 / -1;">
        <div class="panel-header">
          <div class="panel-title">${this.escapeHtml(task.taskName)}</div>
          <div class="panel-meta">${task.plans.length} execution plans to compare</div>
        </div>

        ${task.plans.map(plan => `
          <div style="padding: 16px; margin-bottom: 16px; background: var(--b-bg-secondary, #f9fafb); border-radius: 8px;">
            <div style="font-weight: 600; margin-bottom: 8px;">${plan.planName}</div>
            <div style="font-size: 13px; color: var(--b-text-secondary); margin-bottom: 8px;">
              Agent: ${plan.agentType} • Duration: ${plan.duration}
            </div>
            <div style="font-size: 14px;">
              ${plan.steps.slice(0, 3).map((s, i) => `${i + 1}. ${s.description}`).join('<br>')}
              ${plan.steps.length > 3 ? `<div style="color: var(--b-text-muted); font-size: 12px;">+ ${plan.steps.length - 3} more steps</div>` : ''}
            </div>
          </div>
        `).join('')}
      </div>
    `;
  }

  private getStatusVariant(status: string): string {
    const variantMap: Record<string, string> = {
      'completed': 'success',
      'running': 'info',
      'failed': 'danger',
      'pending': 'secondary'
    };
    return variantMap[status] || 'secondary';
  }

  protected onMount() {
    // Load comparison data
    this.loadComparisons();

    // Set up event listeners
    this.$$('b-button[data-view]').forEach(btn => {
      btn.addEventListener('click', () => {
        this._viewMode = btn.getAttribute('data-view') as any;
        this.update();
      });
    });

    // Task selector
    this.$$('b-button[data-task-id]').forEach(btn => {
      btn.addEventListener('click', () => {
        const taskId = btn.getAttribute('data-task-id');
        this._selectedTask = this._comparisons.find(c => c.taskId === taskId) || null;
        this.update();
      });
    });
  }

  protected onUpdated() {
    this.onMount();
  }

  private async loadComparisons() {
    // TODO: Load from actual API
    // For now, use mock data
    this._comparisons = [
      {
        taskId: 'task-1',
        taskName: 'Implement User Authentication',
        plans: [
          {
            planId: 'plan-1a',
            planName: 'Conservative Approach',
            agentType: 'csharp',
            status: 'completed',
            duration: '12 mins',
            success: true,
            result: 'Successfully implemented JWT authentication with refresh tokens',
            steps: [
              {
                stepId: '1',
                description: 'Design authentication models',
                plans: { planA: 'User, Role, Permission models', planB: 'User, Role models' },
                outcome: 'same'
              },
              {
                stepId: '2',
                description: 'Implement JWT token generation',
                plans: { planA: 'Using System.IdentityModel', planB: 'Using custom JWT library' },
                outcome: 'different'
              },
              {
                stepId: '3',
                description: 'Create login endpoint',
                plans: { planA: 'POST /api/auth/login', planB: 'POST /api/login' },
                outcome: 'different'
              }
            ]
          },
          {
            planId: 'plan-1b',
            planName: 'Comprehensive Approach',
            agentType: 'python',
            status: 'completed',
            duration: '15 mins',
            success: true,
            result: 'Successfully implemented authentication with OAuth2 integration',
            steps: [
              {
                stepId: '1',
                description: 'Design authentication models',
                plans: { planA: 'User, Role, Permission models', planB: 'User, Role models' },
                outcome: 'different'
              },
              {
                stepId: '2',
                description: 'Implement JWT token generation',
                plans: { planA: 'Using System.IdentityModel', planB: 'Using PyJWT library' },
                outcome: 'different'
              },
              {
                stepId: '3',
                description: 'Create login endpoint',
                plans: { planA: 'POST /api/auth/login', planB: 'POST /auth/login' },
                outcome: 'different'
              }
            ]
          }
        ]
      }
    ];

    this.update();
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
    this.loadComparisons();
  }
}

// Register the custom element
define('kobold-compare-view', CompareView);
