/**
 * Proof of Concept: Migrating DraCode modals to Birko.Web.Components
 *
 * This demonstrates how to replace custom modal implementations
 * (showAlert, showConfirm, showPrompt) with Birko.Web's b-modal.
 */

import { BaseComponent, define } from 'birko-web-core';
import type { BModal } from 'birko-web-components';

/**
 * Modal Manager using Birko.Web.Components
 *
 * Replaces the custom general-modal implementation with b-modal
 */
export class ModalManager {
  private modalElement: BModal | null = null;

  constructor() {
    // Ensure modal exists in DOM
    this.ensureModalExists();
  }

  /**
   * Ensure modal element exists in DOM
   */
  private ensureModalExists(): void {
    this.modalElement = document.querySelector('#birko-modal') as BModal;

    if (!this.modalElement) {
      // Create modal if it doesn't exist
      this.modalElement = document.createElement('b-modal') as BModal;
      this.modalElement.id = 'birko-modal';
      this.modalElement.title = '';
      this.modalElement.size = 'md';
      document.body.appendChild(this.modalElement);
    }
  }

  /**
   * Show alert dialog (replaces window.alert)
   *
   * OLD:
   * ```javascript
   * await showAlert('Error', 'Connection failed');
   * ```
   *
   * NEW:
   * ```typescript
   * await modals.alert('Error', 'Connection failed');
   * ```
   */
  async alert(title: string, message: string): Promise<void> {
    if (!this.modalElement) {
      console.error('Modal element not found');
      return;
    }

    return new Promise((resolve) => {
      this.modalElement!.title = title;
      this.modalElement!.size = 'sm';

      // Set content
      const content = document.createElement('div');
      content.innerHTML = `<p>${this.escapeHtml(message)}</p>`;

      // Set footer with OK button
      const footer = document.createElement('footer');
      footer.slot = 'footer';
      footer.style.cssText = 'display: flex; justify-content: flex-end; gap: 8px;';

      const okButton = document.createElement('b-button');
      okButton.variant = 'primary';
      okButton.textContent = 'OK';
      okButton.addEventListener('click', () => {
        this.modalElement!.close();
        resolve();
      });

      footer.appendChild(okButton);

      // Clear previous content and set new content
      this.modalElement!.innerHTML = '';
      this.modalElement!.appendChild(content);
      this.modalElement!.appendChild(footer);

      // Open modal
      this.modalElement!.open();
    });
  }

  /**
   * Show confirm dialog (replaces window.confirm)
   *
   * OLD:
   * ```javascript
   * const confirmed = await showConfirm('Disconnect', 'Are you sure?');
   * if (confirmed) { ... }
   * ```
   *
   * NEW:
   * ```typescript
   * const confirmed = await modals.confirm('Disconnect', 'Are you sure?');
   * if (confirmed) { ... }
   * ```
   */
  async confirm(title: string, message: string): Promise<boolean> {
    if (!this.modalElement) {
      console.error('Modal element not found');
      return false;
    }

    return new Promise((resolve) => {
      this.modalElement!.title = title;
      this.modalElement!.size = 'sm';

      const content = document.createElement('div');
      content.innerHTML = `<p>${this.escapeHtml(message)}</p>`;

      const footer = document.createElement('footer');
      footer.slot = 'footer';
      footer.style.cssText = 'display: flex; justify-content: flex-end; gap: 8px;';

      const cancelButton = document.createElement('b-button');
      cancelButton.variant = 'secondary';
      cancelButton.textContent = 'Cancel';
      cancelButton.addEventListener('click', () => {
        this.modalElement!.close();
        resolve(false);
      });

      const confirmButton = document.createElement('b-button');
      confirmButton.variant = 'primary';
      confirmButton.textContent = 'Confirm';
      confirmButton.addEventListener('click', () => {
        this.modalElement!.close();
        resolve(true);
      });

      footer.appendChild(cancelButton);
      footer.appendChild(confirmButton);

      this.modalElement!.innerHTML = '';
      this.modalElement!.appendChild(content);
      this.modalElement!.appendChild(footer);

      this.modalElement!.open();
    });
  }

  /**
   * Show prompt dialog (replaces window.prompt)
   *
   * OLD:
   * ```javascript
   * const response = await showPrompt('Question', 'Enter value:', 'default');
   * if (response !== null) { ... }
   * ```
   *
   * NEW:
   * ```typescript
   * const response = await modals.prompt('Question', 'Enter value:', 'default');
   * if (response !== null) { ... }
   * ```
   */
  async prompt(title: string, message: string, defaultValue: string = ''): Promise<string | null> {
    if (!this.modalElement) {
      console.error('Modal element not found');
      return null;
    }

    return new Promise((resolve) => {
      this.modalElement!.title = title;
      this.modalElement!.size = 'sm';

      const content = document.createElement('div');
      content.innerHTML = `
        <p style="margin-bottom: 16px;">${this.escapeHtml(message)}</p>
        <b-input
          id="prompt-input"
          value="${this.escapeHtml(defaultValue)}"
          placeholder="Enter value..."
        ></b-input>
      `;

      const footer = document.createElement('footer');
      footer.slot = 'footer';
      footer.style.cssText = 'display: flex; justify-content: flex-end; gap: 8px;';

      const cancelButton = document.createElement('b-button');
      cancelButton.variant = 'secondary';
      cancelButton.textContent = 'Cancel';
      cancelButton.addEventListener('click', () => {
        this.modalElement!.close();
        resolve(null);
      });

      const okButton = document.createElement('b-button');
      okButton.variant = 'primary';
      okButton.textContent = 'OK';
      okButton.addEventListener('click', () => {
        const input = content.querySelector('#prompt-input') as any;
        const value = input?.value || '';
        this.modalElement!.close();
        resolve(value);
      });

      footer.appendChild(cancelButton);
      footer.appendChild(okButton);

      this.modalElement!.innerHTML = '';
      this.modalElement!.appendChild(content);
      this.modalElement!.appendChild(footer);

      this.modalElement!.open();

      // Focus input after modal opens
      setTimeout(() => {
        const input = content.querySelector('#prompt-input') as any;
        input?.focus();
        input?.select();
      }, 100);
    });
  }

  /**
   * Show custom modal with form
   *
   * Example: Connection configuration modal
   */
  async custom<T>(config: {
    title: string;
    size?: 'sm' | 'md' | 'lg' | 'xl';
    renderContent: () => HTMLElement;
    renderActions: (resolve: (value: T) => void, reject: () => void) => HTMLElement;
  }): Promise<T> {
    if (!this.modalElement) {
      throw new Error('Modal element not found');
    }

    return new Promise((resolve, reject) => {
      this.modalElement!.title = config.title;
      this.modalElement!.size = config.size || 'md';

      const content = config.renderContent();
      const footer = config.renderActions(resolve, reject);
      footer.slot = 'footer';

      this.modalElement!.innerHTML = '';
      this.modalElement!.appendChild(content);
      this.modalElement!.appendChild(footer);

      this.modalElement!.open();
    });
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}

/**
 * Global modal manager instance
 */
export const modals = new ModalManager();

/**
 * Connection Modal Example
 *
 * Demonstrates replacing the connection modal with Birko components
 */
export class ConnectionModal {
  private modals: ModalManager;

  constructor(modals: ModalManager) {
    this.modals = modals;
  }

  /**
   * Show connection configuration modal
   *
   * OLD:
   * ```javascript
   * showConnectionModal(defaultTabName, (tabName, workingDir) => {
   *   connectToProvider(tabName, workingDir);
   * });
   * ```
   *
   * NEW:
   * ```typescript
   * const { tabName, workingDir } = await connectionModal.show({
   *   defaultTabName: providerName
   * });
   * connectToProvider(tabName, workingDir);
   * ```
   */
  async show(config: { defaultTabName: string }): Promise<{ tabName: string; workingDir: string } | null> {
    return this.modals.custom<{ tabName: string; workingDir: string }>({
      title: 'Configure Connection',
      size: 'md',
      renderContent: () => {
        const container = document.createElement('div');
        container.innerHTML = `
          <div style="display: flex; flex-direction: column; gap: 16px;">
            <b-input
              id="connection-tab-name"
              label="Tab Name"
              value="${this.escapeHtml(config.defaultTabName)}"
              placeholder="Enter tab name..."
              required
            ></b-input>

            <b-input
              id="connection-working-dir"
              label="Working Directory"
              placeholder="Enter working directory..."
            ></b-input>

            <p style="font-size: 12px; color: var(--b-text-muted, #9ca3af);">
              💡 Tab name will be sanitized to create a valid directory name
            </p>
          </div>
        `;
        return container;
      },
      renderActions: (resolve, reject) => {
        const footer = document.createElement('footer');
        footer.style.cssText = 'display: flex; justify-content: flex-end; gap: 8px;';

        const cancelButton = document.createElement('b-button');
        cancelButton.variant = 'secondary';
        cancelButton.textContent = 'Cancel';
        cancelButton.addEventListener('click', () => {
          this.modals['modalElement']?.close();
          resolve(null);
        });

        const connectButton = document.createElement('b-button');
        connectButton.variant = 'primary';
        connectButton.textContent = 'Connect';
        connectButton.addEventListener('click', () => {
          const tabName = (document.querySelector('#connection-tab-name') as any)?.value || '';
          const workingDir = (document.querySelector('#connection-working-dir') as any)?.value || '';

          if (!tabName.trim()) {
            // Show validation error
            this.modals.alert('Validation Error', 'Tab name is required');
            return;
          }

          this.modals['modalElement']?.close();
          resolve({ tabName: tabName.trim(), workingDir: workingDir.trim() });
        });

        footer.appendChild(cancelButton);
        footer.appendChild(connectButton);

        // Auto-focus on tab name input
        setTimeout(() => {
          const input = document.querySelector('#connection-tab-name') as any;
          input?.focus();
        }, 100);

        return footer;
      }
    });
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}
