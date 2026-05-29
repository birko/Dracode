/**
 * Settings View for KoboldLair
 * Configure providers, manage user settings, and system preferences
 */

import { BaseComponent, define } from 'birko-web-core';
import toastService from '../services/toast-service.ts';

interface FormResult {
  valid: boolean;
  data: Record<string, unknown>;
  errors: Record<string, unknown>;
  groupErrors: Record<string, unknown>;
}

interface ProviderSetting {
  id: string;
  name: string;
  enabled: boolean;
  model: string;
  apiKey: string;
  maxTokens: number;
  temperature: number;
}

interface UserSettings {
  theme: 'light' | 'dark' | 'auto';
  language: string;
  notifications: boolean;
  autoSave: boolean;
  debugMode: boolean;
}

/**
 * Settings View Component
 * Application settings and provider configuration
 */
export class SettingsView extends BaseComponent {
  private _activeTab: 'providers' | 'general' = 'providers';
  private _providers: ProviderSetting[] = [];
  private _settings: UserSettings = {
    theme: 'auto',
    language: 'en',
    notifications: true,
    autoSave: true,
    debugMode: false
  };

  static get styles() {
    return `
      :host {
        display: block;
        padding: 20px;
        max-width: 1200px;
        margin: 0 auto;
      }

      .settings-header {
        margin-bottom: 24px;
      }

      .settings-header h1 {
        font-size: 28px;
        font-weight: 600;
        margin: 0 0 8px 0;
        color: var(--b-text, #1f2937);
      }

      .tabs {
        display: flex;
        gap: 4px;
        margin-bottom: 24px;
        border-bottom: 1px solid var(--b-border, #e5e7eb);
      }

      .tab {
        padding: 12px 20px;
        background: none;
        border: none;
        border-bottom: 2px solid transparent;
        cursor: pointer;
        font-size: 14px;
        font-weight: 500;
        color: var(--b-text-secondary, #6b7280);
        transition: all 0.2s ease;
      }

      .tab:hover {
        color: var(--b-text, #1f2937);
      }

      .tab.active {
        color: var(--b-color-primary, #3b82f6);
        border-bottom-color: var(--b-color-primary, #3b82f6);
      }

      .settings-content {
        background: var(--b-bg-elevated, #ffffff);
        border: 1px solid var(--b-border, #e5e7eb);
        border-radius: var(--b-radius-lg, 12px);
        padding: 24px;
      }

      .settings-section {
        margin-bottom: 32px;
      }

      .settings-section:last-child {
        margin-bottom: 0;
      }

      .section-title {
        font-size: 18px;
        font-weight: 600;
        margin-bottom: 16px;
        color: var(--b-text, #1f2937);
      }

      .section-description {
        font-size: 14px;
        color: var(--b-text-secondary, #6b7280);
        margin-bottom: 20px;
      }

      .provider-card {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 12px;
        transition: border-color 0.2s ease;
      }

      .provider-card:hover {
        border-color: var(--b-color-primary, #3b82f6);
      }

      .provider-info {
        flex: 1;
      }

      .provider-name {
        font-size: 16px;
        font-weight: 600;
        color: var(--b-text, #1f2937);
        margin-bottom: 4px;
      }

      .provider-details {
        font-size: 13px;
        color: var(--b-text-secondary, #6b7280);
      }

      .provider-status {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .provider-actions {
        display: flex;
        gap: 8px;
      }

      .form-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
        gap: 20px;
      }

      .form-group {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .form-label {
        font-size: 14px;
        font-weight: 500;
        color: var(--b-text, #1f2937);
      }

      .form-description {
        font-size: 12px;
        color: var(--b-text-muted, #9ca3af);
      }

      .actions-bar {
        display: flex;
        justify-content: flex-end;
        gap: 12px;
        padding-top: 24px;
        border-top: 1px solid var(--b-border, #e5e7eb);
      }

      .empty-state {
        text-align: center;
        padding: 60px 20px;
        color: var(--b-text-secondary, #6b7280);
      }
    `;
  }

  render() {
    return `
      <div class="settings-header">
        <h1>⚙️ Settings</h1>
        <p>Configure providers and manage application preferences</p>
      </div>

      <b-tabs id="settingsTabs" active-tab="${this._activeTab}">
        <b-tab id="providers" label="Providers">
          ${this.renderProvidersSettings()}
        </b-tab>
        <b-tab id="general" label="General">
          ${this.renderGeneralSettings()}
        </b-tab>
      </b-tabs>
    `;
  }

  private renderProvidersSettings(): string {
    return `
      <div class="settings-section">
        <h2 class="section-title">LLM Providers</h2>
        <p class="section-description">Configure and manage your LLM provider connections</p>

        ${this._providers.length === 0 ? `
          <div class="empty-state">
            <span style="font-size: 48px;">🔧</span>
            <h3>No Providers Configured</h3>
            <p>Add providers to enable AI agent functionality</p>
            <b-button variant="primary" id="addProvider" style="margin-top: 16px;">+ Add Provider</b-button>
          </div>
        ` : `
          <div>
            ${this._providers.map(provider => `
              <b-card class="provider-card">
                <div class="provider-info">
                  <div class="provider-name">${this.escapeHtml(provider.name)}</div>
                  <div class="provider-details">
                    Model: ${this.escapeHtml(provider.model)} •
                    Tokens: ${provider.maxTokens.toLocaleString()}
                  </div>
                </div>
                <div class="provider-status">
                  <b-badge variant="${provider.enabled ? 'success' : 'secondary'}">
                    ${provider.enabled ? 'Enabled' : 'Disabled'}
                  </b-badge>
                </div>
                <div class="provider-actions">
                  <b-button variant="ghost" size="sm" data-edit="${provider.id}">Edit</b-button>
                  <b-button variant="ghost" size="sm" data-test="${provider.id}">Test</b-button>
                  <b-button variant="danger" size="sm" data-delete="${provider.id}">Remove</b-button>
                </div>
              </b-card>
            `).join('')}

            <div style="margin-top: 20px;">
              <b-button variant="primary" id="addProvider">+ Add Provider</b-button>
            </div>
          </div>
        `}
      </div>
    `;
  }

  private renderGeneralSettings(): string {
    return `
      <b-form
        id="generalSettingsForm"
        schema='${JSON.stringify(this.getGeneralSettingsSchema())}'
        data='${JSON.stringify(this._settings)}'>
      </b-form>

      <div class="actions-bar">
        <b-button variant="secondary" id="resetSettings">Reset to Defaults</b-button>
        <b-button variant="primary" id="saveSettings">Save Settings</b-button>
      </div>
    `;
  }

  private getGeneralSettingsSchema() {
    return {
      name: 'generalSettings',
      validateOn: 'submit' as const,
      children: [
        {
          name: 'appearance',
          label: 'Appearance',
          layout: 'grid' as const,
          children: [
            {
              name: 'theme',
              type: 'select' as const,
              label: 'Theme',
              placeholder: 'Select theme',
              required: true,
              options: [
                { value: 'auto', label: 'Auto (system)' },
                { value: 'light', label: 'Light' },
                { value: 'dark', label: 'Dark' }
              ],
              rules: [
                { type: 'required' as const, message: 'Theme is required' }
              ]
            },
            {
              name: 'language',
              type: 'select' as const,
              label: 'Language',
              placeholder: 'Select language',
              required: true,
              options: [
                { value: 'en', label: 'English' },
                { value: 'es', label: 'Spanish' },
                { value: 'fr', label: 'French' }
              ],
              rules: [
                { type: 'required' as const, message: 'Language is required' }
              ]
            }
          ]
        },
        {
          name: 'notifications',
          label: 'Notifications',
          layout: 'stack' as const,
          children: [
            {
              name: 'notifications_enabled',
              type: 'switch' as const,
              label: 'Enable notifications',
              default: true
            },
            {
              name: 'autoSave',
              type: 'switch' as const,
              label: 'Auto-save specifications',
              default: true
            },
            {
              name: 'debugMode',
              type: 'switch' as const,
              label: 'Debug mode (verbose logging)',
              default: false
            }
          ]
        }
      ]
    };
  }

  protected onMount() {
    // Load data
    this.loadProviders();
    this.loadSettings();

    // Set up event listeners
    this.setupTabs();
    this.setupProvidersEvents();
    this.setupSettingsEvents();
  }

  protected onUpdated() {
    this.onMount();
  }

  private setupTabs() {
    // Listen for b-tabs change event
    const tabs = this.$('#settingsTabs') as any;
    if (tabs) {
      tabs.addEventListener('change', (e: any) => {
        this._activeTab = e.detail.activeTab;
        // Re-render only the tab content, not the whole view
        this.update();
      });
    }
  }

  private setupProvidersEvents() {
    this.$('#addProvider')?.addEventListener('click', () => {
      console.log('Add provider clicked');
      this.emit('add-provider');
    });

    this.$$('[data-edit]').forEach(btn => {
      btn.addEventListener('click', () => {
        const providerId = btn.getAttribute('data-edit');
        this.editProvider(providerId!);
      });
    });

    this.$$('[data-test]').forEach(btn => {
      btn.addEventListener('click', () => {
        const providerId = btn.getAttribute('data-test');
        this.testProvider(providerId!);
      });
    });

    this.$$('[data-delete]').forEach(btn => {
      btn.addEventListener('click', () => {
        const providerId = btn.getAttribute('data-delete');
        this.deleteProvider(providerId!);
      });
    });
  }

  private setupSettingsEvents() {
    this.$('#saveSettings')?.addEventListener('click', () => this.saveSettingsFromForm());
    this.$('#resetSettings')?.addEventListener('click', () => this.resetSettings());
  }

  private async loadProviders() {
    // TODO: Load from actual API
    this._providers = [
      {
        id: 'provider-1',
        name: 'OpenAI',
        enabled: true,
        model: 'gpt-4o',
        apiKey: 'sk-...xxx',
        maxTokens: 128000,
        temperature: 7
      },
      {
        id: 'provider-2',
        name: 'Anthropic Claude',
        enabled: true,
        model: 'claude-3-5-sonnet-latest',
        apiKey: 'sk-ant-...xxx',
        maxTokens: 200000,
        temperature: 7
      }
    ];

    this.update();
  }

  private loadSettings() {
    // TODO: Load from localStorage or API
    this.update();
  }

  private editProvider(providerId: string) {
    console.log('Edit provider:', providerId);
    this.emit('edit-provider', { providerId });
  }

  private testProvider(providerId: string) {
    console.log('Test provider:', providerId);
    this.emit('test-provider', { providerId });
  }

  private deleteProvider(providerId: string) {
    console.log('Delete provider:', providerId);
    this._providers = this._providers.filter(p => p.id !== providerId);
    this.update();
    this.emit('provider-removed', { providerId });
  }

  private saveSettingsFromForm() {
    const form = this.$('#generalSettingsForm') as any;

    if (!form) {
      this.showMessage('Form not found', 'error');
      return;
    }

    // Get form result with validation
    const result: FormResult = form.getFormData();

    if (!result.valid) {
      // Show validation errors
      const errorMessages = Object.values(result.errors).filter(Boolean);
      if (errorMessages.length > 0) {
        this.showMessage(String(errorMessages[0]), 'error');
      } else {
        this.showMessage('Please fix form errors', 'error');
      }
      return;
    }

    // Map form data back to settings structure
    this._settings = {
      theme: (result.data.theme as any) || 'auto',
      language: result.data.language as string || 'en',
      notifications: result.data.notifications_enabled as boolean || false,
      autoSave: result.data.autoSave as boolean || false,
      debugMode: result.data.debugMode as boolean || false
    };

    // TODO: Save to localStorage or API
    console.log('Settings saved:', this._settings);
    this.emit('settings-saved', { settings: this._settings });

    // Show success message
    this.showMessage('Settings saved successfully', 'success');
  }

  private saveSettings() {
    // Legacy method - kept for backward compatibility
    this.saveSettingsFromForm();
  }

  private resetSettings() {
    this._settings = {
      theme: 'auto',
      language: 'en',
      notifications: true,
      autoSave: true,
      debugMode: false
    };
    this.update();
    this.showMessage('Settings reset to defaults', 'info');
  }

  private showMessage(message: string, type: 'success' | 'info' | 'warning' | 'error') {
    // Use toast notification service
    toastService.show({ message, variant: type as any });
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  /**
   * Public methods
   */
  public refresh(): void {
    this.loadProviders();
    this.loadSettings();
  }
}

// Register the custom element
define('kobold-settings-view', SettingsView);
