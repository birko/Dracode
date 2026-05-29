/**
 * Metrics View for KoboldLair
 * Displays cost, performance, and usage metrics with b-chart components
 */

import { BaseComponent, define } from 'birko-web-core';

interface MetricData {
  label: string;
  value: number;
  timestamp: string;
}

interface CostData {
  period: string;
  totalCost: number;
  costByProvider: ProviderCost[];
}

interface ProviderCost {
  provider: string;
  cost: number;
  requests: number;
}

/**
 * Metrics View Component
 * Shows LLM usage costs, performance metrics, and statistics
 */
export class MetricsView extends BaseComponent {
  private _timeRange: '24h' | '7d' | '30d' = '7d';
  private _costData: CostData[] = [];
  private _performanceData: MetricData[] = [];

  static get styles() {
    return `
      :host {
        display: block;
        padding: 20px;
        max-width: 1400px;
        margin: 0 auto;
      }

      .metrics-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
      }

      .metrics-header h1 {
        font-size: 28px;
        font-weight: 600;
        margin: 0;
        color: var(--b-text, #1f2937);
      }

      .time-range-selector {
        display: flex;
        gap: 8px;
      }

      .metrics-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
        gap: 20px;
        margin-bottom: 32px;
      }

      .metric-title {
        font-size: 14px;
        font-weight: 600;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 8px;
        display: flex;
        align-items: center;
        gap: 6px;
      }

      .metric-value {
        font-size: 36px;
        font-weight: 700;
        color: var(--b-text, #1f2937);
        margin-bottom: 4px;
      }

      .metric-change {
        font-size: 13px;
        font-weight: 500;
      }

      .metric-change.positive {
        color: var(--b-color-success, #10b981);
      }

      .metric-change.negative {
        color: var(--b-color-danger, #ef4444);
      }

      .chart-container {
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-lg, 12px);
        padding: 24px;
        margin-bottom: 20px;
      }

      .chart-title {
        font-size: 18px;
        font-weight: 600;
        margin-bottom: 16px;
        color: var(--b-text, #1f2937);
      }

      .provider-breakdown {
        margin-top: 24px;
      }

      .provider-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 12px 0;
        border-bottom: 1px solid var(--b-bg-tertiary, #f3f4f6);
      }

      .provider-row:last-child {
        border-bottom: none;
      }

      .provider-name {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 14px;
        font-weight: 500;
        color: var(--b-text, #1f2937);
      }

      .provider-bar {
        flex: 1;
        height: 8px;
        background: var(--b-bg-tertiary, #f3f4f6);
        border-radius: var(--b-radius-full, 9999px);
        overflow: hidden;
        margin: 0 16px;
      }

      .provider-bar-fill {
        height: 100%;
        background: var(--b-color-primary, #3b82f6);
        border-radius: var(--b-radius-full, 9999px);
        transition: width 0.3s ease;
      }

      .provider-stats {
        display: flex;
        gap: 16px;
        font-size: 13px;
        color: var(--b-text-secondary, #6b7280);
      }

      .empty-state {
        text-align: center;
        padding: 60px 20px;
        color: var(--b-text-secondary, #6b7280);
      }
    `;
  }

  render() {
    const stats = this.getOverallStats();
    const totalCost = this.getTotalCost();

    return `
      <div class="metrics-header">
        <h1>📊 Metrics & Analytics</h1>
        <div class="time-range-selector">
          <b-button variant="${this._timeRange === '24h' ? 'primary' : 'ghost'}" size="sm" data-range="24h">24h</b-button>
          <b-button variant="${this._timeRange === '7d' ? 'primary' : 'ghost'}" size="sm" data-range="7d">7d</b-button>
          <b-button variant="${this._timeRange === '30d' ? 'primary' : 'ghost'}" size="sm" data-range="30d">30d</b-button>
        </div>
      </div>

      <div class="metrics-grid">
        <b-card padding="xl" class="metric-card">
          <div class="metric-title">
            <span>💰</span>
            Total Cost
          </div>
          <div class="metric-value">$${totalCost.toFixed(2)}</div>
          <div class="metric-change ${this.getCostChangeTrend()}">
            ${this.getCostChangeText()}
          </div>
        </b-card>

        <b-card padding="xl" class="metric-card">
          <div class="metric-title">
            <span>📝</span>
            Total Requests
          </div>
          <div class="metric-value">${stats.totalRequests.toLocaleString()}</div>
          <div class="metric-change positive">
            +${stats.requestIncrease}% from last period
          </div>
        </b-card>

        <b-card padding="xl" class="metric-card">
          <div class="metric-title">
            <span>⚡</span>
            Avg Response Time
          </div>
          <div class="metric-value">${stats.avgResponseTime}ms</div>
          <div class="metric-change ${stats.responseTimeTrend === 'down' ? 'positive' : 'negative'}">
            ${stats.responseTimeChange}% from last period
          </div>
        </b-card>

        <b-card padding="xl" class="metric-card">
          <div class="metric-title">
            <span>🤖</span>
            Active Agents
          </div>
          <div class="metric-value">${stats.activeAgents}</div>
          <div class="metric-change positive">
            ${stats.agentChange}% increase
          </div>
        </b-card>
      </div>

      <div class="chart-container">
        <div class="chart-title">Cost Over Time</div>
        <b-chart
          id="cost-chart"
          type="line"
          height="300"
          legend="true">
        </b-chart>
      </div>

      <div class="chart-container">
        <div class="chart-title">Cost by Provider</div>
        <div class="provider-breakdown">
          ${this.renderProviderBreakdown()}
        </div>
      </div>

      <div class="chart-container">
        <div class="chart-title">Requests Over Time</div>
        <b-chart
          id="requests-chart"
          type="bar"
          height="250"
          legend="false">
        </b-chart>
      </div>
    `;
  }

  private renderProviderBreakdown(): string {
    const breakdown = this.getProviderBreakdown();
    const maxCost = Math.max(...breakdown.map(p => p.cost));

    return breakdown.map(provider => {
      const percentage = (provider.cost / maxCost) * 100;

      return `
        <div class="provider-row">
          <div class="provider-name">
            <span>${this.getProviderIcon(provider.provider)}</span>
            ${provider.provider}
          </div>
          <div class="provider-bar">
            <div class="provider-bar-fill" style="width: ${percentage}%"></div>
          </div>
          <div class="provider-stats">
            <span>$${provider.cost.toFixed(2)}</span>
            <span>${provider.requests.toLocaleString()} requests</span>
          </div>
        </div>
      `;
    }).join('');
  }

  private getProviderIcon(provider: string): string {
    const icons: Record<string, string> = {
      'openai': '🤖',
      'claude': '🧠',
      'gemini': '✨',
      'ollama': '🦙',
      'azure': '☁️'
    };
    return icons[provider.toLowerCase()] || '🔷';
  }

  private getOverallStats() {
    return {
      totalRequests: 15847,
      requestIncrease: 12.5,
      avgResponseTime: 850,
      responseTimeTrend: 'down',
      responseTimeChange: -8.3,
      activeAgents: 7,
      agentChange: 16
    };
  }

  private getTotalCost(): number {
    return this._costData.reduce((sum, data) => sum + data.totalCost, 0);
  }

  private getCostChangeTrend(): string {
    // Calculate trend (mock for now)
    return 'positive';
  }

  private getCostChangeText(): string {
    return '+$12.50 from last period';
  }

  private getProviderBreakdown(): ProviderCost[] {
    // Aggregate from all cost data
    const aggregated = new Map<string, ProviderCost>();

    this._costData.forEach(data => {
      data.costByProvider.forEach(providerCost => {
        const existing = aggregated.get(providerCost.provider);
        if (existing) {
          existing.cost += providerCost.cost;
          existing.requests += providerCost.requests;
        } else {
          aggregated.set(providerCost.provider, { ...providerCost });
        }
      });
    });

    return Array.from(aggregated.values()).sort((a, b) => b.cost - a.cost);
  }

  protected onMount() {
    // Load metrics data
    this.loadMetrics();

    // Set up time range selector
    this.$$('b-button[data-range]').forEach(btn => {
      btn.addEventListener('click', () => {
        this._timeRange = btn.getAttribute('data-range') as any;
        this.update();
      });
    });

    // Initialize charts when ready
    setTimeout(() => {
      this.initializeCharts();
    }, 100);
  }

  protected onUpdated() {
    this.onMount();
  }

  private async loadMetrics() {
    // TODO: Load from actual API
    // For now, use mock data
    this._costData = [
      {
        period: '7d',
        totalCost: 45.67,
        costByProvider: [
          { provider: 'Claude', cost: 22.80, requests: 3245 },
          { provider: 'GPT-4', cost: 15.42, requests: 1243 },
          { provider: 'Gemini', cost: 7.45, requests: 2156 }
        ]
      }
    ];

    this._performanceData = [
      { label: 'Mon', value: 1234, timestamp: '2024-01-15' },
      { label: 'Tue', value: 1456, timestamp: '2024-01-16' },
      { label: 'Wed', value: 1389, timestamp: '2024-01-17' },
      { label: 'Thu', value: 1678, timestamp: '2024-01-18' },
      { label: 'Fri', value: 1523, timestamp: '2024-01-19' },
      { label: 'Sat', value: 1892, timestamp: '2024-01-20' },
      { label: 'Sun', value: 1123, timestamp: '2024-01-21' }
    ];
  }

  private initializeCharts() {
    // Initialize cost chart
    const costChart = this.$<any>('#cost-chart');
    if (costChart && typeof costChart.setData === 'function') {
      costChart.setData({
        labels: this._performanceData.map(d => d.label),
        series: [
          {
            id: 'cost',
            label: 'Cost ($)',
            data: this._performanceData.map(d => ({ y: d.value * 0.025 })),
          }
        ]
      });
    }

    // Initialize requests chart
    const requestsChart = this.$<any>('#requests-chart');
    if (requestsChart && typeof requestsChart.setData === 'function') {
      requestsChart.setData({
        labels: this._performanceData.map(d => d.label),
        series: [
          {
            id: 'requests',
            label: 'Requests',
            data: this._performanceData.map(d => ({ y: d.value }))
          }
        ]
      });
    }
  }

  /**
   * Public methods
   */
  public refresh(): void {
    this.loadMetrics();
    this.initializeCharts();
  }
}

// Register the custom element
define('kobold-metrics-view', MetricsView);
