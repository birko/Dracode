/**
 * Notification Store
 * Manages escalation notifications using Birko.Web.Core Store
 */

import { Store } from 'birko-web-core/state';

export interface Escalation {
  taskId?: string;
  type?: string;
  message?: string;
  timestamp?: number;
  projectName?: string;
  agentType?: string;
}

interface NotificationState {
  escalations: Escalation[];
}

/**
 * Notification Store
 * Singleton instance for managing escalation notifications across views
 */
class NotificationStoreClass {
  private store: Store<NotificationState>;
  private seenIds = new Set<string>();

  constructor() {
    this.store = new Store<NotificationState>({
      escalations: []
    });
  }

  /**
   * Add an escalation notification
   * Deduplicates by taskId + type + message to prevent replay duplicates
   */
  addEscalation(escalation: Escalation): void {
    const dedupKey = `${escalation.taskId || ''}_${escalation.type || ''}_${escalation.message || ''}`;
    if (this.seenIds.has(dedupKey)) return;

    this.seenIds.add(dedupKey);

    const current = this.store.get('escalations');
    this.store.set('escalations', [...current, { ...escalation, timestamp: Date.now() }]);
  }

  /**
   * Clear all escalation notifications
   */
  clearEscalations(): void {
    this.store.set('escalations', []);
    this.seenIds.clear();
  }

  /**
   * Get the count of pending escalations
   */
  get pendingCount(): number {
    return this.store.get('escalations').length;
  }

  /**
   * Get all escalations
   */
  getEscalations(): Escalation[] {
    return this.store.get('escalations');
  }

  /**
   * Subscribe to escalation changes
   * @returns Unsubscribe function
   */
  onChange(callback: (escalations: Escalation[]) => void): () => void {
    return this.store.on('escalations', callback);
  }

  /**
   * Remove a specific escalation by index
   */
  removeEscalation(index: number): void {
    const current = this.store.get('escalations');
    if (index >= 0 && index < current.length) {
      const updated = [...current];
      updated.splice(index, 1);
      this.store.set('escalations', updated);
    }
  }

  /**
   * Mark an escalation as seen/read
   * This is a no-op for now, but could be used to track read status
   */
  markAsSeen(index: number): void {
    // Could add a 'seen' flag to escalation objects in the future
  }

  /**
   * Get escalations filtered by project
   */
  getEscalationsByProject(projectName: string): Escalation[] {
    return this.store.get('escalations').filter(e => e.projectName === projectName);
  }

  /**
   * Get escalations filtered by type
   */
  getEscalationsByType(type: string): Escalation[] {
    return this.store.get('escalations').filter(e => e.type === type);
  }
}

// Export singleton instance
export const notificationStore = new NotificationStoreClass();
export default notificationStore;
