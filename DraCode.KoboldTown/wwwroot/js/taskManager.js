// Task Manager Module
export class TaskManager {
    constructor() {
        this.tasks = new Map();
        this.currentFilter = 'all';
        this.updateCallbacks = [];
    }

    addTask(task) {
        this.tasks.set(task.id, task);
        this.notifyUpdate();
    }

    updateTask(taskId, updates) {
        const task = this.tasks.get(taskId);
        if (task) {
            Object.assign(task, updates);
            this.notifyUpdate();
        }
    }

    getAllTasks() {
        return Array.from(this.tasks.values());
    }

    getFilteredTasks() {
        const allTasks = this.getAllTasks();
        if (this.currentFilter === 'all') {
            return allTasks;
        }
        return allTasks.filter(task => task.status === this.currentFilter);
    }

    setFilter(filter) {
        this.currentFilter = filter;
        this.notifyUpdate();
    }

    onUpdate(callback) {
        this.updateCallbacks.push(callback);
    }

    notifyUpdate() {
        this.updateCallbacks.forEach(callback => callback());
    }

    clear() {
        this.tasks.clear();
        this.notifyUpdate();
    }
}
