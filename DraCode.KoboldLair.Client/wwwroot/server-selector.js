import { serverManager } from './server-manager.js';

export class ServerSelector {
    constructor() {
        this.onServerChange = null;
    }

    render() {
        const activeServer = serverManager.getActiveServer();
        const servers = serverManager.getAllServers();

        return `
            <div class="server-selector">
                <button class="server-selector-button" id="serverSelectorBtn">
                    <span class="server-selector-icon">🌐</span>
                    <span class="server-selector-name">${activeServer.name}</span>
                    <span class="server-selector-arrow">▼</span>
                </button>
                <div class="server-selector-dropdown" id="serverSelectorDropdown" style="display: none;">
                    <div class="server-selector-list">
                        ${servers.map(server => `
                            <button class="server-selector-item ${server.id === activeServer.id ? 'active' : ''}" 
                                    data-server-id="${server.id}">
                                <span class="server-selector-item-icon">${server.id === activeServer.id ? '✓' : '○'}</span>
                                <div class="server-selector-item-info">
                                    <div class="server-selector-item-name">${server.name}</div>
                                    <div class="server-selector-item-url">${server.url}</div>
                                </div>
                            </button>
                        `).join('')}
                    </div>
                    <div class="server-selector-actions">
                        <button class="btn btn-secondary btn-sm" id="addServerBtn">
                            <span>➕ Add Server</span>
                        </button>
                        <button class="btn btn-secondary btn-sm" id="manageServersBtn">
                            <span>⚙️ Manage</span>
                        </button>
                    </div>
                </div>
            </div>
        `;
    }

    mount(container) {
        container.innerHTML = this.render();
        this.attachEventListeners();
    }

    attachEventListeners() {
        const button = document.getElementById('serverSelectorBtn');
        const dropdown = document.getElementById('serverSelectorDropdown');

        // Toggle dropdown
        button?.addEventListener('click', (e) => {
            e.stopPropagation();
            const isVisible = dropdown.style.display === 'block';
            dropdown.style.display = isVisible ? 'none' : 'block';
        });

        // Close dropdown when clicking outside
        document.addEventListener('click', (e) => {
            if (!e.target.closest('.server-selector')) {
                dropdown.style.display = 'none';
            }
        });

        // Server selection
        document.querySelectorAll('.server-selector-item').forEach(item => {
            item.addEventListener('click', (e) => {
                const serverId = e.currentTarget.dataset.serverId;
                this.switchServer(serverId);
            });
        });

        // Add server button
        document.getElementById('addServerBtn')?.addEventListener('click', () => {
            dropdown.style.display = 'none';
            this.showAddServerModal();
        });

        // Manage servers button
        document.getElementById('manageServersBtn')?.addEventListener('click', () => {
            dropdown.style.display = 'none';
            this.showManageServersModal();
        });
    }

    switchServer(serverId) {
        if (serverManager.setActiveServer(serverId)) {
            // Trigger server change callback
            if (this.onServerChange) {
                this.onServerChange(serverManager.getActiveServer());
            }
            
            // Update UI
            const container = document.querySelector('.server-selector').parentElement;
            this.mount(container);
            
            // Show notification
            this.showNotification('Server switched successfully');
        }
    }

    showAddServerModal() {
        const modal = document.createElement('div');
        modal.className = 'modal';
        modal.innerHTML = `
            <div class="modal-content">
                <div class="modal-header">
                    <h3>Add Server Connection</h3>
                    <button class="modal-close" id="closeAddServerModal">✕</button>
                </div>
                <div class="modal-body">
                    <div class="form-group">
                        <label>Server Name</label>
                        <input type="text" id="serverName" class="form-control" placeholder="My KoboldLair Server">
                    </div>
                    <div class="form-group">
                        <label>WebSocket URL</label>
                        <input type="text" id="serverUrl" class="form-control" placeholder="ws://localhost:5000">
                        <small>Use ws:// for unencrypted or wss:// for encrypted connections</small>
                    </div>
                    <div class="form-group">
                        <label>Auth Token (Optional)</label>
                        <input type="text" id="serverToken" class="form-control" placeholder="Leave empty if not required">
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" id="cancelAddServer">Cancel</button>
                    <button class="btn btn-primary" id="saveAddServer">Add Server</button>
                </div>
            </div>
        `;

        document.body.appendChild(modal);

        // Event listeners
        document.getElementById('closeAddServerModal')?.addEventListener('click', () => modal.remove());
        document.getElementById('cancelAddServer')?.addEventListener('click', () => modal.remove());
        document.getElementById('saveAddServer')?.addEventListener('click', () => {
            const name = document.getElementById('serverName').value.trim();
            const url = document.getElementById('serverUrl').value.trim();
            const token = document.getElementById('serverToken').value.trim();

            if (!name || !url) {
                alert('Please enter server name and URL');
                return;
            }

            if (!url.startsWith('ws://') && !url.startsWith('wss://')) {
                alert('URL must start with ws:// or wss://');
                return;
            }

            const server = serverManager.addServer(name, url, token);
            modal.remove();
            
            // Refresh selector
            const container = document.querySelector('.server-selector').parentElement;
            this.mount(container);
            
            this.showNotification(`Server "${name}" added successfully`);
        });
    }

    showManageServersModal() {
        const servers = serverManager.getAllServers();
        const modal = document.createElement('div');
        modal.className = 'modal';
        modal.innerHTML = `
            <div class="modal-content modal-large">
                <div class="modal-header">
                    <h3>Manage Server Connections</h3>
                    <button class="modal-close" id="closeManageModal">✕</button>
                </div>
                <div class="modal-body">
                    <div class="server-list">
                        ${servers.map(server => `
                            <div class="server-card" data-server-id="${server.id}">
                                <div class="server-card-header">
                                    <h4>${server.name}</h4>
                                    ${servers.length > 1 ? `<button class="btn-icon" data-action="delete" title="Delete">🗑️</button>` : ''}
                                </div>
                                <div class="server-card-body">
                                    <div class="form-group">
                                        <label>WebSocket URL</label>
                                        <input type="text" class="form-control" data-field="url" value="${server.url}">
                                    </div>
                                    <div class="form-group">
                                        <label>Auth Token</label>
                                        <input type="text" class="form-control" data-field="token" value="${server.token || ''}">
                                    </div>
                                </div>
                                <div class="server-card-footer">
                                    <button class="btn btn-sm btn-primary" data-action="save">Save Changes</button>
                                    <button class="btn btn-sm btn-secondary" data-action="test">Test Connection</button>
                                </div>
                            </div>
                        `).join('')}
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(modal);

        // Event listeners
        document.getElementById('closeManageModal')?.addEventListener('click', () => modal.remove());

        // Handle actions
        modal.querySelectorAll('[data-action]').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const action = e.target.dataset.action;
                const card = e.target.closest('.server-card');
                const serverId = card.dataset.serverId;

                if (action === 'delete') {
                    if (confirm('Are you sure you want to delete this server connection?')) {
                        if (serverManager.deleteServer(serverId)) {
                            card.remove();
                            this.showNotification('Server deleted');
                        }
                    }
                } else if (action === 'save') {
                    const url = card.querySelector('[data-field="url"]').value.trim();
                    const token = card.querySelector('[data-field="token"]').value.trim();
                    
                    if (serverManager.updateServer(serverId, { url, token })) {
                        this.showNotification('Server updated');
                    }
                } else if (action === 'test') {
                    this.testConnection(serverId);
                }
            });
        });
    }

    async testConnection(serverId) {
        const server = serverManager.getServerById(serverId);
        if (!server) return;

        this.showNotification('Testing connection...');

        try {
            const ws = new WebSocket(server.url + '/ws' + (server.token ? `?token=${server.token}` : ''));
            
            ws.onopen = () => {
                this.showNotification('✅ Connection successful!', 'success');
                ws.close();
            };

            ws.onerror = () => {
                this.showNotification('❌ Connection failed', 'error');
            };

            // Timeout after 5 seconds
            setTimeout(() => {
                if (ws.readyState !== WebSocket.OPEN) {
                    ws.close();
                    this.showNotification('⏱️ Connection timeout', 'error');
                }
            }, 5000);
        } catch (error) {
            this.showNotification(`❌ Error: ${error.message}`, 'error');
        }
    }

    showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.textContent = message;
        document.body.appendChild(notification);

        setTimeout(() => {
            notification.classList.add('show');
        }, 10);

        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => notification.remove(), 300);
        }, 3000);
    }
}
