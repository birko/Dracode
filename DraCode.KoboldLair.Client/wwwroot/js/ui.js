// UI Controller Module
export class UIController {
    constructor(taskManager) {
        this.taskManager = taskManager;
        this.initElements();
        this.bindEvents();
    }

    initElements() {
        // Optional elements (may not exist on all pages)
        this.taskForm = document.getElementById('taskForm');
        this.taskInput = document.getElementById('taskInput');
        this.submitBtn = document.getElementById('submitBtn');
        this.tasksContainer = document.getElementById('tasksContainer');
        this.logsContainer = document.getElementById('logsContainer');
        this.refreshBtn = document.getElementById('refreshBtn');
        this.downloadMarkdownBtn = document.getElementById('downloadMarkdownBtn');
        this.clearLogsBtn = document.getElementById('clearLogsBtn');
        this.connectionStatus = document.getElementById('connectionStatus');
        this.filterButtons = document.querySelectorAll('.filter-btn');
    }

    bindEvents() {
        // Task submission (only if form exists)
        if (this.taskForm) {
            this.taskForm.addEventListener('submit', (e) => {
                e.preventDefault();
                this.handleSubmit();
            });
        }

        // Filter buttons
        if (this.filterButtons) {
            this.filterButtons.forEach(btn => {
                btn.addEventListener('click', () => {
                    this.handleFilterChange(btn);
                });
            });
        }

        // Refresh button
        if (this.refreshBtn) {
            this.refreshBtn.addEventListener('click', () => {
                this.onRefresh && this.onRefresh();
            });
        }

        // Download markdown button
        if (this.downloadMarkdownBtn) {
            this.downloadMarkdownBtn.addEventListener('click', () => {
                this.onDownloadMarkdown && this.onDownloadMarkdown();
            });
        }

        // Clear logs button
        if (this.clearLogsBtn) {
            this.clearLogsBtn.addEventListener('click', () => {
                this.clearLogs();
            });
        }

        // Update tasks when task manager changes
        this.taskManager.onUpdate(() => {
            this.renderTasks();
        });
    }

    handleSubmit() {
        const task = this.taskInput.value.trim();
        if (task && this.onSubmitTask) {
            this.submitBtn.disabled = true;
            this.submitBtn.textContent = 'Submitting...';
            
            this.onSubmitTask(task);
            
            this.taskInput.value = '';
            
            setTimeout(() => {
                this.submitBtn.disabled = false;
                this.submitBtn.textContent = 'Submit Task';
            }, 1000);
        }
    }

    handleFilterChange(button) {
        // Update active state
        this.filterButtons.forEach(btn => btn.classList.remove('active'));
        button.classList.add('active');

        // Update filter
        const filter = button.dataset.filter;
        this.taskManager.setFilter(filter);
    }

    renderTasks() {
        const tasks = this.taskManager.getFilteredTasks();
        
        if (tasks.length === 0) {
            this.tasksContainer.innerHTML = '<p class="empty-message">No tasks found.</p>';
            return;
        }

        this.tasksContainer.innerHTML = tasks
            .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
            .map(task => this.createTaskCard(task))
            .join('');
    }

    createTaskCard(task) {
        const statusEmoji = {
            unassigned: '⚪',
            notinitialized: '🔵',
            working: '🟡',
            done: '🟢'
        };

        const createdDate = new Date(task.createdAt).toLocaleString();
        
        return `
            <div class="task-card" data-task-id="${task.id}">
                <div class="task-header">
                    <div class="task-description">${this.escapeHtml(task.task)}</div>
                    <span class="task-status ${task.status}">
                        ${statusEmoji[task.status]} ${task.status}
                    </span>
                </div>
                <div class="task-meta">
                    ${task.assignedAgent ? `<span class="task-agent">Agent: ${task.assignedAgent}</span>` : ''}
                    <span>Created: ${createdDate}</span>
                    ${task.errorMessage ? `<span style="color: var(--error-color)">Error: ${this.escapeHtml(task.errorMessage)}</span>` : ''}
                </div>
            </div>
        `;
    }

    addLog(type, message) {
        const timestamp = new Date().toLocaleTimeString();
        const logEntry = document.createElement('div');
        logEntry.className = `log-entry ${type}`;
        logEntry.innerHTML = `
            <span class="log-timestamp">[${timestamp}]</span>
            <span>${this.escapeHtml(message)}</span>
        `;
        
        this.logsContainer.appendChild(logEntry);
        this.logsContainer.scrollTop = this.logsContainer.scrollHeight;
    }

    clearLogs() {
        this.logsContainer.innerHTML = '';
    }

    updateConnectionStatus(connected) {
        this.connectionStatus.textContent = connected ? '🟢 Connected' : '⚪ Disconnected';
        this.connectionStatus.className = `status-indicator ${connected ? 'connected' : ''}`;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Callback setters
    onSubmitTask = null;
    onRefresh = null;
    onDownloadMarkdown = null;
}
