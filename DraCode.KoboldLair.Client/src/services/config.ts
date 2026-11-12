/**
 * Configuration Service
 * Manages application configuration: server URL and settings. The token lives in the auth store (auth-store.ts,
 * TASK-114), never here.
 */

interface Config {
  apiUrl: string;
  wsUrl: string;
  serverUrl: string;
  refreshInterval: number;
  /** Whether the server requires sign-in (KoboldLair:AuthRequired on the client host); gates the route guard. */
  authRequired: boolean;
}

class ConfigService {
  private config: Config = {
    apiUrl: window.location.origin,
    wsUrl: '',
    serverUrl: 'ws://localhost:57087',
    refreshInterval: 5000,
    authRequired: false
  };

  private configChangeHandlers: Array<(config: Config) => void> = [];

  /** Explicit user overrides persisted by the server-selector (localStorage). These win over the
   *  server-provided defaults from /api/config, so a user's chosen server is never clobbered. */
  private storedOverrides: Partial<Config> = {};
  private initialized = false;

  constructor() {
    this.loadConfig();
  }

  /**
   * Load the persisted user overrides from localStorage and apply them over the hardcoded defaults.
   * Runs synchronously in the constructor so getConfig() is usable before init() resolves.
   */
  private loadConfig(): void {
    try {
      const stored = localStorage.getItem('koboldlair-config');
      if (stored) {
        // A token stored here by an earlier client is not config: auth-store.ts migrates it (TASK-114), and dropping
        // it here keeps a later save from writing it back.
        const { authToken: _legacyToken, ...overrides } = JSON.parse(stored) as Partial<Config> & { authToken?: unknown };
        this.storedOverrides = overrides;
        this.config = { ...this.config, ...this.storedOverrides };
      }
    } catch (error) {
      console.warn('Failed to load config from localStorage:', error);
    }

    // Set wsUrl to match serverUrl
    this.config.wsUrl = this.config.serverUrl;
  }

  /**
   * Fetch host-provided configuration from GET /api/config (served by the Client host from its .NET
   * config — the single source of truth for the default serverUrl and authRequired). Await this once at
   * bootstrap BEFORE anything connects. Precedence: hardcoded fallback < server config < explicit user
   * override (localStorage), so the host config sets the default while a user's chosen server still wins.
   * Network/parse failures are non-fatal — the app falls back to the local defaults.
   */
  async init(): Promise<void> {
    if (this.initialized) return;
    try {
      const res = await fetch('/api/config', { headers: { Accept: 'application/json' } });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const server = await res.json() as Partial<Pick<Config, 'serverUrl' | 'authRequired'>>;
      const serverDefaults: Partial<Config> = {};
      if (server.serverUrl) serverDefaults.serverUrl = server.serverUrl;

      // server config = default; re-apply the user's stored overrides on top so they keep precedence.
      this.config = { ...this.config, ...serverDefaults, ...this.storedOverrides };
      // Not a user preference: the host decides whether sign-in is required, whatever is stored.
      this.config.authRequired = server.authRequired === true;
      this.config.wsUrl = this.config.serverUrl;
      this.initialized = true; // mark done only on success, so a transient fetch failure can retry
      this.notifyConfigChange();
    } catch (error) {
      console.warn('Failed to fetch /api/config; using local config defaults:', error);
    }
  }

  /**
   * Persist ONLY the explicit user overrides to localStorage (not the server-provided defaults). This
   * keeps the precedence model honest across reloads: storing the full merged config would bake the
   * current server defaults into the override layer, so a later /api/config change could never reach a
   * client that had ever saved — defeating the single source of truth.
   */
  private saveConfig(): void {
    try {
      localStorage.setItem('koboldlair-config', JSON.stringify(this.storedOverrides));
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
    this.storedOverrides.serverUrl = url;
    this.config.serverUrl = url;
    this.config.wsUrl = url;
    this.saveConfig();
    this.notifyConfigChange();
  }

  /**
   * Update multiple configuration values
   */
  updateConfig(updates: Partial<Config>): void {
    this.storedOverrides = { ...this.storedOverrides, ...updates };
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
