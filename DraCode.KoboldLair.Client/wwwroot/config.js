// Configuration - will be loaded from server
let CONFIG = {
    apiUrl: window.location.origin,
    wsUrl: '', // Will use current origin
    serverUrl: '', // Will use current origin (proxied)
    authToken: '',
    refreshInterval: 5000
};

// Load configuration from server
async function loadConfig() {
    try {
        const response = await fetch('/api/config');
        if (response.ok) {
            const serverConfig = await response.json();
            // If serverUrl is empty, use current origin
            const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
            CONFIG.wsUrl = serverConfig.serverUrl || `${wsProtocol}//${window.location.host}`;
            CONFIG.serverUrl = serverConfig.serverUrl || `${wsProtocol}//${window.location.host}`;
            CONFIG.authToken = serverConfig.authToken || '';
            console.log('Loaded config:', CONFIG);
        }
    } catch (error) {
        console.warn('Failed to load server config, using defaults:', error);
        // Use current origin as fallback
        const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        CONFIG.wsUrl = `${wsProtocol}//${window.location.host}`;
        CONFIG.serverUrl = `${wsProtocol}//${window.location.host}`;
    }
}

// Initialize config on load
loadConfig();

export default CONFIG;
