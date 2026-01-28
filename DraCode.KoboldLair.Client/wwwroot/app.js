import { ApiClient } from './api.js';
import { WebSocketClient } from './websocket.js';
import { DashboardView } from './views.js';
import { DragonView } from './dragon-view.js';
import { HierarchyView } from './hierarchy-view.js';
import { ProjectsView } from './projects-view.js';
import { ProvidersView } from './providers-view.js';
import { ServerSelector } from './server-selector.js';
import { refreshConfig } from './config.js';
import CONFIG from './config.js';

class App {
    constructor() {
        this.api = new ApiClient();
        this.wsHealth = null;
        this.currentView = null;
        this.serverSelector = new ServerSelector();
        this.views = new Map([
            ['dashboard', new DashboardView(this.api)],
            ['dragon', new DragonView(this.api)],
            ['hierarchy', new HierarchyView(this.api)],
            ['projects', new ProjectsView(this.api)],
            ['providers', new ProvidersView(this.api)]
        ]);

        this.init();
    }

    async init() {
        // Wait a bit for config to load
        await new Promise(resolve => setTimeout(resolve, 100));
        
        this.setupServerSelector();
        this.setupNavigation();
        this.setupConnectionControls();
        this.setupRefreshButton();
        this.updateConnectionInfo();
        await this.loadInitialView();
    }

    setupServerSelector() {
        const container = document.querySelector('.header-actions');
        if (container) {
            const selectorContainer = document.createElement('div');
            container.prepend(selectorContainer);
            this.serverSelector.mount(selectorContainer);
            
            // Handle server changes
            this.serverSelector.onServerChange = (server) => {
                console.log('Server changed to:', server.name);
                refreshConfig();
                
                // Disconnect and update connection info
                if (this.api && this.api.isConnected()) {
                    this.api.disconnect();
                }
                
                this.updateConnectionInfo();
            };
        }
    }

    setupNavigation() {
        const navItems = document.querySelectorAll('.nav-item');
        
        navItems.forEach(item => {
            item.addEventListener('click', async (e) => {
                e.preventDefault();
                
                const viewName = item.dataset.view;
                if (viewName) {
                    navItems.forEach(nav => nav.classList.remove('active'));
                    item.classList.add('active');
                    
                    await this.loadView(viewName);
                }
            });
        });

        window.addEventListener('hashchange', () => {
            const hash = window.location.hash.slice(1) || 'dashboard';
            this.loadView(hash);
        });
    }

    setupConnectionControls() {
        const connectBtn = document.getElementById('connectBtn');
        if (!connectBtn) return;

        connectBtn.addEventListener('click', async () => {
            if (this.api.isConnected()) {
                // Disconnect
                this.api.disconnect();
                this.updateConnectionStatus('disconnected');
                connectBtn.textContent = 'Connect';
            } else {
                // Connect
                connectBtn.disabled = true;
                connectBtn.textContent = 'Connecting...';
                this.updateConnectionStatus('connecting');
                
                try {
                    await this.api.connect();
                    this.updateConnectionStatus('connected');
                    connectBtn.textContent = 'Disconnect';
                    
                    // Reload current view after connection
                    const currentHash = window.location.hash.slice(1) || 'dashboard';
                    await this.loadView(currentHash);
                } catch (error) {
                    console.error('Connection failed:', error);
                    this.updateConnectionStatus('error');
                    connectBtn.textContent = 'Connect';
                } finally {
                    connectBtn.disabled = false;
                }
            }
        });

        // Listen to WebSocket status changes
        if (this.api.ws) {
            this.api.ws.onStatusChange((status) => {
                this.updateConnectionStatus(status);
                if (status === 'connected') {
                    connectBtn.textContent = 'Disconnect';
                    connectBtn.disabled = false;
                } else if (status === 'disconnected' || status === 'error') {
                    connectBtn.textContent = 'Connect';
                    connectBtn.disabled = false;
                }
            });
        }
    }

    updateConnectionInfo() {
        const serverUrl = document.getElementById('serverUrl');
        if (serverUrl) {
            serverUrl.textContent = CONFIG.wsUrl || 'Not configured';
        }
        this.updateConnectionStatus('disconnected');
        
        const connectBtn = document.getElementById('connectBtn');
        if (connectBtn) {
            connectBtn.textContent = 'Connect';
            connectBtn.disabled = false;
        }
    }

    updateConnectionStatus(status) {
        const statusEl = document.getElementById('connectionStatus');
        if (!statusEl) return;

        statusEl.className = `connection-status ${status}`;
        
        const statusText = {
            'connected': 'Connected',
            'connecting': 'Connecting...',
            'disconnected': 'Disconnected',
            'error': 'Connection Error'
        };
        
        const textEl = statusEl.querySelector('.status-text');
        if (textEl) {
            textEl.textContent = statusText[status];
        }
    }

    setupRefreshButton() {
        const refreshBtn = document.getElementById('refreshBtn');
        refreshBtn?.addEventListener('click', async () => {
            const currentHash = window.location.hash.slice(1) || 'dashboard';
            await this.loadView(currentHash);
        });
    }

    async loadInitialView() {
        const hash = window.location.hash.slice(1) || 'dashboard';
        await this.loadView(hash);
    }

    async loadView(viewName) {
        const content = document.getElementById('content');
        const pageTitle = document.getElementById('pageTitle');
        
        if (!content || !pageTitle) return;

        if (this.currentView?.onUnmount) {
            this.currentView.onUnmount();
        }

        pageTitle.textContent = this.formatTitle(viewName);
        content.innerHTML = '<div class="loading">⏳</div>';

        try {
            const view = this.views.get(viewName);
            if (view) {
                this.currentView = view;
                const html = await view.render();
                content.innerHTML = html;
                
                if (view.onMount) {
                    view.onMount();
                }
            } else {
                content.innerHTML = `
                    <div class="empty-state">
                        <div class="empty-state-icon">❓</div>
                        <div>View not found</div>
                    </div>
                `;
            }
        } catch (error) {
            console.error('Error loading view:', error);
            content.innerHTML = `
                <div class="empty-state">
                    <div class="empty-state-icon">⚠️</div>
                    <div class="empty-state-title">Error loading view</div>
                    <div class="empty-state-error">${error.message || error.toString()}</div>
                </div>
            `;
        }
    }

    formatTitle(viewName) {
        return viewName.charAt(0).toUpperCase() + viewName.slice(1);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    new App();
});
