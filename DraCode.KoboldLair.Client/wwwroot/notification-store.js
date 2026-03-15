/**
 * Shared notification store for cross-view escalation awareness.
 * DragonView pushes escalations here; Dashboard and Projects views read from it.
 */
class NotificationStore {
    constructor() {
        this.escalations = [];
        this._listeners = [];
    }

    addEscalation(escalation) {
        this.escalations.push(escalation);
        this._notify();
    }

    clearEscalations() {
        this.escalations = [];
        this._notify();
    }

    get pendingCount() {
        return this.escalations.length;
    }

    onChange(callback) {
        this._listeners.push(callback);
    }

    _notify() {
        for (const cb of this._listeners) {
            try { cb(this.escalations); } catch (_) {}
        }
    }
}

const store = new NotificationStore();
export default store;
