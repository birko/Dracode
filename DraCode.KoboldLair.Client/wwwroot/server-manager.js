// Server connection manager
export class ServerManager {
    constructor() {
        this.servers = this.loadServers();
        this.activeServerId = this.loadActiveServerId();
    }

    loadServers() {
        const stored = localStorage.getItem('koboldlair_servers');
        if (stored) {
            return JSON.parse(stored);
        }
        // Default server - connect directly to backend
        return [{
            id: 'default',
            name: 'Local Server',
            url: 'wss://localhost:57085',
            token: '',
            isDefault: true
        }];
    }

    saveServers() {
        localStorage.setItem('koboldlair_servers', JSON.stringify(this.servers));
    }

    loadActiveServerId() {
        const stored = localStorage.getItem('koboldlair_active_server');
        if (stored) {
            return stored;
        }
        // Return first server or default
        return this.servers.length > 0 ? this.servers[0].id : 'default';
    }

    saveActiveServerId() {
        localStorage.setItem('koboldlair_active_server', this.activeServerId);
    }

    getActiveServer() {
        const server = this.servers.find(s => s.id === this.activeServerId);
        return server || this.servers[0];
    }

    setActiveServer(serverId) {
        const server = this.servers.find(s => s.id === serverId);
        if (server) {
            this.activeServerId = serverId;
            this.saveActiveServerId();
            return true;
        }
        return false;
    }

    addServer(name, url, token = '') {
        const id = `server_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        const server = {
            id,
            name,
            url,
            token,
            isDefault: false
        };
        this.servers.push(server);
        this.saveServers();
        return server;
    }

    updateServer(id, updates) {
        const index = this.servers.findIndex(s => s.id === id);
        if (index !== -1) {
            this.servers[index] = { ...this.servers[index], ...updates };
            this.saveServers();
            return true;
        }
        return false;
    }

    deleteServer(id) {
        // Don't allow deleting the last server
        if (this.servers.length <= 1) {
            return false;
        }

        const index = this.servers.findIndex(s => s.id === id);
        if (index !== -1) {
            this.servers.splice(index, 1);
            
            // If we deleted the active server, switch to first available
            if (this.activeServerId === id) {
                this.activeServerId = this.servers[0].id;
                this.saveActiveServerId();
            }
            
            this.saveServers();
            return true;
        }
        return false;
    }

    getAllServers() {
        return this.servers;
    }

    getServerById(id) {
        return this.servers.find(s => s.id === id);
    }
}

// Global instance
export const serverManager = new ServerManager();
