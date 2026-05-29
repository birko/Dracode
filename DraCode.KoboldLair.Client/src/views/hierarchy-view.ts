/**
 * Hierarchy View for KoboldLair
 * Displays agent hierarchy using b-tree-menu with real-time status
 */

import { BaseComponent, define } from 'birko-web-core';

interface AgentNode {
  id: string;
  name: string;
  type: 'dragon' | 'wyrm' | 'wyvern' | 'drake' | 'kobold' | 'kobold-planner';
  status: 'idle' | 'running' | 'completed' | 'failed';
  projectId?: string;
  children?: AgentNode[];
  expanded?: boolean;
}

interface AgentDetails {
  id: string;
  name: string;
  type: string;
  status: string;
  runtime: string;
  memory: string;
  lastActivity: string;
}

/**
 * Hierarchy View Component
 * Shows the agent hierarchy with status indicators
 */
export class HierarchyView extends BaseComponent {
  private _hierarchy: AgentNode[] = [];
  private _selectedAgent: string | null = null;

  static get styles() {
    return `
      :host {
        display: block;
        padding: 20px;
        max-width: 1600px;
        margin: 0 auto;
      }

      .hierarchy-container {
        display: flex;
        gap: 24px;
        height: calc(100vh - 100px);
      }

      .tree-panel {
        flex: 0 0 400px;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-lg, 12px);
        padding: 20px;
        overflow-y: auto;
      }

      .details-panel {
        flex: 1;
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-lg, 12px);
        padding: 24px;
        overflow-y: auto;
      }

      .panel-header {
        margin-bottom: 24px;
      }

      .panel-header h1 {
        font-size: 24px;
        font-weight: 600;
        margin: 0 0 8px 0;
        color: var(--b-text, #1f2937);
      }

      .panel-header p {
        font-size: 14px;
        color: var(--b-text-secondary, #6b7280);
        margin: 0;
      }

      .tree-controls {
        display: flex;
        gap: 8px;
        margin-bottom: 16px;
      }

      .agent-details {
        display: none;
      }

      .agent-details.active {
        display: block;
      }

      .details-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
        padding-bottom: 16px;
        border-bottom: 1px solid var(--b-border, #e5e7eb);
      }

      .agent-name {
        font-size: 28px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
        margin: 0 0 8px 0;
      }

      .agent-type {
        display: inline-block;
        padding: 4px 12px;
        background: var(--b-bg-tertiary, #f3f4f6);
        border-radius: var(--b-radius-full, 9999px);
        font-size: 12px;
        font-weight: 600;
        color: var(--b-text-secondary, #6b7280);
        text-transform: uppercase;
      }

      .details-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
        gap: 16px;
        margin-bottom: 24px;
      }

      .detail-item {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }

      .detail-label {
        font-size: 12px;
        font-weight: 600;
        color: var(--b-text-secondary, #6b7280);
        text-transform: uppercase;
      }

      .detail-value {
        font-size: 16px;
        color: var(--b-text, #1f2937);
      }

      .status-badge {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 6px 12px;
        border-radius: var(--b-radius-md, 8px);
        font-size: 13px;
        font-weight: 500;
      }

      .status-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        animation: pulse 2s ease-in-out infinite;
      }

      .status-badge.running {
        background: var(--b-color-info, #06b6d4);
        color: white;
      }

      .status-badge.running .status-dot {
        background: white;
      }

      .status-badge.completed {
        background: var(--b-color-success, #10b981);
        color: white;
      }

      .status-badge.completed .status-dot {
        background: white;
      }

      .status-badge.failed {
        background: var(--b-color-danger, #ef4444);
        color: white;
      }

      .status-badge.failed .status-dot {
        background: white;
      }

      .status-badge.idle {
        background: var(--b-color-secondary, #9ca3af);
        color: white;
      }

      @keyframes pulse {
        0%, 100% { opacity: 1; }
        50% { opacity: 0.5; }
      }

      .actions-section {
        margin-top: 24px;
        padding-top: 24px;
        border-top: 1px solid var(--b-border, #e5e7eb);
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
      <div class="hierarchy-container">
        <div class="tree-panel">
          <div class="panel-header">
            <h1>🌳 Agent Hierarchy</h1>
            <p>Dragon → Wyrm → Wyvern → Drake → Kobold Planner → Kobold</p>
          </div>

          <div class="tree-controls">
            <b-button variant="ghost" size="sm" id="expandAll">Expand All</b-button>
            <b-button variant="ghost" size="sm" id="collapseAll">Collapse All</b-button>
            <b-button variant="primary" size="sm" id="refresh">Refresh</b-button>
          </div>

          <b-tree-menu id="agent-tree"></b-tree-menu>
        </div>

        <div class="details-panel">
          <div class="panel-header">
            <h1>Agent Details</h1>
            <p>Select an agent to view details</p>
          </div>

          <div id="details-content">
            ${this._selectedAgent ? this.renderAgentDetails() : this.renderEmptyState()}
          </div>
        </div>
      </div>
    `;
  }

  private renderAgentDetails(): string {
    const agent = this.getAgentDetails(this._selectedAgent!);
    if (!agent) {
      return this.renderEmptyState();
    }

    const statusClass = agent.status.toLowerCase();

    return `
      <div class="agent-details active">
        <div class="details-header">
          <div>
            <h2 class="agent-name">${this.escapeHtml(agent.name)}</h2>
            <span class="agent-type">${agent.type}</span>
          </div>
          <div class="status-badge ${statusClass}">
            <span class="status-dot"></span>
            ${agent.status}
          </div>
        </div>

        <div class="details-grid">
          <div class="detail-item">
            <div class="detail-label">Agent ID</div>
            <div class="detail-value">${agent.id}</div>
          </div>
          <div class="detail-item">
            <div class="detail-label">Runtime</div>
            <div class="detail-value">${agent.runtime}</div>
          </div>
          <div class="detail-item">
            <div class="detail-label">Memory Usage</div>
            <div class="detail-value">${agent.memory}</div>
          </div>
          <div class="detail-item">
            <div class="detail-label">Last Activity</div>
            <div class="detail-value">${agent.lastActivity}</div>
          </div>
        </div>

        <div class="actions-section">
          <div style="display: flex; gap: 12px;">
            <b-button variant="primary" id="viewLogs">View Logs</b-button>
            <b-button variant="secondary" id="viewPlan">View Plan</b-button>
            ${agent.status === 'running' ? '<b-button variant="danger" id="stopAgent">Stop</b-button>' : ''}
          </div>
        </div>
      </div>
    `;
  }

  private renderEmptyState(): string {
    return `
      <div class="empty-state">
        <div class="empty-icon">🌳</div>
        <h3>No Agent Selected</h3>
        <p>Select an agent from the tree to view its details</p>
      </div>
    `;
  }

  protected onMount() {
    // Load hierarchy data
    this.loadHierarchy();

    // Set up tree menu
    this.setupTreeMenu();

    // Set up event listeners
    this.$('#expandAll')?.addEventListener('click', () => this.expandAll());
    this.$('#collapseAll')?.addEventListener('click', () => this.collapseAll());
    this.$('#refresh')?.addEventListener('click', () => this.refresh());
  }

  protected onUpdated() {
    this.onMount();
  }

  private async loadHierarchy() {
    // TODO: Load from actual API
    // For now, use mock hierarchy
    this._hierarchy = [
      {
        id: 'dragon-1',
        name: 'Dragon (Interactive)',
        type: 'dragon',
        status: 'idle',
        expanded: true,
        children: [
          {
            id: 'wyrm-1',
            name: 'Wyrm (Pre-Analysis)',
            type: 'wyrm',
            status: 'idle',
            children: [
              {
                id: 'wyvern-1',
                name: 'Wyvern (Analyzer)',
                type: 'wyvern',
                status: 'idle',
                children: [
                  {
                    id: 'drake-1',
                    name: 'Drake (Supervisor)',
                    type: 'drake',
                    status: 'running',
                    expanded: true,
                    projectId: 'project-1',
                    children: [
                      {
                        id: 'kobold-planner-1',
                        name: 'Kobold Planner',
                        type: 'kobold-planner',
                        status: 'idle',
                        children: []
                      },
                      {
                        id: 'kobold-1',
                        name: 'Kobold (Frontend)',
                        type: 'kobold',
                        status: 'running',
                        projectId: 'project-1'
                      },
                      {
                        id: 'kobold-2',
                        name: 'Kobold (Backend)',
                        type: 'kobold',
                        status: 'completed',
                        projectId: 'project-1'
                      }
                    ]
                  }
                ]
              }
            ]
          }
        ]
      }
    ];

    this.update();
  }

  private setupTreeMenu() {
    const tree = this.$<any>('#agent-tree');

    if (tree && typeof tree.setItems === 'function') {
      tree.setItems(this._hierarchy);
      tree.addEventListener('select', (e: CustomEvent) => {
        this._selectedAgent = e.detail.id;
        this.update();
      });
    }
  }

  private getAgentDetails(agentId: string): AgentDetails | null {
    // Mock details based on agent ID
    const mockDetails: Record<string, AgentDetails> = {
      'dragon-1': {
        id: 'dragon-1',
        name: 'Dragon (Interactive)',
        type: 'Dragon Agent',
        status: 'Idle',
        runtime: 'N/A',
        memory: 'N/A',
        lastActivity: 'Waiting for user input'
      },
      'kobold-1': {
        id: 'kobold-1',
        name: 'Kobold (Frontend)',
        type: 'Kobold Agent',
        status: 'Running',
        runtime: '45 mins',
        memory: '256 MB',
        lastActivity: '2 mins ago'
      },
      'kobold-2': {
        id: 'kobold-2',
        name: 'Kobold (Backend)',
        type: 'Kobold Agent',
        status: 'Completed',
        runtime: '32 mins',
        memory: '180 MB',
        lastActivity: '15 mins ago'
      },
      'drake-1': {
        id: 'drake-1',
        name: 'Drake (Supervisor)',
        type: 'Drake Supervisor',
        status: 'Running',
        runtime: '1 hr 20 mins',
        memory: '512 MB',
        lastActivity: 'Just now'
      }
    };

    return mockDetails[agentId] || null;
  }

  private expandAll() {
    const tree = this.$<any>('#agent-tree');
    if (tree && typeof tree.expandAll === 'function') {
      tree.expandAll();
    }
  }

  private collapseAll() {
    const tree = this.$<any>('#agent-tree');
    if (tree && typeof tree.collapseAll === 'function') {
      tree.collapseAll();
    }
  }

  private handleViewLogs() {
    console.log('View logs for agent:', this._selectedAgent);
    this.emit('view-logs', { agentId: this._selectedAgent });
  }

  private handleViewPlan() {
    console.log('View plan for agent:', this._selectedAgent);
    this.emit('view-plan', { agentId: this._selectedAgent });
  }

  private handleStopAgent() {
    console.log('Stop agent:', this._selectedAgent);
    this.emit('stop-agent', { agentId: this._selectedAgent });
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
    this.loadHierarchy();
  }
}

// Register the custom element
define('kobold-hierarchy-view', HierarchyView);
