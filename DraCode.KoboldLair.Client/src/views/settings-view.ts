/**
 * Settings View for KoboldLair
 * Configure providers, manage user settings, and system preferences
 */

import { define } from 'birko-web-core';
import { KoboldComponent } from '../components/kobold-component.js';
import { toast, type BTabs, type BForm } from 'birko-web-components';

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

type SettingsTab = 'providers' | 'general';

const SETTINGS_TABS: { id: SettingsTab; label: string }[] = [
  { id: 'providers', label: 'Providers' },
  { id: 'general', label: 'General' },
];

/**
 * Settings View Component
 * Application settings and provider configuration
 */
export class SettingsView extends KoboldComponent {
  private _activeTab: SettingsTab = 'providers';
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

      <b-tabs id="settingsTabs" active="${this._activeTab}">
        <div slot="providers">
          ${this.renderProvidersSettings()}
        </div>
        <div slot="general">
          ${this.renderGeneralSettings()}
        </div>
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
      <b-form id="generalSettingsForm"></b-form>

      <div class="actions-bar">
        <b-button variant="secondary" id="resetSettings">Reset to Defaults</b-button>
        <b-button variant="primary" id="saveSettings">Save Settings</b-button>
      </div>
    `;
  }

  private getGeneralSettingsSchema() {
    return {
      // b-form treats only a top-level group named 'root' as the root; any other name puts every field path one
      // level too deep, and the selects never receive their options.
      name: 'root',
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
    // #/settings/providers and #/settings/general open their tab (TASK-113).
    const tab = this.getAttribute('tab');
    if (tab === 'providers' || tab === 'general') this._activeTab = tab;
    // Data loads once; the DOM is wired in onUpdated(), which runs after every render.
    this.loadProviders();
    this.loadSettings();
  }

  protected onUpdated() {
    // listen() handlers are dropped on the next update(), so wiring after each render does not stack.
    // Set up event listeners
    this.setupTabs();
    this.setupGeneralForm();
    this.setupProvidersEvents();
    this.setupSettingsEvents();
  }

  /** b-form forms that already have their schema; setSchema() re-renders a form, so it runs once per element. */
  private _formsWithSchema = new WeakSet<Element>();

  private setupGeneralForm() {
    // b-form takes its schema and values through setSchema()/setValues(), not attributes (TASK-112).
    const form = this.$<BForm>('#generalSettingsForm');
    if (!form || this._formsWithSchema.has(form)) return;
    form.setSchema(this.getGeneralSettingsSchema());
    form.setValues(this.toFormValues(this._settings));
    this._formsWithSchema.add(form);
  }

  /** Settings in the form's shape: values nest under their group names. */
  private toFormValues(settings: UserSettings): Record<string, unknown> {
    return {
      appearance: { theme: settings.theme, language: settings.language },
      notifications: {
        notifications_enabled: settings.notifications,
        autoSave: settings.autoSave,
        debugMode: settings.debugMode,
      },
    };
  }

  private setupTabs() {
    // b-tabs takes its tab labels from setTabs() and its panels from children slotted by tab id (TASK-112).
    const tabs = this.$<BTabs>('#settingsTabs');
    if (!tabs) return;
    tabs.setTabs(SETTINGS_TABS, this._activeTab);
    this.listen<CustomEvent<{ tab: SettingsTab }>>(tabs, 'tab-change', (e) => {
      // b-tabs switches the panel itself; remember the tab so the next render keeps it.
      this._activeTab = e.detail.tab;
    });
  }

  private setupProvidersEvents() {
    this.on('#addProvider', 'click', () => {
      console.log('Add provider clicked');
      this.emit('add-provider');
    });

    this.$$('[data-edit]').forEach(btn => {
      this.listen(btn, 'click', () => {
        const providerId = btn.getAttribute('data-edit');
        this.editProvider(providerId!);
      });
    });

    this.$$('[data-test]').forEach(btn => {
      this.listen(btn, 'click', () => {
        const providerId = btn.getAttribute('data-test');
        this.testProvider(providerId!);
      });
    });

    this.$$('[data-delete]').forEach(btn => {
      this.listen(btn, 'click', () => {
        const providerId = btn.getAttribute('data-delete');
        this.deleteProvider(providerId!);
      });
    });
  }

  private setupSettingsEvents() {
    this.on('#saveSettings', 'click', () => this.saveSettingsFromForm());
    this.on('#resetSettings', 'click', () => this.resetSettings());
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
    const form = this.$<BForm>('#generalSettingsForm');

    if (!form) {
      this.showMessage('Form not found', 'error');
      return;
    }

    const result = form.validate();

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

    // Map form data (nested by group) back to settings structure
    const appearance = (result.data.appearance ?? {}) as Record<string, unknown>;
    const notifications = (result.data.notifications ?? {}) as Record<string, unknown>;
    this._settings = {
      theme: (appearance.theme as UserSettings['theme']) || 'auto',
      language: appearance.language as string || 'en',
      notifications: notifications.notifications_enabled as boolean || false,
      autoSave: notifications.autoSave as boolean || false,
      debugMode: notifications.debugMode as boolean || false
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
    this.$<BForm>('#generalSettingsForm')?.setValues(this.toFormValues(this._settings));
    this.showMessage('Settings reset to defaults', 'info');
  }

  private showMessage(message: string, type: 'success' | 'info' | 'warning' | 'error') {
    // Use toast notification service
    toast.show(message, type);
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
