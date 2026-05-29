/**
 * API Client Service
 * WebSocket-based API client for KoboldLair backend communication
 * Uses Birko.Web.Core WsClient for WebSocket connection
 */

import { WsClient } from 'birko-web-core/http';
import configService from './config.js';

interface PendingRequest {
  resolve: (value: any) => void;
  reject: (reason: Error) => void;
  timeout: ReturnType<typeof setTimeout>;
}

export class ApiClient {
  private ws: WsClient | null = null;
  private pendingRequests = new Map<string, PendingRequest>();

  async connect(): Promise<void> {
    if (this.ws) return;

    const config = configService.getConfig();
    const wsUrl = config.serverUrl.replace(/\/$/, '') + '/wyvern';

    this.ws = new WsClient({
      url: wsUrl,
      getToken: () => config.authToken || null,
      reconnectMs: 5000,
      heartbeatMs: 30000,
      maxReconnectAttempts: 5,
      onOpen: () => {
        console.log('API WebSocket connected via Birko.Web.Core');
      }
    });

    // Listen for responses
    this.ws.on('response', (data: any) => {
      const request = this.pendingRequests.get(data.id);
      if (request) {
        clearTimeout(request.timeout);
        request.resolve(data.data);
        this.pendingRequests.delete(data.id);
      }
    });

    // Listen for errors
    this.ws.on('error', (data: any) => {
      if (data && data.id) {
        const request = this.pendingRequests.get(data.id);
        if (request) {
          clearTimeout(request.timeout);
          request.reject(new Error(data.error || 'Unknown error'));
          this.pendingRequests.delete(data.id);
        }
      }
    });

    // Connect
    this.ws.connect();
  }

  async sendCommand(command: string, data: any = null): Promise<any> {
    if (!this.ws) {
      await this.connect();
    }

    const requestId = `req_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

    return new Promise((resolve, reject) => {
      // Timeout after 30 seconds
      const timeout = setTimeout(() => {
        if (this.pendingRequests.has(requestId)) {
          this.pendingRequests.delete(requestId);
          reject(new Error('Request timeout'));
        }
      }, 30000);

      this.pendingRequests.set(requestId, { resolve, reject, timeout });

      // Send the message via WebSocket using sendJson
      this.ws?.sendJson('command', { id: requestId, command, data });
    });
  }

  disconnect(): void {
    // Fail all pending requests
    for (const [id, request] of this.pendingRequests) {
      clearTimeout(request.timeout);
      request.reject(new Error('Client disconnected'));
    }
    this.pendingRequests.clear();

    if (this.ws) {
      this.ws.disconnect();
      this.ws = null;
    }
  }

  isConnected(): boolean {
    // WsClient doesn't expose readyState directly, we assume connected if exists
    return this.ws !== null;
  }

  // Stats & Metrics
  async getStats(): Promise<any> {
    return this.sendCommand('get_stats');
  }

  async getMetrics(timeRangeHours: number = 24): Promise<any> {
    return this.sendCommand('get_metrics', { timeRangeHours });
  }

  async getComparison(projectId: string): Promise<any> {
    return this.sendCommand('get_comparison', { projectId });
  }

  // Projects
  async getProjects(): Promise<any> {
    return this.sendCommand('get_projects');
  }

  async getHierarchy(): Promise<any> {
    return this.sendCommand('get_hierarchy');
  }

  // Providers
  async getProviders(): Promise<any> {
    return this.sendCommand('get_providers');
  }

  // Project Configuration
  async getProjectConfig(projectId: string): Promise<any> {
    return this.sendCommand('get_project_config', { projectId });
  }

  async updateProjectConfig(projectId: string, maxParallelKobolds: number): Promise<any> {
    return this.sendCommand('update_project_config', { projectId, maxParallelKobolds });
  }

  async getProjectProviders(projectId: string): Promise<any> {
    return this.sendCommand('get_project_providers', { projectId });
  }

  async getProjectAgents(projectId: string): Promise<any> {
    return this.sendCommand('get_project_agents', { projectId });
  }

  async updateProjectProviders(
    projectId: string,
    agentType: string,
    providerName: string,
    modelOverride: string | null
  ): Promise<any> {
    return this.sendCommand('update_project_providers', {
      projectId,
      agentType,
      providerName,
      modelOverride
    });
  }

  async toggleAgent(projectId: string, agentType: string, enabled: boolean): Promise<any> {
    return this.sendCommand('toggle_agent', { projectId, agentType, enabled });
  }

  async getAgentStatus(projectId: string, agentType: string): Promise<any> {
    return this.sendCommand('get_agent_status', { projectId, agentType });
  }

  async getImplementationSummary(projectId: string): Promise<any> {
    return this.sendCommand('get_implementation_summary', { projectId });
  }

  // Provider Configuration
  async configureProvider(
    agentType: string,
    providerName: string,
    modelOverride: string | null
  ): Promise<any> {
    return this.sendCommand('configure_provider', { agentType, providerName, modelOverride });
  }

  async validateProvider(providerName: string): Promise<any> {
    return this.sendCommand('validate_provider', { providerName });
  }

  async getProvidersForAgent(agentType: string): Promise<any> {
    return this.sendCommand('get_providers_for_agent', { agentType });
  }

  // Full Project Configuration
  async getAllProjectConfigs(): Promise<any> {
    return this.sendCommand('get_all_project_configs');
  }

  async getProjectConfigFull(projectId: string): Promise<any> {
    return this.sendCommand('get_project_config_full', { projectId });
  }

  async updateProjectConfigFull(projectId: string, config: any): Promise<any> {
    return this.sendCommand('update_project_config_full', { projectId, ...config });
  }

  async deleteProjectConfig(projectId: string): Promise<any> {
    return this.sendCommand('delete_project_config', { projectId });
  }

  // Agent Configuration
  async getAgentConfig(projectId: string, agentType: string): Promise<any> {
    return this.sendCommand('get_agent_config', { projectId, agentType });
  }

  async updateAgentConfig(
    projectId: string,
    agentType: string,
    provider: string,
    model: string | null,
    enabled: boolean
  ): Promise<any> {
    return this.sendCommand('update_agent_config', {
      projectId,
      agentType,
      provider,
      model,
      enabled
    });
  }

  // Analysis Retry
  async retryAnalysis(projectId: string): Promise<any> {
    return this.sendCommand('retry_analysis', { projectId });
  }
}
