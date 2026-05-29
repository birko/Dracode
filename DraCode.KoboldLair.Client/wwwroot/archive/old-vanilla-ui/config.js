import { serverManager } from './server-manager.js';

// Configuration - dynamically loaded from ServerManager
let CONFIG = {
    apiUrl: window.location.origin,
    wsUrl: '',
    serverUrl: '',
    authToken: '',
    refreshInterval: 5000
};

// Load configuration from active server
async function loadConfig() {
    try {
        // First, try to load from /api/config endpoint (initial default)
        const response = await fetch('/api/config');
        if (response.ok) {
            const serverConfig = await response.json();
            // Store as default server if not already exists
            const servers = serverManager.getAllServers();
            if (servers.length === 0 || (servers.length === 1 && servers[0].id === 'default')) {
                serverManager.updateServer('default', {
                    url: serverConfig.serverUrl || 'ws://localhost:5000',
                    token: serverConfig.authToken || ''
                });
            }
        }
    } catch (error) {
        console.warn('Failed to load server config from API, using stored settings:', error);
    }

    // Load from active server in ServerManager
    updateConfigFromActiveServer();
}

// Update config from active server
function updateConfigFromActiveServer() {
    const activeServer = serverManager.getActiveServer();
    if (activeServer) {
        // Use the server URL directly (should be wss:// or ws://)
        CONFIG.wsUrl = activeServer.url;
        CONFIG.serverUrl = activeServer.url;
        CONFIG.authToken = activeServer.token || '';
        console.log('Active server config:', {
            name: activeServer.name,
            url: activeServer.url,
            hasToken: !!activeServer.token
        });
    }
}

// Initialize config on load
loadConfig();

// Export function to refresh config (call after switching servers)
export function refreshConfig() {
    updateConfigFromActiveServer();
}

export default CONFIG;
