import { ApiClient } from './api.js';
import { WebSocketClient } from './websocket.js';
import { DashboardView } from './views.js';
import { DragonView } from './dragon-view.js';
import { HierarchyView } from './hierarchy-view.js';
import { ProjectsView } from './projects-view.js';
import { ProvidersView } from './providers-view.js';
import CONFIG from './config.js';

class App {
    constructor() {
        this.api = new ApiClient();
        this.wsHealth = null;
        this.currentView = null;
        this.views = new Map([
            ['dashboard', new DashboardView(this.api)],
            ['dragon', new DragonView()],
            ['hierarchy', new HierarchyView(this.api)],
            ['projects', new ProjectsView(this.api)],
            ['providers', new ProvidersView(this.api)]
        ]);

        this.init();
    }

    async init() {
        // Wait a bit for config to load
        await new Promise(resolve => setTimeout(resolve, 100));
        
        this.setupNavigation();
        this.setupConnectionMonitor();
        this.setupRefreshButton();
        await this.loadInitialView();
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

    setupConnectionMonitor() {
        const checkConnection = async () => {
            try {
                const response = await fetch(`${CONFIG.apiUrl}/`);
                if (response.ok) {
                    this.updateConnectionStatus('connected');
                } else {
                    this.updateConnectionStatus('error');
                }
            } catch {
                this.updateConnectionStatus('error');
            }
        };

        checkConnection();
        setInterval(checkConnection, 10000);
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
                    <div>Error loading view</div>
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
