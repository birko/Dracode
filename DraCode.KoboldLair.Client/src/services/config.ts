/**
 * Configuration Service
 * Manages application configuration including server URL and auth token
 */

interface Config {
  apiUrl: string;
  wsUrl: string;
  serverUrl: string;
  authToken: string;
  refreshInterval: number;
}

class ConfigService {
  private config: Config = {
    apiUrl: window.location.origin,
    wsUrl: '',
    serverUrl: 'ws://localhost:5000',
    authToken: '',
    refreshInterval: 5000
  };

  private configChangeHandlers: Array<(config: Config) => void> = [];

  constructor() {
    this.loadConfig();
  }

  /**
   * Load configuration from localStorage or use defaults
   */
  private loadConfig(): void {
    try {
      const stored = localStorage.getItem('koboldlair-config');
      if (stored) {
        const parsed = JSON.parse(stored);
        this.config = { ...this.config, ...parsed };
      }
    } catch (error) {
      console.warn('Failed to load config from localStorage:', error);
    }

    // Set wsUrl to match serverUrl
    this.config.wsUrl = this.config.serverUrl;
  }

  /**
   * Save configuration to localStorage
   */
  private saveConfig(): void {
    try {
      localStorage.setItem('koboldlair-config', JSON.stringify(this.config));
    } catch (error) {
      console.error('Failed to save config to localStorage:', error);
    }
  }

  /**
   * Get current configuration
   */
  getConfig(): Config {
    return { ...this.config };
  }

  /**
   * Update server URL
   */
  setServerUrl(url: string): void {
    this.config.serverUrl = url;
    this.config.wsUrl = url;
    this.saveConfig();
    this.notifyConfigChange();
  }

  /**
   * Update auth token
   */
  setAuthToken(token: string): void {
    this.config.authToken = token;
    this.saveConfig();
    this.notifyConfigChange();
  }

  /**
   * Update multiple configuration values
   */
  updateConfig(updates: Partial<Config>): void {
    this.config = { ...this.config, ...updates };
    if (updates.serverUrl) {
      this.config.wsUrl = updates.serverUrl;
    }
    this.saveConfig();
    this.notifyConfigChange();
  }

  /**
   * Subscribe to configuration changes
   */
  onChange(handler: (config: Config) => void): () => void {
    this.configChangeHandlers.push(handler);
    // Return unsubscribe function
    return () => {
      const index = this.configChangeHandlers.indexOf(handler);
      if (index > -1) {
        this.configChangeHandlers.splice(index, 1);
      }
    };
  }

  private notifyConfigChange(): void {
    const configSnapshot = this.getConfig();
    this.configChangeHandlers.forEach(handler => handler(configSnapshot));
  }
}

// Export singleton instance
export const configService = new ConfigService();
export default configService;
