// Configuration for KoboldLair Client
const CONFIG = {
    // Server WebSocket URL (change this to your server URL)
    // For local development: 'ws://localhost:5000' or 'ws://localhost:5001'
    // For production: 'wss://your-server.com'
    serverUrl: 'ws://localhost:5000',
    
    // Authentication token (leave empty if authentication is disabled on server)
    // Get this from your server administrator
    authToken: '',
    
    // WebSocket endpoints
    endpoints: {
        wyvern: '/ws',    // Task delegation endpoint
        dragon: '/dragon' // Requirements gathering endpoint
    }
};
