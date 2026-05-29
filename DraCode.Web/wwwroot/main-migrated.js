import { DraCodeClient } from './client-migrated.js';
// Initialize application when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    // Initialize migrated client (now using Birko.Web components)
    const client = new DraCodeClient();
    // Expose client to window for inline event handlers
    window.draCodeClient = client;
    // Setup global functions for manual provider form
    window.connectManualProvider = () => {
        const provider = document.getElementById('manualProvider').value;
        const apiKey = document.getElementById('manualApiKey').value;
        const model = document.getElementById('manualModel').value;
        const workingDir = document.getElementById('manualWorkingDir').value;
        client.connectManualProvider(provider, apiKey, model || undefined, workingDir || undefined);
        // Clear form
        document.getElementById('manualApiKey').value = '';
        document.getElementById('manualModel').value = '';
        document.getElementById('manualWorkingDir').value = '';
        client.toggleManualConfig();
    };
    window.toggleManualConfig = () => {
        client.toggleManualConfig();
    };
    // Add toggle function for collapsible sections
    window.toggleSection = (sectionId) => {
        const content = document.getElementById(sectionId);
        const icon = document.getElementById(`${sectionId}Icon`);
        const header = icon?.closest('.collapsible-header');
        if (content && icon && header) {
            const isCollapsed = content.classList.contains('collapsed');
            if (isCollapsed) {
                // Expanding
                content.classList.remove('collapsed');
                icon.classList.remove('collapsed');
                header.classList.remove('collapsed');
                // Remove count badge when expanded
                const badge = header.querySelector('.provider-count-badge');
                if (badge) {
                    badge.remove();
                }
                // Show provider filter if this is the providers section
                if (sectionId === 'providersContent') {
                    const providerFilter = document.querySelector('.provider-filter');
                    if (providerFilter) {
                        providerFilter.style.display = 'flex';
                    }
                }
            }
            else {
                // Collapsing
                content.classList.add('collapsed');
                icon.classList.add('collapsed');
                header.classList.add('collapsed');
                // Hide provider filter if this is the providers section
                if (sectionId === 'providersContent') {
                    const providerFilter = document.querySelector('.provider-filter');
                    if (providerFilter) {
                        providerFilter.style.display = 'none';
                    }
                }
                // Add count badge for providers section
                if (sectionId === 'providersContent') {
                    const providersGrid = document.getElementById('providersGrid');
                    const providerCards = providersGrid?.querySelectorAll('provider-card:not([style*="display: none"])');
                    const count = providerCards?.length || 0;
                    if (count > 0) {
                        const badge = document.createElement('span');
                        badge.className = 'provider-count-badge';
                        badge.textContent = `${count}`;
                        badge.title = `${count} provider(s) available`;
                        header.appendChild(badge);
                    }
                }
            }
            // Store state in localStorage
            localStorage.setItem(`section_${sectionId}_collapsed`, (!isCollapsed).toString());
        }
    };
    // Restore collapsed states from localStorage
    const restoreCollapsedStates = () => {
        ['serverConnection', 'providersContent'].forEach(sectionId => {
            const isCollapsed = localStorage.getItem(`section_${sectionId}_collapsed`) === 'true';
            if (isCollapsed) {
                const content = document.getElementById(sectionId);
                const icon = document.getElementById(`${sectionId}Icon`);
                const header = icon?.closest('.collapsible-header');
                if (content && icon && header) {
                    content.classList.add('collapsed');
                    icon.classList.add('collapsed');
                    header.classList.add('collapsed');
                    // Hide provider filter if this is the providers section
                    if (sectionId === 'providersContent') {
                        const providerFilter = document.querySelector('.provider-filter');
                        if (providerFilter) {
                            providerFilter.style.display = 'none';
                        }
                    }
                }
            }
        });
    };
    // Restore states after DOM is ready
    setTimeout(restoreCollapsedStates, 100);
    console.log('✨ DraCode Client initialized with Birko.Web Components');
    console.log('📦 Available Birko components:', [
        'b-input', 'b-select', 'b-button', 'b-checkbox', 'b-switch', 'b-radio',
        'b-textarea', 'b-multi-select', 'b-search-input', 'b-file-upload',
        'b-inline-edit', 'b-form', 'b-card', 'b-modal', 'b-drawer', 'b-tabs',
        'b-confirm-dialog', 'b-dropdown-menu', 'b-tooltip', 'b-table', 'b-data-table',
        'b-pagination', 'b-badge', 'b-chart', 'toast', 'b-spinner', 'b-empty',
        'b-skeleton', 'b-sidebar', 'b-breadcrumb', 'b-ribbon', 'b-tree-menu'
    ]);
    console.log('💡 Debug commands available:');
    console.log('   debugShowProviders() - Force show provider section');
    console.log('   debugCheckElements() - Check DOM elements');
    console.log('   draCodeClient - Access client instance');
});
