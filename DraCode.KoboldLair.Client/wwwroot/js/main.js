/**
 * Main Application Module
 * KoboldTown App
 */
import { WebSocketManager } from './websocket.js';
import { TaskManager } from './taskManager.js';
import { UIController } from './ui.js';

class KoboldTownApp {
    constructor() {
        this.taskManager = new TaskManager();
        this.ui = new UIController(this.taskManager);
        this.ws = new WebSocketManager(this.getWebSocketUrl(), CONFIG.authToken);
        
        this.init();
    }

    getWebSocketUrl() {
        // Use server URL from config
        return CONFIG.serverUrl + CONFIG.endpoints.wyvern;
    }

    init() {
        // Setup WebSocket handlers
        this.setupWebSocketHandlers();
        
        // Setup UI callbacks
        this.setupUICallbacks();
        
        // Connect WebSocket
        this.ws.connect();
        
        console.log('KoboldTown initialized');
    }

    setupWebSocketHandlers() {
        this.ws.onConnectionStatus((connected) => {
            this.ui.updateConnectionStatus(connected);
            if (connected) {
                this.ui.addLog('success', 'Connected to Wyvern');
                // Request initial task list
                this.requestTasks();
            } else {
                this.ui.addLog('error', 'Disconnected from Wyvern');
            }
        });

        this.ws.onMessage('task_created', (data) => {
            this.ui.addLog('success', `Task created: ${data.task}`);
            this.taskManager.addTask({
                id: data.taskId,
                task: data.task,
                status: data.status,
                assignedAgent: '',
                createdAt: new Date().toISOString()
            });
        });

        this.ws.onMessage('status_update', (data) => {
            this.ui.addLog('info', `Task ${data.taskId}: ${data.status}`);
            this.taskManager.updateTask(data.taskId, {
                status: data.status,
                assignedAgent: data.assignedAgent || '',
                errorMessage: data.errorMessage,
                updatedAt: new Date().toISOString()
            });
        });

        this.ws.onMessage('agent_message', (data) => {
            const typeMap = {
                info: 'info',
                success: 'success',
                warning: 'warning',
                error: 'error',
                assistant: 'info',
                assistant_final: 'success',
                tool_call: 'info',
                tool_result: 'info'
            };
            const logType = typeMap[data.messageType] || 'info';
            this.ui.addLog(logType, `[${data.messageType}] ${data.content}`);
        });

        this.ws.onMessage('tasks_list', (data) => {
            this.ui.addLog('info', `Loaded ${data.tasks.length} tasks`);
            data.tasks.forEach(task => {
                this.taskManager.addTask(task);
            });
        });

        this.ws.onMessage('markdown_report', (data) => {
            this.downloadMarkdown(data.markdown);
        });

        this.ws.onMessage('error', (data) => {
            this.ui.addLog('error', `Error: ${data.error}`);
        });
    }

    setupUICallbacks() {
        this.ui.onSubmitTask = (task) => {
            this.submitTask(task);
        };

        this.ui.onRefresh = () => {
            this.requestTasks();
        };

        this.ui.onDownloadMarkdown = () => {
            this.requestMarkdown();
        };
    }

    submitTask(task) {
        if (this.ws.send({ action: 'submit_task', task })) {
            this.ui.addLog('info', 'Task submitted');
        } else {
            this.ui.addLog('error', 'Failed to submit task - not connected');
        }
    }

    requestTasks() {
        if (this.ws.send({ action: 'get_tasks' })) {
            this.ui.addLog('info', 'Requesting task list...');
        } else {
            this.ui.addLog('error', 'Failed to request tasks - not connected');
        }
    }

    requestMarkdown() {
        if (this.ws.send({ action: 'get_markdown' })) {
            this.ui.addLog('info', 'Generating markdown report...');
        } else {
            this.ui.addLog('error', 'Failed to request markdown - not connected');
        }
    }

    downloadMarkdown(content) {
        const blob = new Blob([content], { type: 'text/markdown' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `koboldtown-tasks-${new Date().toISOString().split('T')[0]}.md`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        this.ui.addLog('success', 'Markdown report downloaded');
    }
}

// Initialize app when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.app = new KoboldTownApp();
});
