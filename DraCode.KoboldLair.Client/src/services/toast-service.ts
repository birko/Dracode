/**
 * Toast Notification Service
 * Provides toast notifications using Birko.Web.Components b-toast
 */

type ToastVariant = 'success' | 'error' | 'warning' | 'info';

interface ToastMessage {
  message: string;
  variant?: ToastVariant;
  duration?: number;
  title?: string;
}

class ToastService {
  private container: HTMLElement | null = null;

  constructor() {
    this.ensureContainer();
  }

  private ensureContainer(): void {
    this.container = document.getElementById('toast-container');
    if (!this.container) {
      this.container = document.createElement('div');
      this.container.id = 'toast-container';
      this.container.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        z-index: 9999;
        display: flex;
        flex-direction: column;
        gap: 12px;
        pointer-events: none;
      `;
      document.body.appendChild(this.container);
    }
  }

  /**
   * Show a toast notification
   */
  show(options: ToastMessage): void {
    this.ensureContainer();

    const {
      message,
      variant = 'info',
      duration = 3000,
      title = ''
    } = options;

    // Create toast element
    const toast = document.createElement('b-toast');
    toast.setAttribute('variant', variant);
    toast.setAttribute('duration', duration.toString());

    if (title) {
      toast.setAttribute('title', title);
    }

    toast.textContent = message;
    toast.style.cssText = 'pointer-events: auto;';

    // Append to container
    this.container.appendChild(toast);

    // Auto-remove after duration
    setTimeout(() => {
      if (toast.parentNode) {
        toast.parentNode.removeChild(toast);
      }
    }, duration);
  }

  /**
   * Convenience methods for common toast types
   */
  success(message: string, title?: string): void {
    this.show({ message, variant: 'success', title });
  }

  error(message: string, title?: string): void {
    this.show({ message, variant: 'error', title, duration: 5000 });
  }

  warning(message: string, title?: string): void {
    this.show({ message, variant: 'warning', title });
  }

  info(message: string, title?: string): void {
    this.show({ message, variant: 'info', title });
  }

  /**
   * Clear all toasts
   */
  clear(): void {
    if (this.container) {
      this.container.innerHTML = '';
    }
  }
}

// Export singleton instance
export const toastService = new ToastService();
export default toastService;
